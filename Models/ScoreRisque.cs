using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RecouvrementAPI.Models
{
    // Table score_risque
    [Table("score_risque")]
    public class ScoreRisque
    {
        [Key]
        [Column("id_score")]
        public int IdScore { get; set; }

        [Column("id_dossier")]
        public int IdDossier { get; set; }

        [Column("valeur")]
        public decimal Valeur { get; set; }

        [Column("points_retard")]
        public int PointsRetard { get; set; }

        [Column("points_historique")]
        public int PointsHistorique { get; set; }

        [Column("points_garantie")]
        public int PointsGarantie { get; set; }

        [Column("points_intention")]
        public int PointsIntention { get; set; }

        [Column("niveau")]
        [MaxLength(20)]
        public string Niveau { get; set; } // Faible, Moyen, Élevé

        [Column("date_calcul")]
        public DateTime DateCalcul { get; set; }

        public DossierRecouvrement Dossier { get; set; }
    }
}