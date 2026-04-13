using System;
using System.Collections.Generic;

namespace RecouvrementAPI.DTOs
{
    public class IntentionKpiDto
    {
        public int TotalRecues { get; set; }
        public int NonTraitees { get; set; }
        public int PaiementImmediat { get; set; }
        public int Reclamations { get; set; }
    }

    public class IntentionItemDto
    {
        public int IdIntention { get; set; }
        public int IdDossier { get; set; }
        public string Client { get; set; }
        public string RefDossier => $"#{DateTime.Now.Year}-{IdDossier.ToString("D3")}";
        public DateTime DateSoumission { get; set; }
        public decimal MontantDu { get; set; }
        public string Agence { get; set; }
        public string TypeCredit { get; set; }
        public string CommentaireClient { get; set; }
        public string Canal { get; set; }
        public int Retard { get; set; }
        public string Statut { get; set; }
        public string TypeIntention { get; set; }
    }

    public class IntentionDecisionDto
    {
        public string Decision { get; set; } // "Accepter" ou "Refuser"
    }

    public class IntentionDashboardResponseDto
    {
        public IntentionKpiDto Kpis { get; set; }
        public List<IntentionItemDto> Items { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
    }
}
