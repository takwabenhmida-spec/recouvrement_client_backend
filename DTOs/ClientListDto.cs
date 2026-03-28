using System.Collections.Generic;

namespace RecouvrementAPI.DTOs
{
    public class ClientListKpiDto
    {
        public int TotalClients { get; set; }
        public decimal MontantTotalEmprunte { get; set; }
        public int Contentieux { get; set; }
        public int Amiable { get; set; }
        public int Regularise { get; set; }
    }

    public class ClientListItemDto
    {
        public int IdDossier { get; set; }
        public string NumDossier => $"#{DateTime.Now.Year}-{IdDossier.ToString("D3")}"; 
        public string Client { get; set; }
        public string Telephone { get; set; }
        public string Email { get; set; }
        public string Agence { get; set; }
        public string TypeCredit { get; set; }
        public decimal MontantDu { get; set; }
        public int Retard { get; set; }
        public string Statut { get; set; }
    }

    public class ClientListResponseDto
    {
        public ClientListKpiDto Kpis { get; set; }
        public List<ClientListItemDto> Items { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
    }
}
