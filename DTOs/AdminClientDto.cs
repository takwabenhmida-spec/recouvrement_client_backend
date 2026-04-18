using System.ComponentModel.DataAnnotations;

namespace RecouvrementAPI.DTOs
{
    public class CreateClientDto
    {
        [Required]
        public string Nom { get; set; }
        [Required]
        public string Prenom { get; set; }
        [Required]
        public string CIN { get; set; }
        [Required]
        public string Adresse { get; set; }
        [EmailAddress]
        public string Email { get; set; }
        [Required]
        public string Telephone { get; set; }
        
        public int? IdAgence { get; set; }

        // Dossier initial optionnel lors de la création du client
        public CreateDossierDto PremierDossier { get; set; }
    }

    public class CreateDossierDto
    {
        [Required]
        public decimal? MontantInitial { get; set; }
        [Required]
        public string TypeEmprunt { get; set; } // Consommation, Immobilier, Auto
        [Required]
        public decimal? TauxInteret { get; set; }
        [Required]
        public string StatutDossier { get; set; } // aimable, contentieux
    }

    public class UpdateClientDto
    {
        public string Nom { get; set; }
        public string Prenom { get; set; }
        public string Adresse { get; set; }
        public string Email { get; set; }
        public string Telephone { get; set; }
        public int? IdAgence { get; set; }
    }
}
