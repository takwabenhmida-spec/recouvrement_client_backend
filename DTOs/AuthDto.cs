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

    public class LoginResponseDto
    {
        public string Token { get; set; }
        public int AgentId { get; set; }
        public string Nom { get; set; }
        public string Prenom { get; set; }
        public string Role { get; set; }
    }
}
