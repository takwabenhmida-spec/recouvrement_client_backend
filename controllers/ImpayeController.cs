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
    /// Contrôleur métier traitant spécifiquement l'onglet "Gestion des impayés".
    /// Orienté vers le calcul strict de la dimension financière (calcul d'intérêt retard, suivi contentieux).
    /// Route de base : /api/Impaye
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ImpayeController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ImpayeController> _logger;

        public ImpayeController(ApplicationDbContext context, ILogger<ImpayeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Récupère la liste détaillée formatée des clients dans le rouge financièrement + les 4 KPIs récapitulatifs.
        /// Route API ex: GET http://localhost:5203/api/Impaye/gestion?filtre=Avec%20intérêt%20>=90j&page=1
        /// </summary>
        [HttpGet("gestion")]
        public async Task<ActionResult<ImpayeResponseDto>> GetImpayesGestion(
            [FromQuery] string filtre = "Tous",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                // Nous exécutons la récupération pour appliquer les calculs financiers complexes côté RAM Serveur
                // car les fonctions Math ne sont pas toutes traduisibles en requête Linq to SQL native.
                var dossiers = await _context.Dossiers
                    .Include(d => d.Client)
                    .Include(d => d.Echeances)
                    .Where(d => d.Client.Statut != AppConstants.ClientStatut.Archive) // Masquer les clients archivés
                    .ToListAsync();

                // Transforme la structure BD en format orienté Gestion d'Impayé Financier
                var mappedItems = dossiers.Select(d => 
                {
                    var retard = RecouvrementHelper.CalculerJoursRetard(d.Echeances);
                    
                    // LÉGISLATION FINANCIÈRE IMPLÉMENTÉE (Centralisée dans RecouvrementHelper)
                    decimal interets = RecouvrementHelper.CalculerInteretsRetard(d.MontantImpaye, d.TauxInteret, retard);

                    var dejaPaye = d.MontantInitial - d.MontantImpaye;

                    // On retient la date du bascule d'impayé la plus ancienne
                    var premiereEcheance = d.Echeances
                        .Where(e => e.Statut == AppConstants.EcheanceStatut.Impaye)
                        .OrderBy(e => e.DateEcheance)
                        .Select(e => (DateTime?)e.DateEcheance)
                        .FirstOrDefault();

                    return new ImpayeItemDto
                    {
                        IdDossier = d.IdDossier,
                        NomPrenom = $"{d.Client.Nom} {d.Client.Prenom}",
                        DateOctroi = d.DateCreation, // Souvent coïncidente avec la date de l'octroi crédit
                        DateEcheance = premiereEcheance,
                        MontantInitial = d.MontantInitial,
                        DejaPaye = dejaPaye,
                        PrincipalDu = d.MontantImpaye,
                        Frais = d.FraisDossier,
                        Taux = d.TauxInteret,
                        Retard = retard,
                        Interets = Math.Round(interets, 3), // Tolérance Dinars 3 chiffres
                        TotalARegler = Math.Round(d.MontantImpaye + interets + d.FraisDossier, 3) 
                    };
                }).ToList();

                // Application du filtrage selon le sélecteur Front dropdown
                if (filtre == "Avec intérêt >=90j")
                {
                    mappedItems = mappedItems.Where(i => i.Retard >= 90).ToList();
                }
                else if (filtre == "Sans intérêt")
                {
                    mappedItems = mappedItems.Where(i => i.Retard < 90 && i.PrincipalDu > 0).ToList();
                }
                else if (filtre == "Soldé")
                {
                    mappedItems = mappedItems.Where(i => i.PrincipalDu <= 0).ToList();
                }

                // -------------------------------------------------------------
                // Calcul du Tableau des KPIs ("Total impayé", "Intérêts dus..." )
                // Ces valeurs illustrent le portefeuille global dans la base pour éviter des confusions
                // -------------------------------------------------------------
                var totalImpaye = dossiers.Where(d => d.StatutDossier != AppConstants.DossierStatut.Regularise).Sum(d => d.MontantImpaye);
                
                // Recalcul en temps réel de tous les intérêts dépassant 90j au global
                var totalInteretsDus = mappedItems.Where(i => i.Retard >= 90).Sum(i => i.Interets);
                var totalFrais = dossiers.Where(d => d.StatutDossier != AppConstants.DossierStatut.Regularise).Sum(d => d.FraisDossier);
                var dejaRecupere = dossiers.Sum(d => d.MontantInitial - d.MontantImpaye);
                var totalInitial = dossiers.Sum(d => d.MontantInitial);

                var kpis = new ImpayeKpiDto
                {
                    TotalImpaye = totalImpaye,
                    InteretsDus = Math.Round(totalInteretsDus, 2),
                    TotalARecouvrer = Math.Round(totalImpaye + totalInteretsDus + totalFrais, 2),
                    DejaRecupere = dejaRecupere,
                    // Rendement en taux global
                    TauxRecuperation = totalInitial > 0 ? Math.Round((dejaRecupere / totalInitial) * 100, 1) : 0
                };

                // Pagination Memory-side : sécurisation des perfs UI
                int totalItems = mappedItems.Count;
                int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                var paginatedItems = mappedItems
                    .OrderByDescending(i => i.TotalARegler) // Les dettes majeures d'abord en haut d'affichage
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return Ok(new ImpayeResponseDto
                {
                    Kpis = kpis,
                    Items = paginatedItems,
                    TotalItems = totalItems,
                    TotalPages = totalPages,
                    CurrentPage = page
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur critique de la route Impaye.");
                return StatusCode(500, new { message = "L'API a rencontré un dysfonctionnement lors des calculs financiers." });
            }
        }

        // Logic moved to RecouvrementHelper
    }
}
