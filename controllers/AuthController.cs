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
    /// Authentification simplifiée : JWT 8h uniquement, pas de Refresh Token.
    /// Route de base : /api/Auth
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(ApplicationDbContext context, IConfiguration configuration, ILogger<AuthController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        // ─────────────────────────────────────────────────────────────────
        // POST /api/Auth/login
        // ─────────────────────────────────────────────────────────────────
        /// <summary>
        /// Connexion d'un agent ou admin. Retourne un Access Token JWT valable 8 heures.
        /// À l'expiration, l'agent doit se reconnecter avec ses identifiants.
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            try
            {
                _logger.LogInformation($"Tentative de connexion : {request.Email}");

                // 1. Recherche de l'agent
                var agent = await _context.UtilisateursBack
                    .FirstOrDefaultAsync(u => u.Email == request.Email);

                if (agent == null)
                {
                    _logger.LogWarning($"Connexion échouée : utilisateur introuvable ({request.Email})");
                    return Unauthorized(new { message = "Email ou mot de passe incorrect." });
                }

                // 2. Vérification du mot de passe
                bool isPasswordValid = false;
                if (agent.MotDePasse.StartsWith("$2a$") || agent.MotDePasse.StartsWith("$2y$") || agent.MotDePasse.StartsWith("$2b$"))
                    isPasswordValid = BCrypt.Net.BCrypt.Verify(request.MotDePasse, agent.MotDePasse);
                else
                    isPasswordValid = false; // Suppression de la faille de sécurité (comparaison en clair)

                if (!isPasswordValid)
                {
                    _logger.LogWarning($"Connexion échouée : mot de passe invalide ({request.Email})");
                    return Unauthorized(new { message = "Email ou mot de passe incorrect." });
                }

                // 3. Mise à jour de la dernière connexion
                agent.DerniereConnexion = DateTime.UtcNow;

                // 4. Génération de l'Access Token JWT (8 heures)
                var accessTokenExpire = DateTime.UtcNow.AddHours(8);
                var accessToken = GenerateAccessToken(agent, accessTokenExpire);

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Connexion réussie : agent {agent.IdAgent} ({agent.Nom})");

                return Ok(new LoginResponseDto
                {
                    Token       = accessToken,
                    TokenExpire = accessTokenExpire,
                    AgentId     = agent.IdAgent,
                    Nom         = agent.Nom,
                    Prenom      = agent.Prenom,
                    Role        = agent.Role
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue lors de la connexion.");
                return StatusCode(500, new { message = "Erreur interne du serveur." });
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // POST /api/Auth/seed-admin  (utilitaire PFE uniquement)
        // ─────────────────────────────────────────────────────────────────
        [HttpPost("seed-admin")]
        public async Task<IActionResult> SeedAdmin()
        {
            try
            {
                if (await _context.UtilisateursBack.AnyAsync(u => u.Email == "admin@stb.tn"))
                    return BadRequest(new { message = "Un administrateur existe déjà dans la base de données." });

                var newAdmin = new UtilisateurBack
                {
                    Nom        = "Admin",
                    Prenom     = "STB",
                    Email      = "admin@stb.tn",
                    MotDePasse = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString().Substring(0, 8)), // Génération sécurisée / aléatoire
                    Role       = "Admin"
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

        // ─────────────────────────────────────────────────────────────────
        // MÉTHODE PRIVÉE : Génération du token JWT
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Génère un Access Token JWT signé pour un agent donné.
        /// </summary>
        private string GenerateAccessToken(UtilisateurBack agent, DateTime expiration)
        {
            var jwtKey    = _configuration["Jwt:Key"];
            var jwtIssuer = _configuration["Jwt:Issuer"];

            if (string.IsNullOrEmpty(jwtKey))
                throw new InvalidOperationException("Clé secrète JWT non configurée.");

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(jwtKey);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.NameIdentifier, agent.IdAgent.ToString()),
                    new Claim(ClaimTypes.Email, agent.Email),
                    new Claim(ClaimTypes.Role, agent.Role ?? "Utilisateur")
                }),
                Expires            = expiration,
                Issuer             = jwtIssuer,
                SigningCredentials  = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
