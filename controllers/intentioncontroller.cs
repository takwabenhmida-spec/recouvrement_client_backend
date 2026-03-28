using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecouvrementAPI.Data;
using RecouvrementAPI.DTOs;
using RecouvrementAPI.Models;

namespace RecouvrementAPI.Controllers
{
    /// <summary>
    /// Contrôleur gérant les formulaires de "Réponse d'intention" du client.
    /// Route API de base : /api/Intention
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class IntentionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<IntentionController> _logger;

        public IntentionController(ApplicationDbContext context, ILogger<IntentionController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Renvoie la vue tableau de bord des intentions (KPIs et Liste).
        /// Route externe API : GET http://localhost:5203/api/Intention/dashboard
        /// </summary>
        [HttpGet("dashboard")]
        public async Task<ActionResult<IntentionDashboardResponseDto>> GetDashboard(
            [FromQuery] string typeIntention = "Tous",
            [FromQuery] string statut = "Tous",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var intentionsQuery = _context.Intentions
                    .Include(i => i.Dossier)
                        .ThenInclude(d => d.Client)
                            .ThenInclude(c => c.Agence)
                    .Include(i => i.Dossier)
                        .ThenInclude(d => d.Echeances)
                    .Include(i => i.Dossier)
                        .ThenInclude(d => d.Communications)
                    .Include(i => i.Dossier)
                        .ThenInclude(d => d.ScoresRisque) // Requis pour calculer la confiance IA
                    .AsQueryable();

                // Calculs des KPIs (Top des tuiles)
                var currentMonth = DateTime.Now.Month;
                var currentYear = DateTime.Now.Year;
                
                var allIntentionsForKpi = await intentionsQuery.ToListAsync();

                int totalRecues = allIntentionsForKpi.Count(i => i.DateIntention.Month == currentMonth && i.DateIntention.Year == currentYear);
                int nonTraitees = allIntentionsForKpi.Count(i => i.Statut == "En attente");
                int paiementImmediat = allIntentionsForKpi.Count(i => i.TypeIntention == "paiement_immediat" && i.Statut == "En attente");
                int reclamations = allIntentionsForKpi.Count(i => i.TypeIntention == "reclamation");

                // Filtres Datatable
                if (!string.IsNullOrEmpty(typeIntention) && typeIntention != "Tous")
                {
                    intentionsQuery = intentionsQuery.Where(i => i.TypeIntention == typeIntention);
                }
                if (!string.IsNullOrEmpty(statut) && statut != "Tous")
                {
                    intentionsQuery = intentionsQuery.Where(i => i.Statut == statut);
                }

                int totalItems = await intentionsQuery.CountAsync();
                int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                var intentionsList = await intentionsQuery
                    .OrderByDescending(i => i.DateIntention)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var items = intentionsList.Select(i => 
                {
                    // Recherche du texte libre inséré par le client
                    // On prend la dernière communication reçue de source client sur ce dossier avant/le moment de l'intention
                    var commClient = i.Dossier.Communications
                        .Where(c => c.Origine == "client" && c.DateEnvoi >= i.DateIntention.AddHours(-1))
                        .OrderByDescending(c => c.DateEnvoi)
                        .FirstOrDefault();

                    return new IntentionItemDto
                    {
                        IdIntention = i.IdIntention,
                        IdDossier = i.IdDossier,
                        Client = $"{i.Dossier.Client.Nom} {i.Dossier.Client.Prenom}",
                        DateSoumission = i.DateIntention,
                        MontantDu = i.Dossier.MontantImpaye,
                        Agence = i.Dossier.Client.Agence?.Ville ?? "Non affecté",
                        TypeCredit = i.Dossier.TypeEmprunt,
                        CommentaireClient = i.MontantPropose.HasValue 
                            ? $"Montant proposé: {i.MontantPropose} TND. {commClient?.Message}" 
                            : commClient?.Message ?? "Aucun commentaire fourni",
                        Canal = "Email+SMS", // Standard fallback
                        Retard = CalculerJoursRetard(i.Dossier.Echeances),
                        Statut = i.Statut ?? "En attente",
                        TypeIntention = i.TypeIntention,
                        ConfianceClient = i.ConfianceClient,
                        // IA Confidence: If score is 0-30 (Safe) -> Confidence is high (e.g. 90%+). 
                        // Formula: 100 - Score
                        ConfianceIa = (int)(100 - (i.Dossier.ScoresRisque?.OrderByDescending(s => s.DateCalcul).FirstOrDefault()?.Valeur ?? 25))
                    };
                }).ToList();

                return Ok(new IntentionDashboardResponseDto
                {
                    Kpis = new IntentionKpiDto
                    {
                        TotalRecues = totalRecues,
                        NonTraitees = nonTraitees,
                        PaiementImmediat = paiementImmediat,
                        Reclamations = reclamations
                    },
                    Items = items,
                    TotalItems = totalItems,
                    TotalPages = totalPages,
                    CurrentPage = page
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur au rendu du Dashboard Intention.");
                return StatusCode(500, new { message = "Erreur de chargement des intentions." });
            }
        }

        /// <summary>
        /// Accepter ou refuser une intention en attente.
        /// Route externe API : PUT http://localhost:5203/api/Intention/{id}/decision
        /// </summary>
        [HttpPut("{id}/decision")]
        public async Task<IActionResult> MakeDecision(int id, [FromBody] IntentionDecisionDto decisionDto)
        {
            try
            {
                var intention = await _context.Intentions.FindAsync(id);
                if (intention == null) return NotFound(new { message = "Intention introuvable." });

                if (decisionDto.Decision != "Accepter" && decisionDto.Decision != "Refuser")
                    return BadRequest(new { message = "La décision doit être 'Accepter' ou 'Refuser'." });

                intention.Statut = decisionDto.Decision == "Accepter" ? "Accepté" : "Refusé";

                _context.HistoriqueActions.Add(new HistoriqueAction
                {
                    IdDossier = intention.IdDossier,
                    ActionDetail = $"L'intention de type '{intention.TypeIntention}' a été {intention.Statut.ToLower()}.",
                    Acteur = "agent", 
                    DateAction = DateTime.Now
                });

                await _context.SaveChangesAsync();

                return Ok(new { message = $"Intention traitée ({intention.Statut})." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur décision intention.");
                return StatusCode(500, new { message = "Impossible d'appliquer la décision." });
            }
        }

        private int CalculerJoursRetard(IEnumerable<Echeance> echeances)
        {
            var echeancesDepassees = echeances
                .Where(e => e.Statut == "impaye" && e.DateEcheance < DateTime.Now)
                .ToList();

            if (!echeancesDepassees.Any()) return 0;
            return (int)(DateTime.Now - echeancesDepassees.Min(e => e.DateEcheance)).TotalDays;
        }
    }
}