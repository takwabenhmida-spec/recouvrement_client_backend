using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecouvrementAPI.Data;
using RecouvrementAPI.DTOs;
using RecouvrementAPI.Models;
using RecouvrementAPI.Helpers;

namespace RecouvrementAPI.Controllers
{
    // Contrôleur qui gère les réponses du client face à son dossier impayé
    // Route de base : http://localhost:5203/api/intention
    [ApiController]
    [Route("api/intention")]
    public class IntentionAdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<IntentionAdminController> _logger;

        private const string STATUT_EN_ATTENTE = "En attente";
        private const string ORIGINE_SYSTEME = "systeme";
        private const string ACTEUR_CLIENT = "client";

        public IntentionAdminController(ApplicationDbContext context, ILogger<IntentionAdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ==============================
        // POST api/intention
        // Appelé quand le client valide son choix sur le portail Angular
        // Reçoit un objet JSON : { idDossier, typeIntention, commentaire, ... }
        //
        // Types acceptés :
        //   - paiement_immediat    → Règlement total immédiat
        //   - paiement_partiel     → Règlement partiel (montantPropose requis)
        //   - promesse_paiement    → Engagement futur (datePaiementPrevue requis)
        //   - demande_consolidation→ Demande de regroupement de dettes
        //   - demande_echeance     → Demande d'échéancier de paiement
        //   - reclamation          → Contestation de la dette
        // ==============================
        [HttpPost]
        [Authorize] // Sécurisé : nécessite un token valide. Le frontend client DOIT utiliser /api/client/intention/{tokenAcces}
        public async Task<IActionResult> AjouterIntention([FromBody] IntentionClient intention)
        {
            // Vérification 1 : le body JSON n'est pas null
            if (intention == null)
                return BadRequest(new { message = "Données manquantes." });

            // Vérification 2 : le type d'intention est obligatoire
            if (string.IsNullOrEmpty(intention.TypeIntention))
                return BadRequest(new { message = "Le type d'intention est requis." });

            // Vérification 3 : le dossier existe dans la BDD
            var dossier = await _context.Dossiers.FindAsync(intention.IdDossier);
            if (dossier == null)
                return NotFound(new { message = "Dossier introuvable." });

            // SÉCURITÉ : Blocage multi-soumission
            // Un client ne peut soumettre qu'UNE SEULE intention par jour (toutes types confondus)
            bool dejaSoumis = await _context.Intentions.AnyAsync(i =>
                i.IdDossier == intention.IdDossier &&
                i.DateIntention.Date == DateTime.Today);

            if (dejaSoumis)
                return BadRequest(new
                {
                    message = "Vous avez déjà soumis une réponse aujourd'hui. Veuillez contacter votre agence pour toute modification."
                });

            // Date remplie automatiquement côté serveur
            intention.DateIntention = DateTime.Now;
            intention.Statut = STATUT_EN_ATTENTE;

            // Commentaire optionnel
            string commentairePart = string.IsNullOrEmpty(intention.Commentaire)
                ? ""
                : $" Commentaire : {intention.Commentaire}";

            var validationResult = intention.TypeIntention switch
            {
                "paiement_immediat"     => HandlePaiementImmediat(intention, commentairePart),
                "paiement_partiel"      => HandlePaiementPartiel(intention, dossier, commentairePart),
                "promesse_paiement"     => HandlePromessePaiement(intention, dossier, commentairePart),
                "demande_consolidation" => HandleDemandeConsolidation(intention, commentairePart),
                "demande_echeance"      => HandleDemandeEcheance(intention, commentairePart),
                "reclamation"           => HandleReclamation(intention, dossier, commentairePart),
                _                       => BadRequest(new
                {
                    message = "Type d'intention invalide.",
                    typesAcceptes = new[] {
                        "paiement_immediat",
                        "paiement_partiel",
                        "promesse_paiement",
                        "demande_consolidation",
                        "demande_echeance",
                        "reclamation"
                    }
                })
            };

            if (validationResult is BadRequestObjectResult)
                return validationResult;

            // Ajout de l'intention dans le contexte
            _context.Intentions.Add(intention);

            // Sauvegarde tout en BDD en une seule transaction :
            // intention + communication + historique + éventuel statut dossier
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Intention enregistrée avec succès. L'agent traitera votre demande dans les meilleurs délais.",
                type = intention.TypeIntention,
                idIntention = intention.IdIntention
            });
        }

        // ==============================
        // GET api/intention/{idDossier}
        // Récupère l'historique des intentions d'un dossier
        // Utilisé par l'agent dans le back-office
        // ==============================
        [HttpGet("{idDossier}")]
        [Authorize]
        public async Task<IActionResult> GetIntentions(int idDossier)
        {
            var intentions = await _context.Intentions
                .Where(i => i.IdDossier == idDossier)
                .OrderByDescending(i => i.DateIntention)
                .ToListAsync();

            if (intentions.Count == 0)
                return NotFound(new { message = "Aucune intention trouvée pour ce dossier." });

            return Ok(intentions);
        }

        // ==============================
        // GET api/intention/dashboard
        // Tableau de bord agent : KPIs + liste paginée des intentions
        // ==============================
        [HttpGet("dashboard")]
        [Authorize]
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
                        .ThenInclude(d => d.ScoresRisque)
                    .AsQueryable();

                var currentMonth = DateTime.Now.Month;
                var currentYear = DateTime.Now.Year;

                var allIntentionsForKpi = await intentionsQuery.ToListAsync();

                int totalRecues        = allIntentionsForKpi.Count(i => i.DateIntention.Month == currentMonth && i.DateIntention.Year == currentYear);
                int nonTraitees        = allIntentionsForKpi.Count(i => i.Statut == STATUT_EN_ATTENTE);
                int paiementImmediat   = allIntentionsForKpi.Count(i => i.TypeIntention == "paiement_immediat" && i.Statut == STATUT_EN_ATTENTE);
                int reclamations       = allIntentionsForKpi.Count(i => i.TypeIntention == "reclamation");

                if (!string.IsNullOrEmpty(typeIntention) && typeIntention != "Tous")
                    intentionsQuery = intentionsQuery.Where(i => i.TypeIntention == typeIntention);

                if (!string.IsNullOrEmpty(statut) && statut != "Tous")
                    intentionsQuery = intentionsQuery.Where(i => i.Statut == statut);

                int totalItems = await intentionsQuery.CountAsync();
                int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                var intentionsList = await intentionsQuery
                    .OrderByDescending(i => i.DateIntention)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var items = intentionsList.Select(i =>
                {
                    var commClient = i.Dossier.Communications
                        .Where(c => c.Origine == ACTEUR_CLIENT && c.DateEnvoi >= i.DateIntention.AddHours(-1))
                        .OrderByDescending(c => c.DateEnvoi)
                        .FirstOrDefault();

                    return new IntentionItemDto
                    {
                        IdIntention       = i.IdIntention,
                        IdDossier         = i.IdDossier,
                        Client            = $"{i.Dossier.Client.Nom} {i.Dossier.Client.Prenom}",
                        DateSoumission    = i.DateIntention,
                        MontantDu         = i.Dossier.MontantImpaye,
                        Agence            = i.Dossier.Client.Agence?.Ville ?? "Non affecté",
                        TypeCredit        = i.Dossier.TypeEmprunt,
                        CommentaireClient = i.MontantPropose.HasValue
                            ? $"Montant proposé: {i.MontantPropose} TND. {commClient?.Message}"
                            : commClient?.Message ?? "Aucun commentaire fourni",
                        Canal             = "Email+SMS",
                        Retard            = RecouvrementHelper.CalculerJoursRetard(i.Dossier.Echeances),
                        Statut            = i.Statut ?? STATUT_EN_ATTENTE,
                        TypeIntention     = i.TypeIntention
                    };
                }).ToList();

                return Ok(new IntentionDashboardResponseDto
                {
                    Kpis = new IntentionKpiDto
                    {
                        TotalRecues      = totalRecues,
                        NonTraitees      = nonTraitees,
                        PaiementImmediat = paiementImmediat,
                        Reclamations     = reclamations
                    },
                    Items       = items,
                    TotalItems  = totalItems,
                    TotalPages  = totalPages,
                    CurrentPage = page
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur au rendu du Dashboard Intention.");
                return StatusCode(500, new { message = "Erreur de chargement des intentions." });
            }
        }

        

        // ==============================
        // MÉTHODES PRIVÉES DE GESTION DES INTENTIONS
        // ==============================

        private OkResult HandlePaiementImmediat(IntentionClient intention, string commentairePart)
        {
            _context.Communications.Add(new Communication
            {
                IdDossier = intention.IdDossier,
                Message = $"Le client a indiqué vouloir effectuer un règlement total immédiat.{commentairePart}",
                Origine = ORIGINE_SYSTEME,
                DateEnvoi = DateTime.Now
            });

            _context.HistoriqueActions.Add(new HistoriqueAction
            {
                IdDossier = intention.IdDossier,
                ActionDetail = "Client : intention de règlement total immédiat déclarée.",
                Acteur = ACTEUR_CLIENT,
                DateAction = DateTime.Now
            });

            return Ok();
        }

        private IActionResult HandlePaiementPartiel(IntentionClient intention, DossierRecouvrement dossier, string commentairePart)
        {
            if (!intention.MontantPropose.HasValue || intention.MontantPropose <= 0)
                return BadRequest(new { message = "Un montant proposé valide est requis pour un paiement partiel." });

            if (intention.MontantPropose >= dossier.MontantImpaye)
                return BadRequest(new { message = "Le montant partiel doit être inférieur au montant total impayé. Utilisez 'Règlement total' à la place." });

            _context.Communications.Add(new Communication
            {
                IdDossier = intention.IdDossier,
                Message = $"Le client propose un règlement partiel de {intention.MontantPropose:F3} TND (sur {dossier.MontantImpaye:F3} TND dus).{commentairePart}",
                Origine = ORIGINE_SYSTEME,
                DateEnvoi = DateTime.Now
            });

            _context.HistoriqueActions.Add(new HistoriqueAction
            {
                IdDossier = intention.IdDossier,
                ActionDetail = $"Règlement partiel proposé : {intention.MontantPropose:F3} TND.",
                Acteur = ACTEUR_CLIENT,
                DateAction = DateTime.Now
            });

            return Ok();
        }

        private IActionResult HandlePromessePaiement(IntentionClient intention, DossierRecouvrement dossier, string commentairePart)
        {
            if (!intention.DatePaiementPrevue.HasValue)
                return BadRequest(new { message = "Une date de paiement prévue est requise pour une promesse de paiement." });

            if (intention.DatePaiementPrevue.Value.Date <= DateTime.Today)
                return BadRequest(new { message = "La date de paiement promise doit être dans le futur." });

            _context.Echeances.Add(new Echeance
            {
                IdDossier = dossier.IdDossier,
                Montant = dossier.MontantImpaye,
                DateEcheance = intention.DatePaiementPrevue.Value,
                Statut = AppConstants.EcheanceStatut.Impaye
            });

            _context.Communications.Add(new Communication
            {
                IdDossier = intention.IdDossier,
                Message = $"Le client a promis un paiement pour le {intention.DatePaiementPrevue.Value:dd/MM/yyyy}.{commentairePart}",
                Origine = ORIGINE_SYSTEME,
                DateEnvoi = DateTime.Now
            });

            _context.HistoriqueActions.Add(new HistoriqueAction
            {
                IdDossier = intention.IdDossier,
                ActionDetail = $"Promesse de paiement prévue le {intention.DatePaiementPrevue.Value:dd/MM/yyyy}.",
                Acteur = ACTEUR_CLIENT,
                DateAction = DateTime.Now
            });

            return Ok();
        }

        private OkResult HandleDemandeConsolidation(IntentionClient intention, string commentairePart)
        {
            _context.Communications.Add(new Communication
            {
                IdDossier = intention.IdDossier,
                Message = $"Le client demande une consolidation (restructuration) de sa dette.{commentairePart}",
                Origine = ORIGINE_SYSTEME,
                DateEnvoi = DateTime.Now
            });

            _context.HistoriqueActions.Add(new HistoriqueAction
            {
                IdDossier = intention.IdDossier,
                ActionDetail = "Demande de consolidation/restructuration de dette soumise.",
                Acteur = ACTEUR_CLIENT,
                DateAction = DateTime.Now
            });

            return Ok();
        }

        private OkResult HandleDemandeEcheance(IntentionClient intention, string commentairePart)
        {
            _context.Communications.Add(new Communication
            {
                IdDossier = intention.IdDossier,
                Message = $"Le client demande un échéancier de paiement.{commentairePart}",
                Origine = ORIGINE_SYSTEME,
                DateEnvoi = DateTime.Now
            });

            _context.HistoriqueActions.Add(new HistoriqueAction
            {
                IdDossier = intention.IdDossier,
                ActionDetail = "Demande d'échéancier de paiement soumise.",
                Acteur = ACTEUR_CLIENT,
                DateAction = DateTime.Now
            });

            return Ok();
        }

        private OkResult HandleReclamation(IntentionClient intention, DossierRecouvrement dossier, string commentairePart)
        {
            dossier.StatutDossier = AppConstants.DossierStatut.Contentieux;

            _context.Communications.Add(new Communication
            {
                IdDossier = intention.IdDossier,
                Message = $"Le client a soumis une réclamation. Dossier passé en contentieux.{commentairePart}",
                Origine = ORIGINE_SYSTEME,
                DateEnvoi = DateTime.Now
            });

            _context.HistoriqueActions.Add(new HistoriqueAction
            {
                IdDossier = intention.IdDossier,
                ActionDetail = "Réclamation soumise — dossier passé en contentieux.",
                Acteur = ACTEUR_CLIENT,
                DateAction = DateTime.Now
            });

            return Ok();
        }

        // Logic moved to RecouvrementHelper
    }
}