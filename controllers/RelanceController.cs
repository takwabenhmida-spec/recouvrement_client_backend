using Microsoft.AspNetCore.Authorization;
using RecouvrementAPI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecouvrementAPI.Data;
using RecouvrementAPI.DTOs;
using RecouvrementAPI.Models;

namespace RecouvrementAPI.Controllers
{
    /// <summary>
    /// Contrôleur destiné à monitorer, historiser et agir sur la "Gestion des relances".
    /// Mesure les performances des e-mails, les appels agents, et les SMS envoyés.
    /// Route de base API : /api/Relance
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RelanceController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RelanceController> _logger;
        private readonly RecouvrementAPI.Services.IEmailService _emailService;
        private readonly RecouvrementAPI.Services.ISmsService _smsService;

        public RelanceController(
            ApplicationDbContext context, 
            ILogger<RelanceController> logger,
            RecouvrementAPI.Services.IEmailService emailService,
            RecouvrementAPI.Services.ISmsService smsService)
        {
            _context = context;
            _logger = logger;
            _emailService = emailService;
            _smsService = smsService;
        }

        /// <summary>
        /// Renvoie l'historique des relances paginé + les analytiques d'efficacités des campagnes (Top KPI + Canaux).
        /// Route API ex: GET http://localhost:5203/api/Relance/dashboard?canal=sms&statut=repondu
        /// </summary>
        [HttpGet("dashboard")]
        public async Task<ActionResult<RelanceDashboardResponseDto>> GetRelancesDashboard(
            [FromQuery] string canal = "Tous",
            [FromQuery] string statut = "Tous",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                // Inclut tout le chaînage depuis Relance jusqu'aux communications en réponse
                var relances = await _context.Relances
                    .Include(r => r.Dossier)
                        .ThenInclude(d => d.Client)
                    .Include(r => r.Communications) // Requis car si un client envoie un motif partiel, on le lie
                    .ToListAsync();

                // 1. Calcul Analytics (Dashboard Top KPI)
                int totalEnvoyees = relances.Count;
                int formulairesSoumis = relances.Count(r => r.Statut == AppConstants.RelanceStatut.Replied);
                int enAttente = totalEnvoyees - formulairesSoumis;
                
                decimal tauxReponse = totalEnvoyees > 0 ? (decimal)formulairesSoumis / totalEnvoyees * 100 : 0;

                // 2. Ventilation par canaux
                var canaux = CalculerStatsCanaux(relances);

                // -------------------------------------------------------------
                // Assemblage de la Table d'audit paginée
                // -------------------------------------------------------------
                var query = relances.AsEnumerable();
                
                // Moteur de recherche filtré
                if (!string.IsNullOrEmpty(canal) && canal != "Tous")
                {
                    query = query.Where(r => r.Moyen.Equals(canal, StringComparison.OrdinalIgnoreCase));
                }
                if (!string.IsNullOrEmpty(statut) && statut != "Tous")
                {
                    query = query.Where(r => r.Statut.Equals(statut, StringComparison.OrdinalIgnoreCase));
                }

                var itemsToMap = query.OrderByDescending(r => r.DateRelance).ToList();
                int totalItems = itemsToMap.Count;
                int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                var paginatedItems = itemsToMap
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(r => new RelanceItemDto
                    {
                        IdRelance = r.IdRelance,
                        IdDossier = r.IdDossier,
                        Client = $"{r.Dossier.Client.Nom} {r.Dossier.Client.Prenom}",
                        Telephone = r.Dossier.Client.Telephone,
                        Email = r.Dossier.Client.Email,
                        Canal = r.Moyen,
                        
                        // Sécurité: masquage partiel du token JWT client (ex. 6fE8a1b2...)
                        Token = FormatToken(r.Dossier.Client.TokenAcces),
                        
                        Statut = r.Statut,
                        
                        // Détection heuristique si le client a répondu, on interroge la sous-table Communications
                        // Ceci permet d'afficher en direct le "Demande d'échéancier" ou "Paiement en cours"
                        Reponse = r.Statut == AppConstants.RelanceStatut.Replied ? 
                            (r.Communications.OrderByDescending(c => c.DateEnvoi).FirstOrDefault(c => c.Origine == AppConstants.ClientActor)?.Message ?? "Demande soumise") 
                            : "Aucune"
                    }).ToList();

                return Ok(new RelanceDashboardResponseDto
                {
                    Kpis = new RelanceKpiDto
                    {
                        TotalEnvoyees = totalEnvoyees,
                        EnAttenteReponse = enAttente,
                        FormulairesSoumis = formulairesSoumis,
                        TauxReponse = Math.Round(tauxReponse, 1)
                    },
                    Canaux = canaux,
                    Items = paginatedItems,
                    TotalItems = totalItems,
                    TotalPages = totalPages,
                    CurrentPage = page
                });
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Échec lors de la récolte des logs Relance.");
                return StatusCode(500, new { message = "L'API a échoué à composer l'historique des requêtes manuelles." });
            }
        }

        /// <summary>
        /// API DE SIMULATION : L'agent a cliqué sur "Relancer".
        /// Génère le Token unique, met à jour le client, trace l'action et fabrique le faux message.
        /// Route externe API : POST http://localhost:5203/api/Relance/{idDossier}/envoyer-token
        /// </summary>
        [HttpPost("{idDossier}/envoyer-token")]
        public async Task<ActionResult<EnvoiTokenResponseDto>> EnvoyerToken(int idDossier, [FromBody] EnvoiTokenDto req)
        {
            try
            {
                var dossier = await _context.Dossiers
                    .Include(d => d.Client)
                    .FirstOrDefaultAsync(d => d.IdDossier == idDossier);

                if (dossier == null)
                    return NotFound(new { message = "Dossier introuvable." });

                // 1. Génération d'un UUID inviolable
                string nouveauToken = Guid.NewGuid().ToString("N");
                
                // 2. Mise à jour du client
                dossier.Client.TokenAcces = nouveauToken;
                dossier.Client.TokenExpireLe = DateTime.Now.AddDays(7);

                // 3. Traçabilité (Création de la relance manuelle)
                var relance = new RelanceClient
                {
                    IdDossier = dossier.IdDossier,
                    Moyen = req.Canal ?? "sms", // Par défaut on simule un sms
                    Statut = AppConstants.RelanceStatut.Sent,
                    DateRelance = DateTime.Now,
                    Contenu = $"Envoi {req.Canal} manuel depuis interface"
                };

                _context.Relances.Add(relance);

                await _context.SaveChangesAsync();

                // 4. Construction et Envoi Réel
                string lien = $"https://stbbank.tn/portail/{nouveauToken}";
                string messageSimulation = "";

                if (req.Canal == "sms")
                {
                    messageSimulation = $"[ENVOI SMS] STB BANK: Cher {dossier.Client.Nom}, veuillez régler votre retard via : {lien}";
                    if (!string.IsNullOrEmpty(dossier.Client.Telephone))
                    {
                        await _smsService.SendSmsAsync(dossier.Client.Telephone, messageSimulation);
                    }
                }
                else if (req.Canal == "email")
                {
                    messageSimulation = $"[ENVOI E-MAIL] STB BANK - Action Requise. Accès sécurisé : {lien}";
                    if (!string.IsNullOrEmpty(dossier.Client.Email))
                    {
                        await _emailService.SendEmailAsync(dossier.Client.Email, "STB BANK - Rappel de Paiement", messageSimulation);
                    }
                }

                // Logger dans le backend
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("[ENVOI TOKEN CANAL: {Canal}] Dossier: {DossierId} | Client: {Client}", req.Canal, idDossier, dossier.Client.Nom);
                }
                
                return Ok(new EnvoiTokenResponseDto
                {
                    Message = messageSimulation,
                    TokenGenere = nouveauToken,
                    LienPaiement = lien
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la génération du token.");
                return StatusCode(500, new { message = "Impossible d'expédier le lien d'accès." });
            }
        }

        private static RelanceCanalStatDto CalculerStatsCanaux(List<RelanceClient> relances)
        {
            return new RelanceCanalStatDto
            {
                Appels = relances.Count(r => r.Moyen == "appel"),
                AppelsDecroches = relances.Count(r => r.Moyen == "appel" && r.Statut == AppConstants.RelanceStatut.Replied),
                AppelsNonJoignables = relances.Count(r => r.Moyen == "appel" && r.Statut != AppConstants.RelanceStatut.Replied),

                SmsEnvoyes = relances.Count(r => r.Moyen == "sms"),
                SmsRepondus = relances.Count(r => r.Moyen == "sms" && r.Statut == AppConstants.RelanceStatut.Replied),
                SmsEnAttente = relances.Count(r => r.Moyen == "sms" && r.Statut != AppConstants.RelanceStatut.Replied),

                EmailsEnvoyes = relances.Count(r => r.Moyen == "email"),
                EmailsRepondus = relances.Count(r => r.Moyen == "email" && r.Statut == AppConstants.RelanceStatut.Replied),
                EmailsEnAttente = relances.Count(r => r.Moyen == "email" && r.Statut != AppConstants.RelanceStatut.Replied)
            };
        }

        private static string FormatToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return "";
            return token.Length > 8 ? string.Concat(token.AsSpan(0, 8), "...") : token;
        }
    }
}
