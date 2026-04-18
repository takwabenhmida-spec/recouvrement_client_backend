using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RecouvrementAPI.Models
{
    [Table("dossier_recouvrement")]
    public class DossierRecouvrement
    {
        [Key]
        [Column("id_dossier")]
        [JsonRequired]
        public int IdDossier { get; set; } // Clé primaire

        [Column("id_client")]
        [JsonRequired]
        public int IdClient { get; set; } // Clé étrangère vers le client

        [Column("montant_initial")]
        [JsonRequired]
        public decimal MontantInitial { get; set; } // Montant total du dossier

        [Column("montant_impaye")]
        [JsonRequired]
        public decimal MontantImpaye { get; set; } // Montant restant à payer

        [Column("frais_dossier")]
        [JsonRequired]
        public decimal FraisDossier { get; set; } // Frais éventuels

        [Column("statut_dossier")]
        public string StatutDossier { get; set; } = null!;
        // Statut : aimable / contentieux / regularise

        

        [Column("date_creation")]
        [JsonRequired]
        public DateTime DateCreation { get; set; } 
        //  date à laquelle le dossier a été créé
        
        [Column("type_emprunt")]
        public string TypeEmprunt { get; set; } = null!; 
        
        [Column("taux_interet")]
        [JsonRequired]
         public decimal TauxInteret { get; set; }

        public Client Client { get; set; } = null!; // Navigation vers client

        public ICollection<IntentionClient> Intentions { get; set; } = new List<IntentionClient>();
        // Navigation vers intentions associées

        public ICollection<Echeance> Echeances { get; set; } = new List<Echeance>();
        // Navigation vers échéances associées

        public ICollection<HistoriquePaiement> HistoriquePaiements { get; set; } = new List<HistoriquePaiement>();
        // Navigation vers les paiements enregistrés

        public ICollection<RelanceClient> Relances { get; set; } = new List<RelanceClient>();
        // Navigation vers les relances effectuées

        public ICollection<Communication> Communications { get; set; } = new List<Communication>();
        // Messages et notes liés au dossier

        public ICollection<Garantie> Garanties { get; set; } = new List<Garantie>();
        // Garanties associées au dossier

        public ICollection<ScoreRisque> ScoresRisque { get; set; } = new List<ScoreRisque>();
        // Évaluations de risque du dossier

        public ICollection<HistoriqueAction> HistoriqueActions { get; set; } = new List<HistoriqueAction>();
        // Actions réalisées sur le dossier
    }
}