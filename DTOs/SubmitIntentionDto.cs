using System;

namespace RecouvrementAPI.DTOs
{
    /// <summary>
    /// DTO utilisé par le CLIENT pour soumettre son intention depuis le portail.
    /// </summary>
    public class SubmitIntentionDto
    {
        // Type d'intention (obligatoire) : paiement_immediat, promesse_paiement, reclamation, etc.
        public string TypeIntention { get; set; } = string.Empty;

        // Date à laquelle le client s'engage à payer (optionnel, requis pour promesse_paiement)
        public DateTime? DatePaiementPrevue { get; set; }

        // Montant que le client propose de payer (optionnel, requis pour paiement_partiel)
        public decimal? MontantPropose { get; set; }

        // Indice de confiance déclaré par le client (0-100%)
        public int? ConfianceClient { get; set; }

        // Commentaire libre du client
        public string? Commentaire { get; set; }
        
        // ID du dossier concerné (optionnel, déduit du token si nul)
        public int? IdDossier { get; set; }
    }
}
