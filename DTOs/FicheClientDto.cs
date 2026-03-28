using System;
using System.Collections.Generic;

namespace RecouvrementAPI.DTOs
{
    public class FicheClientInfoDto
    {
        public string NomComplet { get; set; }
        public string ClientDepuis { get; set; }
        public string Agence { get; set; }
        public string CIN { get; set; }
        public string Telephone { get; set; }
        public string Email { get; set; }
        public string Adresse { get; set; }
        public string PireStatut { get; set; } // ex: Contentieux
        public decimal PireScore { get; set; }
    }

    public class FicheDossierDto
    {
        public int IdDossier { get; set; }
        public string NumDossier => $"#DOS-{IdDossier.ToString("D4")}";
        public string TypeCredit { get; set; }
        public decimal MontantInitial { get; set; }
        public decimal MontantDu { get; set; }
        public decimal Frais { get; set; }
        public decimal Taux { get; set; }
        public int RetardJours { get; set; }
        public string Statut { get; set; }
    }

    public class FicheIntentionDto
    {
        public string Date { get; set; }
        public string Intention { get; set; }
        public string Commentaire { get; set; }
        public string Statut { get; set; }
    }

    public class FicheTokenStateDto
    {
        public string EnvoyéLe { get; set; }
        public string Canal { get; set; }
        public string FormulaireSoumis { get; set; } // Oui / Non
        public string LienUrl { get; set; }
    }

    public class FicheHistoriqueDto
    {
        public string Action { get; set; }
        public string Date { get; set; }
        public string Acteur { get; set; }
    }

    public class FicheClientResponseDto
    {
        public FicheClientInfoDto Infos { get; set; }
        public ScoringDetailsDto IA { get; set; } // Réutilise le DTO IA
        public List<FicheDossierDto> Dossiers { get; set; }
        public List<FicheIntentionDto> Intentions { get; set; }
        public FicheTokenStateDto TokenInfos { get; set; }
        public List<FicheHistoriqueDto> Historiques { get; set; }
    }
}
