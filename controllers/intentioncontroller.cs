using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecouvrementAPI.Data;
using RecouvrementAPI.DTOs;
using RecouvrementAPI.Models;
using RecouvrementAPI.Helpers;

namespace RecouvrementAPI.Controllers
{
    [ApiController]
    [Route("api/client/intention")]
    public class IntentionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        // Constantes pour éviter S1192 (string littérale répétée)
        private const string TYPE_RECLAMATION          = "reclamation";
        private const string TYPE_PAIEMENT_IMMEDIAT    = "paiement_immediat";
        private const string TYPE_PAIEMENT_PARTIEL     = "paiement_partiel";
        private const string TYPE_PROMESSE_PAIEMENT    = "promesse_paiement";
        private const string TYPE_CONSOLIDATION        = "demande_consolidation";
        private const string TYPE_ECHEANCE             = "demande_echeance";

        private static readonly string[] TypesValides =
        {
            TYPE_PAIEMENT_IMMEDIAT,
            TYPE_PAIEMENT_PARTIEL,
            TYPE_PROMESSE_PAIEMENT,
            TYPE_CONSOLIDATION,
            TYPE_ECHEANCE,
            TYPE_RECLAMATION
        };

     public IntentionController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==============================
        // POST api/client/intention/{token}
        // Client soumet son intention via son token UUID (lien SMS/email)
        // le token UUID est le mécanisme d'authentification
        // ==============================
        [HttpPost("{token}")]
        public async Task<IActionResult> PostIntention(string token, [FromBody] SubmitIntentionDto dto)
        {
            // 1. Authentification par token UUID
            var client = await _context.Clients
                .Include(c => c.Dossiers)
                .FirstOrDefaultAsync(c => c.TokenAcces == token);

            if (client == null)
                return Unauthorized(AppConstants.TokenInvalid);

            if (client.TokenExpireLe.HasValue && client.TokenExpireLe.Value < DateTime.UtcNow)
                return Unauthorized(AppConstants.TokenInvalid);

            // 2. Validation du body
            if (dto == null)
                return BadRequest(new { message = "Données manquantes." });

            if (string.IsNullOrWhiteSpace(dto.TypeIntention))
                return BadRequest(new { message = "Le type d'intention est requis." });

            if (!TypesValides.Contains(dto.TypeIntention))
                return BadRequest(new { message = "Type d'intention invalide.", typesAcceptes = TypesValides });

            // 3. Résolution du dossier (anti-IDOR)
            var dossier = dto.IdDossier.HasValue
                ? client.Dossiers.FirstOrDefault(d => d.IdDossier == dto.IdDossier.Value)
                : client.Dossiers.OrderByDescending(d => d.DateCreation).FirstOrDefault();

            if (dossier == null)
                return NotFound(AppConstants.DossierNotFound);

            // 4. Blocage multi-soumission (une seule par jour)
            bool dejaSoumis = await _context.Intentions.AnyAsync(i =>
                i.IdDossier == dossier.IdDossier &&
                i.DateIntention.Date == DateTime.Today);

            if (dejaSoumis)
                return BadRequest(new { message = "Vous avez déjà soumis une réponse aujourd'hui. Veuillez contacter votre agence pour toute modification." });

            // 5. Validation métier — extraite en méthode privée (réduit complexité cognitive)
            var erreurValidation = ValiderReglesMetier(dto, dossier);
            if (erreurValidation != null)
                return BadRequest(new { message = erreurValidation });

            // 6. Effet de bord reclamation
            if (dto.TypeIntention == TYPE_RECLAMATION)
                dossier.StatutDossier = AppConstants.DossierStatut.Contentieux;

            // 7. Création de l'intention
            var intention = new IntentionClient
            {
                IdDossier          = dossier.IdDossier,
                TypeIntention      = dto.TypeIntention,
                DateIntention      = DateTime.UtcNow,
                DatePaiementPrevue = dto.DatePaiementPrevue,
                MontantPropose     = dto.MontantPropose,
                Statut             = "En attente"
            };

            _context.Intentions.Add(intention);

            // 8. Communications + historique — extraits en méthode privée
            EnregistrerCommunications(dto, dossier);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message     = "Intention enregistrée avec succès",
                idIntention = intention.IdIntention,
                idDossier   = dossier.IdDossier
            });
        }

        // ==============================
        // Validation métier selon type d'intention
        // Retourne null si valide, sinon le message d'erreur
        // Extrait pour réduire la complexité cognitive de PostIntention
        // ==============================
        private static string ValiderReglesMetier(SubmitIntentionDto dto, DossierRecouvrement dossier)
        {
            if (dto.TypeIntention == TYPE_PAIEMENT_PARTIEL)
            {
                if (!dto.MontantPropose.HasValue || dto.MontantPropose <= 0)
                    return "Un montant proposé valide est requis pour un paiement partiel.";

                if (dto.MontantPropose >= dossier.MontantImpaye)
                    return "Le montant partiel doit être inférieur au montant total impayé. Utilisez 'Règlement total' à la place.";
            }
            else if (dto.TypeIntention == TYPE_PROMESSE_PAIEMENT)
            {
                if (!dto.DatePaiementPrevue.HasValue)
                    return "Une date de paiement prévue est requise pour une promesse de paiement.";

                if (dto.DatePaiementPrevue.Value.Date <= DateTime.UtcNow.Date)
                    return "La date de paiement promise doit être dans le futur.";
            }

            return null;
        }

        // ==============================
        // Enregistrement communications + historique
        // Extrait pour réduire la complexité cognitive de PostIntention
        // ==============================
        private void EnregistrerCommunications(SubmitIntentionDto dto, DossierRecouvrement dossier)
        {
            string detailMessage = dto.TypeIntention switch
            {
                TYPE_PAIEMENT_IMMEDIAT => "Le client a indiqué vouloir effectuer un règlement total immédiat.",
                TYPE_PAIEMENT_PARTIEL  => $"Le client propose un règlement partiel de {dto.MontantPropose:F3} TND (sur {dossier.MontantImpaye:F3} TND dus).",
                TYPE_PROMESSE_PAIEMENT => $"Le client a promis un paiement pour le {dto.DatePaiementPrevue!.Value:dd/MM/yyyy}.",
                TYPE_CONSOLIDATION     => "Le client demande une consolidation (restructuration) de sa dette.",
                TYPE_ECHEANCE          => "Le client demande un échéancier de paiement.",
                TYPE_RECLAMATION       => "Le client a soumis une réclamation. Dossier passé en contentieux.",
                _                      => $"Nouvelle intention : {dto.TypeIntention}"
            };

            if (!string.IsNullOrWhiteSpace(dto.Commentaire))
                detailMessage += $" Commentaire : {dto.Commentaire.Trim()}";

            _context.Communications.Add(new Communication
            {
                IdDossier = dossier.IdDossier,
                Message   = detailMessage,
                Origine   = AppConstants.ClientActor,
                DateEnvoi = DateTime.UtcNow
            });

            _context.Communications.Add(new Communication
            {
                IdDossier = dossier.IdDossier,
                Message   = $"[ACCUSÉ DE RÉCEPTION] Nous avons bien enregistré votre '{dto.TypeIntention.Replace("_", " ")}'. Votre demande est en cours de traitement par votre agence.",
                Origine   = "systeme",
                DateEnvoi = DateTime.UtcNow.AddSeconds(1)
            });

            _context.HistoriqueActions.Add(new HistoriqueAction
            {
                IdDossier    = dossier.IdDossier,
                ActionDetail = $"Soumission d'intention : {dto.TypeIntention}.{(dto.TypeIntention == TYPE_RECLAMATION ? " Passage en CONTENTIEUX." : "")}",
                Acteur       = AppConstants.ClientActor,
                DateAction   = DateTime.UtcNow
            });
        }
    }
}