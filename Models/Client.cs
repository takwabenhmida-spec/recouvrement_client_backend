using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using System.Text.Json.Serialization;
namespace RecouvrementAPI.Models
{
    [Table("client")]
    public class Client
    {
        [Key]
        [Column("id_client")]
        [JsonRequired]
        public int IdClient { get; set; }

        [Column("id_agence")]
        [JsonRequired]
        public int IdAgence { get; set; }

        [Required]
        [Column("nom")]
        public string Nom { get; set; } = null!;

        [Required]
        [Column("prenom")]
        public string Prenom { get; set; } = null!;

        [Column("telephone")]
        public string Telephone { get; set; } = null!;

        [Column("email")]
        public string Email { get; set; } = null!;

        [Column("token_acces")]
        public string TokenAcces { get; set; } = null!;

        [Column("cin")]
        public string CIN { get; set; } = null!;

        [Column("adresse")]
        public string Adresse { get; set; } = null!;

        [Column("statut")]
        public string Statut { get; set; } = null!;

        [Column("token_expire_le")]
        public DateTime? TokenExpireLe { get; set; }

        // Navigation
        public Agence Agence { get; set; } = null!;

        public ICollection<DossierRecouvrement> Dossiers { get; set; } = new List<DossierRecouvrement>();
    }
}