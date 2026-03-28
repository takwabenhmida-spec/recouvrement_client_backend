using System;
using System.Collections.Generic;

namespace RecouvrementAPI.DTOs
{
    public class ScoringKpiDto
    {
        public int DossiersScores { get; set; }
        public int RisqueEleve { get; set; }
        public int RisqueMoyen { get; set; }
        public int RisqueFaible { get; set; }
    }

    public class ScoringItemDto
    {
        public int IdDossier { get; set; }
        public string Client { get; set; }
        public string RefDossier => $"#DOS-{IdDossier.ToString("D4")}";
        public string RetardTexte { get; set; }
        public int PointsRetard { get; set; }
        public int PointsHistorique { get; set; }
        public int PointsGarantie { get; set; }
        public int PointsIntention { get; set; }
        public decimal ScoreTotal { get; set; }
        public string Niveau { get; set; }
    }

    public class ScoringDetailsDto
    {
        public string ClientNom { get; set; }
        public decimal ScoreTotal { get; set; }
        public int ConfianceIa { get; set; } // % de confiance de l'algorithme
        
        public string DetailRetard { get; set; }
        public int PtsRetard { get; set; }
        
        public string DetailHistorique { get; set; }
        public int PtsHistorique { get; set; }
        
        public string DetailGarantie { get; set; }
        public int PtsGarantie { get; set; }
        
        public string DetailIntention { get; set; }
        public int PtsIntention { get; set; }
        
        public string Recommandation { get; set; }
        public string DateCalcul { get; set; }
    }

    public class ScoringDashboardResponseDto
    {
        public ScoringKpiDto Kpis { get; set; }
        public List<ScoringItemDto> Items { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
        public ScoringDetailsDto DetailActif { get; set; } // Pour le panneau latéral droit de la maquette
    }
}
