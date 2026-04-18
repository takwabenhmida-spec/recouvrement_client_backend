using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace RecouvrementAPI.Models
{
    // Cette classe représente la table "agence" dans MySQL
    [Table("agence")]
    public class Agence
    {
        // Clé primaire unique pour chaque agence
        [Key]
        [Column("id_agence")]
        [JsonRequired]
        public int IdAgence { get; set; }

        
        // Ville où se trouve l'agence
        [Column("ville")]
        public string Ville { get; set; }

        // Relation : une agence peut avoir plusieurs clients
        public ICollection<Client> Clients { get; set; }
    }
}