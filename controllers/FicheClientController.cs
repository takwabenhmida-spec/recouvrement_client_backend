using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecouvrementAPI.Data;
using RecouvrementAPI.DTOs;
using RecouvrementAPI.Models;
using RecouvrementAPI.Helpers;

namespace RecouvrementAPI.Controllers
{
    /// <summary>
    /// Contrôleur "Master" agglomérant l'intégralité du contexte d'un Client (Vue Détail / Fiche Client).
    /// Route externe API : /api/FicheClient
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class FicheClientController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FicheClientController> _logger;

        public FicheClientController(ApplicationDbContext context, ILogger<FicheClientController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Agglomère, en 1 seule trame JSON, la vision à 360 degrés d'un client. 
        /// Route externe API = GET http://localhost:5203/api/FicheClient/123
        /// Utilisé par la maquette "Liste des clients / Fiche client"
        /// </summary>
        [HttpGet("{idClient}")]
        public async Task<ActionResult<FicheClientResponseDto>> GetFicheComplete(int idClient)
        {
            try
            {
                // L'arme secrète d'Entity Framework : l'Eager loading profond
                var client = await _context.Clients
                    .Include(c => c.Agence)
                    .Include(c => c.Dossiers)
                        .ThenInclude(d => d.Echeances)
                    .Include(c => c.Dossiers)
                        .ThenInclude(d => d.Intentions)
                    .Include(c => c.Dossiers)
                        .ThenInclude(d => d.ScoresRisque)
                    .Include(c => c.Dossiers)
                        .ThenInclude(d => d.Communications)
                    .Include(c => c.Dossiers)
                        .ThenInclude(d => d.HistoriqueActions)
                    .FirstOrDefaultAsync(c => c.IdClient == idClient);

                if (client == null)
                    return NotFound(new { message = "La fiche de ce client n'existe pas en base." });

                // Refus d'accès si le client est archivé
                if (client.Statut == "Archivé")
                    return BadRequest(new { message = "Ce client est archivé et ne peut plus être consulté depuis le Back-office actif." });

                // 1. Informations Personnelles (Haut Gauche)
                // Extrait le statut de dossier le plus grave (contentieux > aimable > regularise)
                var statutOrdre = new List<string> { "contentieux", "aimable", "regularise" };
                var pireStatut = client.Dossiers
                    .OrderBy(d => statutOrdre.IndexOf(d.StatutDossier))
                    .FirstOrDefault()?.StatutDossier ?? "aimable";

                // Le pire score (IA risk tolerance)
                var scoresTousDossiers = client.Dossiers
                    .SelectMany(d => d.ScoresRisque)
                    .OrderByDescending(s => s.ScoreTotal)
                    .FirstOrDefault();

                var infos = new FicheClientInfoDto
                {
                    NomComplet = $"{client.Nom} {client.Prenom}",
                    CIN = client.CIN ?? "Non renseigné",
                    Telephone = client.Telephone,
                    Email = client.Email,
                    Agence = client.Agence?.Ville ?? "Siège",
                    Adresse = client.Adresse ?? "Inconnue",
                    ClientDepuis = client.Dossiers.Count > 0 ? client.Dossiers.Min(d => d.DateCreation).Year.ToString() : "N/A",
                    PireStatut = char.ToUpper(pireStatut[0]) + pireStatut.Substring(1).ToLower(),
                    PireScore = scoresTousDossiers?.ScoreTotal ?? 0
                };

                // 2. Score de Risque IA (Haut Droite) -> Panel IA Repris 
                ScoringDetailsDto iaPanel = null;
                if (scoresTousDossiers != null)
                {
                    iaPanel = new ScoringDetailsDto
                    {
                        PtsRetard = scoresTousDossiers.PointsRetard,
                        PtsHistorique = scoresTousDossiers.PointsHistorique,
                        PtsGarantie = scoresTousDossiers.PointsGarantie,
                        PtsIntention = scoresTousDossiers.PointsIntention,
                        ScoreTotal = scoresTousDossiers.ScoreTotal
                    };
                }

                // 3. Dossiers du client (Tableau Central 1)
                var dtosDossiers = client.Dossiers.Select(d => new FicheDossierDto
                {
                    IdDossier = d.IdDossier,
                    TypeCredit = d.TypeEmprunt,
                    MontantInitial = d.MontantInitial,
                    MontantDu = d.MontantImpaye,
                    Frais = d.FraisDossier,
                    Taux = d.TauxInteret,
                    RetardJours = RecouvrementHelper.CalculerJoursRetard(d.Echeances),
                    Statut = d.StatutDossier
                }).ToList();

                // 4. Intentions (Tableau Central 2)
                var listeIntentions = client.Dossiers
                    .SelectMany(d => d.Intentions.Select(i => new { intention = i, dossier = d }))
                    .OrderByDescending(x => x.intention.DateIntention)
                    .Select(x => 
                    {
                        // Matcher la comm client sur ce dossier proche de la date (Heuristique simplifiée)
                        var objCom = x.dossier.Communications
                            .OrderByDescending(c => c.DateEnvoi)
                            .FirstOrDefault(c => c.Origine == "client");

                        return new FicheIntentionDto
                        {
                            Date = x.intention.DateIntention.ToString("dd/MM/yyyy"),
                            Intention = x.intention.TypeIntention,
                            Statut = x.intention.Statut ?? "En attente",
                            Commentaire = objCom != null ? objCom.Message : ""
                        };
                    }).ToList();

                // 5. Infos "Token d'accès client" et "Historique" (Bas de Maquette)
                // Extrêmement utile pour prouver au jury PFE que tout le cycle a été surveillé.
                var actionsAudit = client.Dossiers
                    .SelectMany(d => d.HistoriqueActions)
                    .OrderByDescending(a => a.DateAction)
                    .Select(a => new FicheHistoriqueDto
                    {
                        Date = a.DateAction.ToString("dd/MM/yyyy · HH:mm"),
                        Action = a.ActionDetail,
                        Acteur = a.Acteur
                    }).ToList();

                var tokenInfos = new FicheTokenStateDto
                {
                    EnvoyéLe = "Sur événement dynamique", // Déterminé par la campagne de relance
                    Canal = "Email + SMS",
                    FormulaireSoumis = listeIntentions.Count > 0 ? "Oui" : "Non",
                    LienUrl = $"https://recouvrement.stb.tn/formulaire/{client.TokenAcces ?? "NON-GENERE"}"
                };

                return Ok(new FicheClientResponseDto
                {
                    Infos = infos,
                    IA = iaPanel,
                    Dossiers = dtosDossiers,
                    Intentions = listeIntentions,
                    TokenInfos = tokenInfos,
                    Historiques = actionsAudit
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Crash chargement fiche client 360.");
                return StatusCode(500, new { message = "Assemblage complexe refusé." });
            }
        }

        // Logic moved to RecouvrementHelper
    }
}
