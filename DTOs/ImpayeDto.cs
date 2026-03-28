using System;
using System.Collections.Generic;

namespace RecouvrementAPI.DTOs
{
    public class ImpayeKpiDto
    {
        public decimal TotalImpaye { get; set; }
        public decimal InteretsDus { get; set; }
        public decimal TotalARecouvrer { get; set; }
        public decimal DejaRecupere { get; set; }
        public decimal TauxRecuperation { get; set; }
    }

    public class ImpayeItemDto
    {
        public int IdDossier { get; set; }
        public string NomPrenom { get; set; }
        public string RefDossier => $"#{DateTime.Now.Year}-{IdDossier.ToString("D3")}";
        public DateTime DateOctroi { get; set; }
        public DateTime? DateEcheance { get; set; }
        public decimal MontantInitial { get; set; }
        public decimal DejaPaye { get; set; }
        public decimal PrincipalDu { get; set; }
        public decimal Frais { get; set; }
        public decimal Taux { get; set; }
        public int Retard { get; set; }
        public decimal Interets { get; set; }
        public decimal TotalARegler { get; set; }
    }

    public class ImpayeResponseDto
    {
        public ImpayeKpiDto Kpis { get; set; }
        public List<ImpayeItemDto> Items { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
    }
}
