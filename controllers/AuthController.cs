using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RecouvrementAPI.Data;
using RecouvrementAPI.DTOs;
using RecouvrementAPI.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace RecouvrementAPI.Controllers
{
    /// <summary>
    /// Contrôleur gérant l'authentification des agents et administrateurs du back-office.
    /// Route de base de ce contrôleur : /api/Auth
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        // Injection des dépendances (Base de données, Configuration appsettings.json, Logger)
        public AuthController(ApplicationDbContext context, IConfiguration configuration, ILogger<AuthController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Point de terminaison pour la connexion d'un agent.
        /// Route API externe : POST http://localhost:5203/api/Auth/login
        /// Rôle : Valider les credentials et retourner un Token JWT.
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            try
            {
                _logger.LogInformation($"Tentative de connexion pour l'utilisateur : {request.Email}");

                // 1. Recherche de l'utilisateur par son adresse email
                var agent = await _context.UtilisateursBack
                    .FirstOrDefaultAsync(u => u.Email == request.Email);

                if (agent == null)
                {
                    _logger.LogWarning($"Connexion échouée : Utilisateur introuvable ({request.Email})");
                    return Unauthorized(new { message = "Email ou mot de passe incorrect." });
                }

                // 2. Vérification sécurisée du mot de passe
                bool isPasswordValid = false;
                
                // Si la chaîne commence par $2, c'est un format de hash BCrypt standard
                if (agent.MotDePasse.StartsWith("$2a$") || agent.MotDePasse.StartsWith("$2y$") || agent.MotDePasse.StartsWith("$2b$"))
                {
                    isPasswordValid = BCrypt.Net.BCrypt.Verify(request.MotDePasse, agent.MotDePasse);
                }
                else
                {
                    // Fallback (Tolérance PFE) pour les mots de passe insérés manuellement en clair en BDD
                    isPasswordValid = (agent.MotDePasse == request.MotDePasse);
                }

                if (!isPasswordValid)
                {
                    _logger.LogWarning($"Connexion échouée : Mot de passe invalide ({request.Email})");
                    return Unauthorized(new { message = "Email ou mot de passe incorrect." });
                }

                // 3. Récupération des clés JWT depuis appsettings.json
                var jwtKey = _configuration["Jwt:Key"];
                var jwtIssuer = _configuration["Jwt:Issuer"];

                if (string.IsNullOrEmpty(jwtKey))
                {
                    _logger.LogError("Erreur Critique : La clé secrète JWT n'est pas configurée côté serveur.");
                    return StatusCode(500, new { message = "Erreur interne du serveur de configuration." });
                }

                // 4. Création du Token JWT
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(jwtKey);
                
                // Le payload du token encapsule l'identité (ID, Email, Rôle)
                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new Claim[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, agent.IdAgent.ToString()),
                        new Claim(ClaimTypes.Email, agent.Email),
                        new Claim(ClaimTypes.Role, agent.Role ?? "Utilisateur")
                    }),
                    Expires = DateTime.UtcNow.AddHours(8), // Le token expire dans 8 heures
                    Issuer = jwtIssuer,
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                };

                var token = tokenHandler.CreateToken(tokenDescriptor);
                var tokenString = tokenHandler.WriteToken(token);

                _logger.LogInformation($"Connexion réussie pour l'agent {agent.IdAgent} ({agent.Nom})");

                // 5. Réponse HTTP 200 OK avec le DTO adéquat pour Angular
                return Ok(new LoginResponseDto
                {
                    Token = tokenString,
                    AgentId = agent.IdAgent,
                    Nom = agent.Nom,
                    Prenom = agent.Prenom,
                    Role = agent.Role
                });
            }
            catch (Exception ex)
            {
                // Gestion professionnelle des exceptions : évite de crasher l'API
                _logger.LogError(ex, "Une erreur inattendue s'est produite lors de la connexion.");
                return StatusCode(500, new { message = "Une erreur interne s'est produite. Veuillez contacter l'administrateur." });
            }
        }

        /// <summary>
        /// Endpoint utilitaire pour créer rapidement un 1er accès Administrateur dans MySQL.
        /// Route API externe : POST http://localhost:5203/api/Auth/seed-admin
        /// (À retirer lors du déploiement en production ! Utile juste pour le jury/démo de PFE)
        /// </summary>
        [HttpPost("seed-admin")]
        public async Task<IActionResult> SeedAdmin()
        {
            try
            {
                if (await _context.UtilisateursBack.AnyAsync(u => u.Email == "admin@stb.tn"))
                {
                    return BadRequest(new { message = "Un administrateur existe déjà dans la base de données." });
                }

                var newAdmin = new UtilisateurBack
                {
                     Nom = "Admin",
                     Prenom = "STB",
                     Email = "admin@stb.tn",
                     MotDePasse = BCrypt.Net.BCrypt.HashPassword("admin123"), // Mdp haché sécurisé
                     Role = "Admin"
                };

                _context.UtilisateursBack.Add(newAdmin);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Agent administrateur créé avec succès. (Email: admin@stb.tn | Password: admin123)" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la génération de l'Admin par défaut.");
                return StatusCode(500, new { message = "Erreur interne lors de l'insertion." });
            }
        }
    }
}
