using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecouvrementAPI.Data;
using RecouvrementAPI.DTOs;
using System.Globalization;

namespace RecouvrementAPI.Controllers
{
    /// <summary>
    /// Contrôleur gérant les statistiques globales affichées sur "Tableau de bord".
    /// Route de base : /api/Dashboard
    /// Note : Toutes les routes nécessitent un Bearer Token ([Authorize]).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Bloque l'accès si l'agent n'envoie pas le token JWT reçu au login
    public class DashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(ApplicationDbContext context, ILogger<DashboardController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Récupère les 4 KPIs principaux en haut du Dashboard.
        /// Route API : GET http://localhost:5203/api/Dashboard/kpi
        /// </summary>
        /// <returns>Objet JSON contenant les statistiques : Dossiers actifs, Montant impayé, Taux réponse, Intentions</returns>
        [HttpGet("kpi")]
        public async Task<ActionResult<DashboardKpiDto>> GetKpis()
        {
            try
            {
                // Un dossier est "actif" s'il n'est pas clôturé et que le client n'est pas archivé
                var dossiersActifsQuery = _context.Dossiers
                    .Include(d => d.Client)
                    .Where(d => d.StatutDossier != "regularise" && d.Client.Statut != "Archivé");

                var dossiersActifs = await dossiersActifsQuery.CountAsync();
                
                // Montant cumulé des impayés pour les clients non archivés
                var montantTotalImpaye = await dossiersActifsQuery.SumAsync(d => d.MontantImpaye);
                
                var totalRelances = await _context.Relances.CountAsync();
                var relancesRepondues = await _context.Relances.CountAsync(r => r.Statut == "repondu");
                
                // Calcul du taux en pourcentage
                decimal tauxReponse = totalRelances > 0 ? (decimal)relancesRepondues / totalRelances * 100 : 0;
                
                // Totaux des intentions (formulaires clients)
                var intentions = await _context.Intentions.CountAsync();

                return Ok(new DashboardKpiDto
                {
                    DossiersActifs = dossiersActifs,
                    MontantTotalImpaye = montantTotalImpaye,
                    TauxReponse = Math.Round(tauxReponse, 1),
                    IntentionsATraiter = intentions
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur au calcul des KPIs Dashboard.");
                return StatusCode(500, new { message = "Impossible de générer les KPIs globaux." });
            }
        }

        /// <summary>
        /// Récupère la liste des dossiers déclarés comme "Prioritaires" via l'IA de scoring.
        /// Route API : GET http://localhost:5203/api/Dashboard/prioritaires?filtre=Tous
        /// Paramètre [filtre] optionnel : "Tous", "Amiable", "Contentieux"...
        /// </summary>
        [HttpGet("prioritaires")]
        public async Task<ActionResult<IEnumerable<PriorityDossierDto>>> GetPriorityDossiers([FromQuery] string filtre = "Tous")
        {
            try
            {
                // Jointure entre Dossiers, Clients et ScoresRisque pour ramener tout en une fois
                var query = _context.Dossiers
                    .Include(d => d.Client)
                    .Include(d => d.ScoresRisque)
                    .Where(d => d.StatutDossier != "regularise" && d.Client.Statut != "Archivé")
                    .AsQueryable();

                var dossiersWithScores = await query.Select(d => new
                {
                    Dossier = d,
                    Client = d.Client,
                    // On prend la valeur de scoring la plus récente calculée par l'algorithme IA
                    Score = d.ScoresRisque.OrderByDescending(s => s.DateCalcul).FirstOrDefault()
                }).ToListAsync();

                // Cartographie des règles métiers de risque associées au score IA
                var result = dossiersWithScores.Select(x => 
                {
                    var scoreVal = x.Score?.Valeur ?? 0;
                    
                    // Règle de catégorisation du risque basée sur les limites requises (ajustable pr la soutenance)
                    string risque = "Faible";
                    if (scoreVal >= 80) risque = "Élevé";
                    else if (scoreVal >= 50) risque = "Moyen";

                    return new PriorityDossierDto
                    {
                        IdDossier = x.Dossier.IdDossier,
                        Client = $"{x.Client.Nom} {x.Client.Prenom?.Substring(0, 1)}.",
                        TypeCredit = x.Dossier.TypeEmprunt,
                        Montant = x.Dossier.MontantImpaye,
                        Score = scoreVal,
                        Risque = risque
                    };
                });

                // Application d'un filtre externe si paramètre "filtre" passé
                if (!string.IsNullOrEmpty(filtre) && filtre != "Tous")
                {
                    result = result.Where(r => r.Risque == filtre);
                }

                // Pour un dashboard, on ne renvoie que les 5 pires (ceux nécessitant une action prioritaire immédiate)
                return Ok(result.OrderByDescending(r => r.Score).Take(5).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur au chargement des dossiers prioritaires.");
                return StatusCode(500, new { message = "Erreur fatale lors du chargement des dossiers scorés." });
            }
        }

        /// <summary>
        /// Prépare la structure JSON attendue par les librairies de graphiques Front-End (Chart.js / ng2-charts).
        /// Route API : GET http://localhost:5203/api/Dashboard/charts
        /// </summary>
        [HttpGet("charts")]
        public async Task<ActionResult<DashboardChartsDto>> GetCharts()
        {
            try
            {
                // Graphique en Donut / Camembert : Statut global des dossiers (Amiable, Contentieux, ...)
                var statutsData = await _context.Dossiers
                    .GroupBy(d => d.StatutDossier)
                    .Select(g => new StatutDossierStatDto
                    {
                        Statut = string.IsNullOrEmpty(g.Key) ? "Non défini" : g.Key,
                        Count = g.Count()
                    }).ToListAsync();

                // Graphique en Bâtons : Évolution mensuelle sur l'année en cours
                var currentYear = DateTime.Now.Year;
                var paiements = await _context.HistoriquePaiements
                    .Where(p => p.DatePaiement.Year == currentYear)
                    .ToListAsync();

                // Agrégation des montants récupérés (paiements) par mois d'année
                var paiementsGroupes = paiements
                    .GroupBy(p => p.DatePaiement.Month)
                    .OrderBy(g => g.Key)
                    .Select(g => new EvolutionMensuelleDto
                    {
                        // Convertit le n° de mois en chaîne compréhensible (ex: "janv", "févr")
                        Mois = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(g.Key),
                        Recouvrement = g.Sum(p => p.MontantPaye)
                    }).ToList();

                return Ok(new DashboardChartsDto
                {
                    StatutsDossiers = statutsData,
                    EvolutionMensuelle = paiementsGroupes
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur à la génération du Chart JSON.");
                return StatusCode(500, new { message = "Erreur de formatage de graphiques." });
            }
        }
    }
}
