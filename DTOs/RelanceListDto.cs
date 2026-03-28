using System;
using System.Collections.Generic;

namespace RecouvrementAPI.DTOs
{
    public class RelanceKpiDto
    {
        public int TotalEnvoyees { get; set; }
        public int EnAttenteReponse { get; set; }
        public int FormulairesSoumis { get; set; }
        public decimal TauxReponse { get; set; }
    }

    public class RelanceCanalStatDto
    {
        public int Appels { get; set; }
        public int AppelsDecroches { get; set; }
        public int AppelsNonJoignables { get; set; }

        public int SmsEnvoyes { get; set; }
        public int SmsRepondus { get; set; }
        public int SmsEnAttente { get; set; }

        public int EmailsEnvoyes { get; set; }
        public int EmailsRepondus { get; set; }
        public int EmailsEnAttente { get; set; }
    }

    public class RelanceItemDto
    {
        public int IdRelance { get; set; }
        public int IdDossier { get; set; }
        public string NumDossier => $"#{DateTime.Now.Year}-{IdDossier.ToString("D3")}";
        public string Client { get; set; }
        public string Telephone { get; set; }
        public string Email { get; set; }
        public string Canal { get; set; } // Moyen
        public string Token { get; set; } // substr(Client.TokenAcces)
        public string Statut { get; set; }
        public string Reponse { get; set; } 
    }

    public class RelanceDashboardResponseDto
    {
        public RelanceKpiDto Kpis { get; set; }
        public RelanceCanalStatDto Canaux { get; set; }
        public List<RelanceItemDto> Items { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
    }
}
