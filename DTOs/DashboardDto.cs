using System.Collections.Generic;

namespace RecouvrementAPI.DTOs
{
    public class DashboardKpiDto
    {
        public int DossiersActifs { get; set; }
        public decimal MontantTotalImpaye { get; set; }
        public decimal TauxReponse { get; set; }
        public int IntentionsATraiter { get; set; }
    }

    public class PriorityDossierDto
    {
        public string Client { get; set; }
        public string TypeCredit { get; set; }
        public decimal Montant { get; set; }
        public decimal Score { get; set; }
        public string Risque { get; set; }
        public int IdDossier { get; set; } 
    }

    public class StatutDossierStatDto
    {
        public string Statut { get; set; }
        public int Count { get; set; }
    }

    public class EvolutionMensuelleDto
    {
        public string Mois { get; set; }
        public decimal Recouvrement { get; set; }
    }

    public class DashboardChartsDto
    {
        public List<EvolutionMensuelleDto> EvolutionMensuelle { get; set; }
        public List<StatutDossierStatDto> StatutsDossiers { get; set; }
    }
}
