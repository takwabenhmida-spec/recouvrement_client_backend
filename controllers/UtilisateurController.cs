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
    /// Contrôleur gérant la "Gestion des utilisateurs" configurés en back-office (Administrateurs et Agents).
    /// Route API de base : /api/Utilisateur
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Optionnellement, on restreint l'accès à l'Administrateur uniquement [Authorize(Roles = "Admin")]
    public class UtilisateurController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UtilisateurController> _logger;

        public UtilisateurController(ApplicationDbContext context, ILogger<UtilisateurController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Liste tous les utilisateurs enregistrés avec infos complètes et pagination.
        /// Route externe API : GET http://localhost:5203/api/Utilisateur/gestion
        /// </summary>
        [HttpGet("gestion")]
        public async Task<ActionResult<UtilisateurListResponseDto>> GetUtilisateurs(
            [FromQuery] string agence = "Toutes",
            [FromQuery] string role = "Tous",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var query = _context.UtilisateursBack
                    .Include(u => u.Agence)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(agence) && agence != "Toutes")
                {
                    query = query.Where(u => u.Agence != null && u.Agence.Ville == agence);
                }
                
                if (!string.IsNullOrEmpty(role) && role != "Tous")
                {
                    query = query.Where(u => u.Role == role);
                }

                int totalItems = await query.CountAsync();
                int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                var users = await query
                    .OrderBy(u => u.Nom)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var items = users.Select(u => new UtilisateurItemDto
                {
                    IdAgent = u.IdAgent,
                    NomComplet = $"{u.Nom} {u.Prenom}",
                    Email = u.Email,
                    Telephone = u.Telephone ?? "Non renseigné",
                    Role = u.Role,
                    IdAgence = u.IdAgence,
                    Agence = u.Agence?.Ville ?? "Siège",
                    DerniereConnexion = FormatDerniereConnexion(u.DerniereConnexion),
                    Statut = AppConstants.UserStatut.Active
                }).ToList();

                return Ok(new UtilisateurListResponseDto
                {
                    Items = items,
                    TotalItems = totalItems,
                    TotalPages = totalPages,
                    CurrentPage = page
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur au chargement de la liste des utilisateurs.");
                return StatusCode(500, new { message = "Impossible d'accéder à la liste du personnel." });
            }
        }

        /// <summary>
        /// Ajout manuel d'un nouvel Agent/Admin depuis le panel.
        /// Route externe API : POST http://localhost:5203/api/Utilisateur
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateUtilisateur([FromBody] CreateUtilisateurDto dto)
        {
            try
            {
                if (await _context.UtilisateursBack.AnyAsync(u => u.Email == dto.Email))
                    return BadRequest(new { message = "L'adresse email est déjà utilisée." });

                var nouveau = new UtilisateurBack
                {
                    Nom = dto.Nom,
                    Prenom = dto.Prenom,
                    Email = dto.Email,
                    Telephone = dto.Telephone,
                    MotDePasse = BCrypt.Net.BCrypt.HashPassword(dto.MotDePasse), // Sécurité oblige
                    Role = dto.Role,
                    IdAgence = dto.IdAgence,
                    Statut = AppConstants.UserStatut.Active
                };

                _context.UtilisateursBack.Add(nouveau);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Utilisateur créé avec succès." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création utilisateur.");
                return StatusCode(500, new { message = "Impossible d'insérer l'utilisateur." });
            }
        }

        /// <summary>
        /// Rend inactif (suppression logique) ou réactive un compte agent.
        /// Route externe API : PUT http://localhost:5203/api/Utilisateur/{id}/statut
        /// </summary>
        [HttpPut("{id}/statut")]
        public async Task<IActionResult> ToggleStatut(int id)
        {
            try
            {
                var user = await _context.UtilisateursBack.FindAsync(id);
                if (user == null) return NotFound(new { message = "Agent introuvable." });

                user.Statut = user.Statut == AppConstants.UserStatut.Active ? AppConstants.UserStatut.Inactive : AppConstants.UserStatut.Active;
                await _context.SaveChangesAsync();

                return Ok(new { message = $"Le compte agent est désormais {user.Statut}." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur statut agent.");
                return StatusCode(500, new { message = "La mise à jour du statut a échouée." });
            }
        }

        /// <summary>
        /// Edite les droits (Rôle), Agence ou informations personnelles d'un Agent.
        /// Route externe API : PUT http://localhost:5203/api/Utilisateur/{id}
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUtilisateur(int id, [FromBody] UpdateUtilisateurDto dto)
        {
            try
            {
                var user = await _context.UtilisateursBack.FindAsync(id);
                if (user == null) return NotFound(new { message = "Agent introuvable." });

                if (!string.IsNullOrEmpty(dto.Nom)) user.Nom = dto.Nom;
                if (!string.IsNullOrEmpty(dto.Prenom)) user.Prenom = dto.Prenom;
                if (!string.IsNullOrEmpty(dto.Telephone)) user.Telephone = dto.Telephone;
                if (!string.IsNullOrEmpty(dto.Role)) user.Role = dto.Role;
                user.IdAgence = dto.IdAgence; // Peut être null si on le ramène au siège

                await _context.SaveChangesAsync();

                return Ok(new { message = "Modifications enregistrées avec succès." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur d'édition agent.");
                return StatusCode(500, new { message = "La modification a échouée." });
            }
        }

        // Utilitaire pour afficher "Aujourd'hui 09:30" ou "Hier 17:00" sur la plateforme Angular
        private static string FormatDerniereConnexion(DateTime? dateConnexion)
        {
            if (!dateConnexion.HasValue) return "Jamais";
            
            var nbJours = (DateTime.Now.Date - dateConnexion.Value.Date).Days;
            var heure = dateConnexion.Value.ToString("HH:mm");

            return nbJours switch
            {
                0 => $"Aujourd'hui {heure}",
                1 => $"Hier {heure}",
                _ => $"Il y a {nbJours} jours"
            };
        }
    }
}
