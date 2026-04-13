using System.ComponentModel.DataAnnotations;

namespace RecouvrementAPI.DTOs
{
    public class LoginRequestDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string MotDePasse { get; set; }
    }

    /// <summary>
    /// Réponse retournée après une connexion réussie.
    /// Token JWT valable 8h — à l'expiration, l'agent doit se reconnecter.
    /// </summary>
    public class LoginResponseDto
    {
        public string Token { get; set; }        // JWT 8h
        public DateTime TokenExpire { get; set; } // Date/heure d'expiration
        public int AgentId { get; set; }
        public string Nom { get; set; }
        public string Prenom { get; set; }
        public string Role { get; set; }
    }
}
