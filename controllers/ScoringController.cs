using Microsoft.AspNetCore.Authorization;
using RecouvrementAPI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecouvrementAPI.Data;
using RecouvrementAPI.DTOs;
using RecouvrementAPI.Models;
using RecouvrementAPI.Helpers;

namespace RecouvrementAPI.Controllers
{
    /// <summary>
    /// Contrôleur implémentant le "Moteur de scoring IA" basées sur les règles métiers (Rule-Based).
    /// Route externe : /api/Scoring
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ScoringController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ScoringController> _logger;

        public ScoringController(ApplicationDbContext context, ILogger<ScoringController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Affiche le tableau de bord de priorisation des dossiers (Dashboard "Scoring IA")
        /// Route explicite : GET http://localhost:5203/api/Scoring/dashboard
        /// </summary>
        [HttpGet("dashboard")]
        public async Task<ActionResult<ScoringDashboardResponseDto>> GetDashboard(
            [FromQuery] string etatDossier = "Tous",
            [FromQuery] string recherche = "",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var query = _context.Dossiers
                    .Include(d => d.Client)
                    .Include(d => d.ScoresRisque)
                    .Include(d => d.Echeances) 
                    .Include(d => d.Intentions.OrderByDescending(i => i.DateIntention))
                    .Where(d => d.Client.Statut != "Archivé") // Masquer les clients archivés
                    .AsQueryable();

                if (!string.IsNullOrEmpty(etatDossier) && !string.Equals(etatDossier, "Tous", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(d => string.Equals(d.StatutDossier, etatDossier, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrEmpty(recherche))
                {
                    query = query.Where(d => 
                        d.Client.Nom.Contains(recherche) ||
                        d.Client.Prenom.Contains(recherche) ||
                        d.IdDossier.ToString().Contains(recherche));
                }

                var tousDossiers = await query.ToListAsync();

                var dossiersScores = tousDossiers
                    .Where(d => d.ScoresRisque != null && d.ScoresRisque.Count > 0)
                    .Select(d => new
                    {
                        Dossier = d,
                        DernierScore = d.ScoresRisque.OrderByDescending(s => s.DateCalcul).First()
                    })
                    .ToList();

                int risqueEleve = dossiersScores.Count(x => x.DernierScore.ScoreTotal > 60);
                int risqueMoyen = dossiersScores.Count(x => x.DernierScore.ScoreTotal >= 30 && x.DernierScore.ScoreTotal <= 60);
                int risqueFaible = dossiersScores.Count(x => x.DernierScore.ScoreTotal < 30);

                var items = dossiersScores
                    .OrderByDescending(x => x.DernierScore.ScoreTotal) // Plus risqués en haut
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new ScoringItemDto
                    {
                        IdDossier = x.Dossier.IdDossier,
                        Client = $"{x.Dossier.Client.Nom} {x.Dossier.Client.Prenom.Substring(0, 1)}.",
                        RetardTexte = GetRetardLabel(RecouvrementHelper.CalculerJoursRetard(x.Dossier.Echeances)),
                        PointsRetard = x.DernierScore.PointsRetard,
                        PointsHistorique = x.DernierScore.PointsHistorique,
                        PointsGarantie = x.DernierScore.PointsGarantie,
                        PointsIntention = x.DernierScore.PointsIntention,
                        ScoreTotal = x.DernierScore.ScoreTotal,
                        Niveau = x.DernierScore.Niveau
                    })
                    .ToList();

                // On sélectionne par défaut le client le plus risqué (score élevé) pour le mettre de côté dans le panel détails
                var topScore = dossiersScores.OrderByDescending(x => x.DernierScore.ScoreTotal).FirstOrDefault();
                ScoringDetailsDto detailsActif = null;

                if (topScore != null)
                {
                    detailsActif = ConstruireDetailsDto(topScore.DernierScore, topScore.Dossier);
                }

                return Ok(new ScoringDashboardResponseDto
                {
                    Kpis = new ScoringKpiDto
                    {
                        DossiersScores = dossiersScores.Count,
                        RisqueEleve = risqueEleve,
                        RisqueMoyen = risqueMoyen,
                        RisqueFaible = risqueFaible
                    },
                    Items = items,
                    TotalItems = dossiersScores.Count,
                    TotalPages = (int)Math.Ceiling(dossiersScores.Count / (double)pageSize),
                    CurrentPage = page,
                    DetailActif = detailsActif!
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur au rendu du Scoring Dashboard.");
                return StatusCode(500, new { message = "Erreur de chargement du moteur IA." });
            }
        }

        /// <summary>
        /// Permet de récupérer la recommandation et le panneau latéral pour un client cliqué.
        /// Route: GET http://localhost:5203/api/Scoring/{idDossier}/details
        /// </summary>
        [HttpGet("{id}/details")]
        public async Task<ActionResult<ScoringDetailsDto>> GetScoringDetails(int id)
        {
            var dossier = await _context.Dossiers
                .Include(d => d.Client)
                .Include(d => d.ScoresRisque)
                .Include(d => d.Echeances)
                .Include(d => d.Intentions.OrderByDescending(i => i.DateIntention))
                .FirstOrDefaultAsync(d => d.IdDossier == id);

            if (dossier == null || dossier.ScoresRisque.Count == 0)
                return NotFound(new { message = "Dossier ou score introuvable" });

            var dernierScore = dossier.ScoresRisque.OrderByDescending(s => s.DateCalcul).First();
            return Ok(ConstruireDetailsDto(dernierScore, dossier));
        }

        /// <summary>
        /// Mini IA de recommandation contextuelle selon l'etat reel du dossier.
        /// Route: GET /api/Scoring/{id}/recommandation-ia
        /// </summary>
        [HttpGet("{id}/recommandation-ia")]
        public async Task<ActionResult<ScoringAiRecommendationDto>> GetRecommandationIA(int id)
        {
            var dossier = await _context.Dossiers
                .Include(d => d.Client)
                .Include(d => d.ScoresRisque)
                .Include(d => d.Echeances)
                .Include(d => d.Garanties)
                .Include(d => d.Intentions.OrderByDescending(i => i.DateIntention))
                .FirstOrDefaultAsync(d => d.IdDossier == id);

            if (dossier == null || dossier.ScoresRisque.Count == 0)
                return NotFound(new { message = "Dossier ou score introuvable" });

            var score = dossier.ScoresRisque.OrderByDescending(s => s.DateCalcul).First();
            return Ok(ConstruireRecommandationIA(score, dossier));
        }

        /// <summary>
        /// EXÉCUTE DE MANIÈRE GLOBALE.
        /// Route: POST http://localhost:5203/api/Scoring/recalculer-tous
        /// </summary>
        [HttpPost("recalculer-tous")]
        public async Task<IActionResult> RecalculerTous()
        {
            var dossiersId = await _context.Dossiers
                .Where(d => d.StatutDossier != "regularise")
                .Select(d => d.IdDossier)
                .ToListAsync();

            foreach(var id in dossiersId)
            {
                await RunScoringAlgorithm(id);
            }

            return Ok(new { message = $"Recalcul effectué pour {dossiersId.Count} dossiers." });
        }

        /// <summary>
        /// Déclenche l'algorithme pour 1 seul dossier cible.
        /// Route : POST http://localhost:5203/api/Scoring/{id}/recalculer
        /// </summary>
        [HttpPost("{id}/recalculer")]
        public async Task<IActionResult> RecalculerDossier(int id)
        {
            await RunScoringAlgorithm(id);
            return Ok(new { message = "Score mis à jour avec succès." });
        }

        // ==========================================
        //  FONCTIONS ALGORITHMIQUES 
        // ==========================================

        private async Task RunScoringAlgorithm(int idDossier)
        {
            var dossier = await _context.Dossiers
                .Include(d => d.Client)
                    .ThenInclude(c => c.Dossiers) 
                        .ThenInclude(cd => cd.Echeances)
                .Include(d => d.Echeances)
                .Include(d => d.Garanties)
                .Include(d => d.Intentions.OrderByDescending(i => i.DateIntention))
                .FirstOrDefaultAsync(d => d.IdDossier == idDossier);

            if (dossier == null) return;

            int retardJours = RecouvrementHelper.CalculerJoursRetard(dossier.Echeances);
            int ptsRetard = GetPointsRetard(retardJours);

            int ptsHistorique = GetPointsHistorique(dossier.Client);

            int ptsGarantie = GetPointsGarantie(dossier.Garanties);

            int ptsIntention = GetPointsIntention(dossier.Intentions.FirstOrDefault()!);

            int scoreCalcule = ptsRetard + ptsHistorique + ptsGarantie + ptsIntention;
            if (scoreCalcule < 0) scoreCalcule = 0;
            if (scoreCalcule > 100) scoreCalcule = 100;

            string niveau = "Faible";
            if (scoreCalcule >= 60) niveau = "Élevé";
            else if (scoreCalcule >= 30) niveau = "Moyen";

            _context.ScoresRisque.Add(new ScoreRisque
            {
                IdDossier = dossier.IdDossier,
                ScoreTotal = scoreCalcule,
                PointsRetard = ptsRetard,
                PointsHistorique = ptsHistorique,
                PointsGarantie = ptsGarantie,
                PointsIntention = ptsIntention,
                Niveau = niveau,
                DateCalcul = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
        }

        private ScoringDetailsDto ConstruireDetailsDto(ScoreRisque score, DossierRecouvrement dossier)
        {
            var derniereInt = dossier.Intentions.FirstOrDefault();
            var intentionStr = derniereInt != null ? derniereInt.TypeIntention.Replace("_", " ") : "Non spécifié";

            return new ScoringDetailsDto
            {
                ClientNom = $"{dossier.Client.Nom} {dossier.Client.Prenom}",
                ScoreTotal = score.ScoreTotal,
                DetailRetard = $"{GetRetardLabel(RecouvrementHelper.CalculerJoursRetard(dossier.Echeances))}",
                PtsRetard = score.PointsRetard,
                DetailHistorique = score.PointsHistorique >= 40 ? "Retards fréquents" : "Retards moyens/faibles",
                PtsHistorique = score.PointsHistorique,
                DetailGarantie = score.PointsGarantie == 40 ? "Aucune garantie" : "Garantie moyenne/Forte",
                PtsGarantie = score.PointsGarantie,
                DetailIntention = char.ToUpper(intentionStr[0]) + intentionStr.Substring(1),
                PtsIntention = score.PointsIntention,
                Recommandation = GenererTexteRecommandation(score, dossier, intentionStr),
                DateCalcul = score.DateCalcul.ToString("dd MMMM yyyy")
            };
        }

        private static string GenererTexteRecommandation(ScoreRisque score, DossierRecouvrement dossier, string derniereIntention)
        {
            var retardTexte = GetRetardLabel(RecouvrementHelper.CalculerJoursRetard(dossier.Echeances));
            var points = score.ScoreTotal;

            // Logique intelligente pour générer un bloc de texte complet comme sur la maquette :
            if (score.Niveau == "Élevé")
            {
                return 
$@"Alerte : Pré-Escalade Juridique (M-1)

Situation : Retard de *{retardTexte}*. Le dossier approche des seuils critiques légaux.
Analyse : Score IA est de *{points} pts* malgré {derniereIntention} ({score.PointsIntention}), à cause de retards trop fréquents (+{score.PointsHistorique}).
Action requise : *Ultime tentative de contact*. Préparer le dossier pour transfert automatique au Juridique dans 30 jours si la dette n'est pas régularisée.";
            }
            if (score.Niveau == "Moyen")
            {
                return 
$@"Alerte : Priorité Surveillance Modérée

Situation : Retard d'environnement sain qui s'est détérioré ({retardTexte}).
Analyse : Score algorithmique de *{points} pts* équilibré.
Action requise : *Phase amiable forcée*. Déclencher la procédure d'appels hebdomadaires par l'agent de recouvrement.";
            }

            return 
$@"Alerte : Risque Faible (Sécurisé)

Situation : Retard minimal ({retardTexte}). Garanties solides.
Analyse : Comportement rassurant ({points} pts).
Action requise : *Suivi automatisé standard*. Ne pas interférer manuellement, laissez les e-mails s'envoyer seuls.";
        }

        private static int GetPointsRetard(int retardJours)
        {
            if (retardJours < 30) return 10;
            if (retardJours <= 90) return 30;
            return 50;
        }

        private static int GetPointsHistorique(Client client)
        {
            var totalEcheancesImpayees = client.Dossiers
                .SelectMany(d => d.Echeances)
                .Count(e => e.Statut == "impaye" && e.DateEcheance < DateTime.UtcNow);

            if (totalEcheancesImpayees == 0) return 5;
            if (totalEcheancesImpayees <= 3) return 20;
            return 40;
        }

        private static int GetPointsGarantie(IEnumerable<Garantie> garanties)
        {
            if (!garanties.Any()) return 40;
            if (garanties.Any(g => string.Equals(g.TypeGarantie, "hypotheque", StringComparison.OrdinalIgnoreCase) || 
                                    string.Equals(g.TypeGarantie, "salaire", StringComparison.OrdinalIgnoreCase))) return 5;
            return 20;
        }

        private static int GetPointsIntention(IntentionClient derniereIntention)
        {
            if (derniereIntention == null) return 20;
            return derniereIntention.TypeIntention switch
            {
                "paiement_immediat" => -20,
                "promesse_paiement" => -10,
                "reclamation" or "demande_consolidation" => 0,
                _ => 20
            };
        }

        // Logic moved to RecouvrementHelper

        private static string GetRetardLabel(int jours)
        {
            if (jours == 0) return "Aucun retard";
            if (jours < 30) return $"{jours} jours";
            return $"{jours / 30} mois";
        }

        private static ScoringAiRecommendationDto ConstruireRecommandationIA(ScoreRisque score, DossierRecouvrement dossier)
        {
            int retardJours = RecouvrementHelper.CalculerJoursRetard(dossier.Echeances);
            var derniereIntention = dossier.Intentions.FirstOrDefault()?.TypeIntention ?? "non_specifiee";

            var dto = new ScoringAiRecommendationDto
            {
                IdDossier = dossier.IdDossier,
                ClientNom = $"{dossier.Client.Nom} {dossier.Client.Prenom}",
                ScoreTotal = score.ScoreTotal,
                NiveauRisque = score.Niveau,
                Resume = GenererTexteRecommandation(score, dossier, derniereIntention.Replace("_", " ")),
                Confiance = CalculerConfiance(score, dossier)
            };

            if (score.Niveau == "Élevé")
            {
                dto.PrioriteTraitement = "Urgente (24h)";
                dto.ActionsRecommandees.Add("Contacter le client aujourd hui (appel + email)");
                dto.ActionsRecommandees.Add("Preparer pre-escalade juridique si absence de retour sous 7 jours");
                dto.ActionsRecommandees.Add("Valider possibilite de plan de paiement supervise");
            }
            else if (score.Niveau == "Moyen")
            {
                dto.PrioriteTraitement = "Haute (72h)";
                dto.ActionsRecommandees.Add("Lancer sequence amiable (appel + relance ecrite)");
                dto.ActionsRecommandees.Add("Proposer echeancier adapte au reste a payer");
                dto.ActionsRecommandees.Add("Reevaluer le score apres nouvelle interaction client");
            }
            else
            {
                dto.PrioriteTraitement = "Normale (7 jours)";
                dto.ActionsRecommandees.Add("Maintenir suivi automatise");
                dto.ActionsRecommandees.Add("Programmer rappel preventif avant prochaine echeance");
            }

            dto.Justifications.Add($"Retard observe: {retardJours} jours");
            dto.Justifications.Add($"Intention client recente: {derniereIntention}");
            dto.Justifications.Add($"Statut dossier: {dossier.StatutDossier}");
            dto.Justifications.Add($"Presence garanties: {(dossier.Garanties.Any() ? "oui" : "non")}");

            return dto;
        }

        private static int CalculerConfiance(ScoreRisque score, DossierRecouvrement dossier)
        {
            int confiance = 60;

            if (dossier.Echeances.Any()) confiance += 10;
            if (dossier.Intentions.Any()) confiance += 10;
            if (dossier.Garanties.Any()) confiance += 5;
            if (score.DateCalcul > DateTime.UtcNow.AddDays(-30)) confiance += 10;

            if (confiance > 95) confiance = 95;
            if (confiance < 0) confiance = 0;
            return confiance;
        }
    }
}

