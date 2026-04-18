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
    /// Contrôleur gérant la vue tabulaire exhaustive "Gestion de liste des clients".
    /// Route de base : /api/ClientList
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ClientListController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ClientListController> _logger;

        public ClientListController(ApplicationDbContext context, ILogger<ClientListController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Renvoie les données à afficher de la table 'Gestion de liste des clients', paramétrables par la pagination et les filtres.
        /// Route API ex: GET http://localhost:5203/api/ClientList/gestion?page=1&pageSize=10&statut=Contentieux
        /// </summary>
        /// <param name="statut">Filtre sélection menu déroulant (Tous, Contentieux, Aimable...)</param>
        /// <param name="agence">Filtre sélection menu déroulant agence (Toutes, Tunis Centre, ...)</param>
        /// <param name="page">Numéro de page Front-End</param>
        /// <param name="pageSize">Taille du tableau front (combien de lignes)</param>
        [HttpGet("gestion")]
        public async Task<ActionResult<ClientListResponseDto>> GetClientsGestion(
            [FromQuery] string statut = "Tous",
            [FromQuery] string agence = "Toutes",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var query = _context.Dossiers
                    .Include(d => d.Client)
                        .ThenInclude(c => c.Agence)
                    .Include(d => d.Echeances)
                    .Where(d => d.Client.Statut != AppConstants.ClientStatut.Archive) // EXCLURE LES ARCHIVES
                    .AsQueryable();

                // ---------------------------------------------------------------------------------
                // BLOC KPI HEADER : Ils sont calculés GLOBALEMENT sans être altérés par la pagination
                // (Conforme aux wireframes UI : le top board montre la globalité parc analytique)
                // ---------------------------------------------------------------------------------
                var totalClients = await _context.Clients.CountAsync(c => c.Statut != AppConstants.ClientStatut.Archive);
                var dossiersActifs = _context.Dossiers.Where(d => d.Client.Statut != AppConstants.ClientStatut.Archive);

                var montantEmprunte = await dossiersActifs.SumAsync(d => d.MontantInitial);
                var contentieux = await dossiersActifs.CountAsync(d => d.StatutDossier == AppConstants.DossierStatut.Contentieux);
                var amiable = await dossiersActifs.CountAsync(d => d.StatutDossier == AppConstants.DossierStatut.Amiable);
                var regularise = await dossiersActifs.CountAsync(d => d.StatutDossier == AppConstants.DossierStatut.Regularise);

                // ---------------------------------------------------------------------------------
                // ALGORITHME DE FILTRAGE : Agit *seulement* sur la Datatable et non les KPI 
                // ---------------------------------------------------------------------------------
                if (!string.IsNullOrEmpty(statut) && statut != "Tous")
                {
                    query = query.Where(d => d.StatutDossier == statut.ToLower());
                }

                if (!string.IsNullOrEmpty(agence) && agence != "Toutes")
                {
                    // L'Agence ne posséde qu'une propriété Ville comme référence locale
                    query = query.Where(d => d.Client.Agence != null && d.Client.Agence.Ville == agence);
                }

                // ---------------------------------------------------------------------------------
                // ALGORITHME DE PAGINATION
                // ---------------------------------------------------------------------------------
                int totalItems = await query.CountAsync();
                int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                // Exécution de la requête avec Skip+Take pour optimisation de la mémoire RAM
                var dossiers = await query
                    .OrderByDescending(d => d.DateCreation)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Mapping métier : transformation des Entities Database vers les DTO (Data Transfer Objects) Front
                var items = dossiers.Select(d => new ClientListItemDto
                {
                    IdDossier = d.IdDossier,
                    Client = $"{d.Client.Nom} {d.Client.Prenom}",
                    Telephone = d.Client.Telephone,
                    Email = d.Client.Email,
                    Agence = d.Client.Agence?.Ville ?? "Inconnue", // Gestion de l'association nulle
                    TypeCredit = d.TypeEmprunt,
                    MontantDu = d.MontantImpaye,
                    Retard = RecouvrementHelper.CalculerJoursRetard(d.Echeances), // Calcul du retard en jours selon les écheances
                    Statut = CapitalizeFirstLetter(d.StatutDossier)
                }).ToList();

                // Retourne la DTO globale contenant les KPI du haut et les LIGNES du tableau en bas
                return Ok(new ClientListResponseDto
                {
                    Kpis = new ClientListKpiDto
                    {
                        TotalClients = totalClients,
                        MontantTotalEmprunte = montantEmprunte,
                        Contentieux = contentieux,
                        Amiable = amiable,
                        Regularise = regularise
                    },
                    Items = items,
                    TotalItems = totalItems,
                    TotalPages = totalPages,
                    CurrentPage = page
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue au rendu de la vue ClientList.");
                return StatusCode(500, new { message = "Erreur de chargement du module clients." });
            }
        }

        // Logic moved to RecouvrementHelper

        /// <summary>
        /// Assistant de formatage UI pour affichage propre. (Ex: "contentieux" devient "Contentieux")
        /// </summary>
        private static string CapitalizeFirstLetter(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;
            return char.ToUpper(str[0]) + str.Substring(1).ToLower();
        }
    }
}
