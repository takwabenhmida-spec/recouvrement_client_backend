using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecouvrementAPI.Data;
using RecouvrementAPI.DTOs;
using RecouvrementAPI.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace RecouvrementAPI.Controllers
{
    // Contrôleur qui gère tout ce que le CLIENT peut faire
    // Route de base : http://localhost:5203/api/client
    [ApiController]
    [Route("api/client")]
    public class ClientController : ControllerBase
    {
        // _context : accès à la base de données MySQL
        private readonly ApplicationDbContext _context;

        // _env : accès au système de fichiers du serveur (upload)
        private readonly IWebHostEnvironment _env;

        // Constructeur : .NET injecte automatiquement les dépendances
        public ClientController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ==============================
        // MÉTHODE PRIVÉE : Vérifier token
        // Charge le client avec son agence et TOUS ses dossiers.
        // Retourne null si token introuvable → les endpoints retournent 401.
        // ==============================
        private async Task<Client> VerifierToken(string token)
        {
            return await _context.Clients
                .Include(c => c.Agence)
                .Include(c => c.Dossiers)
                .FirstOrDefaultAsync(c => c.TokenAcces == token);
        }

        // ==============================
        // MÉTHODE PRIVÉE : Résoudre le dossier cible
        //
        // Logique de sélection du dossier (comportement par défaut) :
        //   • Si idDossier est fourni  → cherche ce dossier précis parmi ceux du client
        //                                (retourne null si non trouvé ou n'appartient pas au client)
        //   • Si idDossier est null    → prend automatiquement le dossier le plus récent
        //                                (OrderByDescending sur DateCreation)
        //
        // Ce comportement par défaut garantit qu'un client qui ne sélectionne
        // rien obtient toujours son dossier actif le plus récent.
        // ==============================
        private DossierRecouvrement ResoudreDossier(Client client, int? idDossier)
        {
            if (idDossier.HasValue)
            {
                // Recherche du dossier spécifié ET appartenant bien à ce client
                // (protection contre la manipulation d'ID par un autre client)
                return client.Dossiers.FirstOrDefault(d => d.IdDossier == idDossier.Value);
            }

            // Comportement par défaut : dossier le plus récent
            return client.Dossiers
                .OrderByDescending(d => d.DateCreation)
                .FirstOrDefault();
        }

        // ==============================
        // MÉTHODE PRIVÉE : Vérifier retard > 3 mois
        // Déclenche une communication automatique si retard > 90 jours.
        // Appelée pour chaque dossier dans GetHistorique().
        // ==============================
        private async Task VerifierRetard3Mois(DossierRecouvrement dossier)
        {
            // Cherche la première échéance impayée dépassée
            var premiereEcheance = dossier.Echeances
                .Where(e => e.Statut == "impaye" && e.DateEcheance < DateTime.Now)
                .OrderBy(e => e.DateEcheance)
                .FirstOrDefault();

            // Aucune échéance impayée → rien à faire
            if (premiereEcheance == null) return;

            int joursRetard = (int)(DateTime.Now - premiereEcheance.DateEcheance).TotalDays;

            if (joursRetard > 90)
            {
                // Anti-doublon : pas de communication si une a déjà été envoyée ce mois
                bool dejaEnvoyee = await _context.Communications
                    .AnyAsync(c =>
                        c.IdDossier == dossier.IdDossier &&
                        c.Origine == "systeme" &&
                        c.DateEnvoi >= DateTime.Now.AddMonths(-1));

                if (!dejaEnvoyee)
                {
                    _context.Communications.Add(new Communication
                    {
                        IdDossier = dossier.IdDossier,
                        Message = $"Alerte automatique : retard de {joursRetard} jours " +
                                  $"détecté sur votre dossier. " +
                                  $"Montant impayé : {dossier.MontantImpaye} TND. " +
                                  $"Veuillez régulariser votre situation.",
                        Origine = "systeme",
                        DateEnvoi = DateTime.Now
                    });

                    _context.HistoriqueActions.Add(new HistoriqueAction
                    {
                        IdDossier = dossier.IdDossier,
                        ActionDetail = $"Communication auto déclenchée — retard > 3 mois ({joursRetard} jours)",
                        Acteur = "systeme",
                        DateAction = DateTime.Now
                    });

                    await _context.SaveChangesAsync();
                }
            }
        }

        // ==============================
        // MÉTHODE PRIVÉE : Calculer jours de retard
        // Factorisée pour éviter la duplication entre GetHistorique() et GenerateRecu().
        // Retourne 0 si aucune échéance impayée dépassée.
        // ==============================
        private int CalculerJoursRetard(DossierRecouvrement dossier)
        {
            var echeancesImpayeesDepassees = dossier.Echeances
                .Where(e => e.Statut == "impaye" && e.DateEcheance < DateTime.Now);

            if (!echeancesImpayeesDepassees.Any()) return 0;

            return (int)(DateTime.Now - echeancesImpayeesDepassees.Min(e => e.DateEcheance)).TotalDays;
        }

        // ==============================
        // MÉTHODE PRIVÉE : Mapper DossierRecouvrement → DossierDto
        // Factorisée pour éviter la duplication entre GetHistorique() et tout futur endpoint.
        // ==============================
        private DossierDto MapDossierToDto(DossierRecouvrement dossier)
        {
            int joursRetard = CalculerJoursRetard(dossier);

            return new DossierDto
            {
                IdDossier      = dossier.IdDossier,
                TypeEmprunt    = dossier.TypeEmprunt,
                MontantImpaye  = dossier.MontantImpaye,
                MontantInitial = dossier.MontantInitial,
                MontantPaye    = dossier.MontantInitial - dossier.MontantImpaye,
                FraisDossier   = dossier.FraisDossier,
                StatutDossier  = dossier.StatutDossier,
                TauxInteret    = dossier.TauxInteret,

                // Intérêts : 0 si retard ≤ 90 jours, calculés sinon
                MontantInterets = joursRetard > 90
                    ? dossier.MontantImpaye * (dossier.TauxInteret / 100) * (decimal)joursRetard / 365
                    : 0,

                NombreJoursRetard = joursRetard,

                // Prochaine échéance (la plus proche dans le temps)
                DateEcheance = dossier.Echeances
                    .OrderBy(e => e.DateEcheance)
                    .Select(e => e.DateEcheance)
                    .FirstOrDefault(),

                Garanties = dossier.Garanties.Select(g => new GarantieDto
                {
                    TypeGarantie = g.TypeGarantie,
                    Description  = g.Description
                }).ToList(),

                Echeances = dossier.Echeances.Select(e => new EcheanceDto
                {
                    Montant      = e.Montant,
                    DateEcheance = e.DateEcheance,
                    Statut       = e.Statut
                }).ToList(),

                Paiements = dossier.HistoriquePaiements.Select(p => new HistoriquePaiementDto
                {
                    MontantPaye  = p.MontantPaye,
                    TypePaiement = p.TypePaiement,
                    DatePaiement = p.DatePaiement
                }).ToList(),

                // ← CORRECTION : IdRelance ajouté pour que Angular puisse appeler repondre-relance
                Relances = dossier.Relances.Select(r => new RelanceDto
                {
                    IdRelance   = r.IdRelance,
                    DateRelance = r.DateRelance,
                    Moyen       = r.Moyen,
                    Statut      = r.Statut,
                    Contenu     = r.Contenu,
                }).ToList(),

                // ← CORRECTION : IdRelance ajouté pour distinguer réponse relance / message libre
                Communications = dossier.Communications.Select(c => new CommunicationDto
                {
                    Message   = c.Message,
                    Origine   = c.Origine,
                    DateEnvoi = c.DateEnvoi,
                    IdRelance = c.IdRelance
                }).ToList()
            };
        }

        // ==============================
        // GET api/client/historique/{token}
        //
        // Retourne TOUS les dossiers du client en JSON.
        // Appelé par Angular pour afficher la liste des dossiers
        // et laisser le client en choisir un.
        // ==============================
       [HttpGet("historique/{token}")]
        public async Task<IActionResult> GetHistorique(string token)
        {
            if (string.IsNullOrEmpty(token))
                return BadRequest("Token requis");

            // Chargement eager : toutes les relations en une seule requête SQL
            var client = await _context.Clients
                .Include(c => c.Agence)
                .Include(c => c.Dossiers)
                    .ThenInclude(d => d.Echeances)
                .Include(c => c.Dossiers)
                    .ThenInclude(d => d.HistoriquePaiements)
                .Include(c => c.Dossiers)
                    .ThenInclude(d => d.Relances)
                .Include(c => c.Dossiers)
                    .ThenInclude(d => d.Communications)
                .Include(c => c.Dossiers)
                    .ThenInclude(d => d.Garanties)
                .FirstOrDefaultAsync(c => c.TokenAcces == token);

            if (client == null)
                return Unauthorized("Token invalide");

            // Journalisation de l'accès (dossier le plus récent comme référence de log)
            var dossierPrincipal = client.Dossiers
                .OrderByDescending(d => d.DateCreation)
                .FirstOrDefault();

            if (dossierPrincipal != null)
            {
                _context.HistoriqueActions.Add(new HistoriqueAction
                {
                    IdDossier = dossierPrincipal.IdDossier,
                    ActionDetail = $"Accès client via token UUID — IP : {HttpContext.Connection.RemoteIpAddress}",
                    Acteur = "client",
                    DateAction = DateTime.Now
                });
                await _context.SaveChangesAsync();
            }

            // Vérification du retard > 3 mois pour chaque dossier
            foreach (var dossier in client.Dossiers)
            {
                await VerifierRetard3Mois(dossier);
            }

            // Construction du DTO — contient TOUS les dossiers du client
            // Angular pourra afficher la liste et laisser le client en choisir un
            var dto = new ClientHistoriqueDto
            {
                NomComplet = client.Nom + " " + client.Prenom,
                IdAgence   = client.Agence != null ? client.Agence.IdAgence : 0,
                VilleAgence = client.Agence?.Ville,

                // Tous les dossiers, du plus récent au plus ancien
                Dossiers = client.Dossiers
                    .OrderByDescending(d => d.DateCreation)
                    .Select(dossier => MapDossierToDto(dossier))
                    .ToList()
            };

            return Ok(dto);
        }

        // ==============================
        // GET api/client/recu/{token}
        // GET api/client/recu/{token}?idDossier=42   ← dossier spécifique
        //
        // Génère et télécharge un PDF du reçu de situation.
        //   • Sans idDossier → PDF du dossier le plus récent (comportement par défaut)
        //   • Avec idDossier → PDF du dossier choisi par le client
        // ==============================
        [HttpGet("recu/{token}")]
        public async Task<IActionResult> GenerateRecu(string token, [FromQuery] int? idDossier = null)
        {
            // 1. Sécurité : Vérification de l'identité du client via le Token unique (UUID)
            var client = await VerifierToken(token);
            if (client == null)
                return Unauthorized("Token invalide");

            // 2. Sélection du dossier : Soit l'ID fourni, soit le dossier le plus récent par défaut
            var dossier = ResoudreDossier(client, idDossier);
            if (dossier == null) return NotFound("Dossier introuvable");

            // 3. Chargement des données : On inclut les échéances pour calculer le retard
            dossier = await _context.Dossiers
                .Include(d => d.Echeances)
                .FirstOrDefaultAsync(d => d.IdDossier == dossier.IdDossier);

            // 4. Logique métier : Calcul du nombre de jours de retard cumulés
            int joursRetard = CalculerJoursRetard(dossier);

            // --- CALCUL DU MONTANT PAYÉ ---
            // Différence entre ce qui était prévu au départ et ce qui reste à payer
            decimal montantPaye = dossier.MontantInitial - dossier.MontantImpaye;

            // 5. Calcul des intérêts de retard (Règle des 90 jours)
            decimal montantInterets = joursRetard > 90
                ? dossier.MontantImpaye * (dossier.TauxInteret / 100) * ((decimal)joursRetard / 365)
                : 0;

            // 6. Calcul du Total (Principal restant + Intérêts)
            decimal totalARegler = dossier.MontantImpaye + montantInterets;

            // 7. Identité visuelle selon le statut
            string colorHex = dossier.StatutDossier == "regularise" ? Colors.Green.Medium :
                             (dossier.StatutDossier == "contentieux" ? Colors.Red.Medium : Colors.Blue.Medium);

            // 8. Génération du document PDF
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(50);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    // EN-TÊTE CORRIGÉ : Affichage STB BANK + Ville
                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("REÇU DE SITUATION").FontSize(22).SemiBold().FontColor(Colors.Blue.Medium);
                            col.Item().Text($"Dossier n° {dossier.IdDossier}").FontSize(10);
                        });

                        // Ici on force "STB BANK" suivi de la ville de l'agence
                        row.RelativeItem().AlignRight().Text($"STB BANK - {client.Agence?.Ville}").Bold();
                    });

                    page.Content().PaddingVertical(25).Column(col =>
                    {
                        col.Spacing(10);
                        col.Item().Text($"Client : {client.Nom} {client.Prenom}").Bold();

                        col.Item().Text(text => {
                            text.Span("Retard constaté : ").Bold();
                            text.Span($"{joursRetard} jours").FontColor(joursRetard > 0 ? Colors.Red.Medium : Colors.Green.Medium).Bold();
                        });

                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        col.Item().Text($"Type de crédit : {dossier.TypeEmprunt}");
                        col.Item().Text($"Montant initial : {dossier.MontantInitial:F3} TND");

                        // --- AFFICHAGE DU MONTANT DÉJÀ PAYÉ ---
                        col.Item().Text(text => {
                            text.Span("Montant déjà payé : ");
                            text.Span($"{montantPaye:F3} TND").FontColor(Colors.Green.Medium).SemiBold();
                        });

                        col.Item().Text($"Principal restant : {dossier.MontantImpaye:F3} TND");

                        if (montantInterets > 0)
                        {
                            col.Item().Text(text => {
                                text.Span($"Intérêts de retard ({dossier.TauxInteret}%) : ").Bold();
                                text.Span($"{montantInterets:F3} TND").FontColor(Colors.Red.Medium);
                            });
                        }

                        col.Item().Text($"Frais de dossier : {dossier.FraisDossier:F3} TND");

                        // BLOC RÉCAPITULATIF FINAL
                        col.Item().PaddingTop(15).Background(Colors.Grey.Lighten4).Padding(15).Column(inner =>
                        {
                            inner.Item().Text("Montant à apyer").FontSize(11).Bold();
                            inner.Item().Text($"{totalARegler:F3} TND")
                                .FontSize(28).Bold().FontColor(colorHex);
                        });
                    });

                    page.Footer().AlignCenter().Text($"Document généré le {DateTime.Now:dd/MM/yyyy HH:mm}");
                });
            });

            byte[] pdfBytes = document.GeneratePdf();
            return File(pdfBytes, "application/pdf", $"Recu_STB_Dossier_{dossier.IdDossier}.pdf");
        }
        // Nom du fichier inclut l'idDossier pour distinguer les reçus d'un même client

        // ==============================
        // POST api/client/upload/{token}
        // POST api/client/upload/{token}?idDossier=42   ← dossier spécifique
        //

        // ==============================
        [HttpPost("upload/{token}")]
        public async Task<IActionResult> UploadJustificatif(
            string token,
            IFormFile File,
            [FromQuery] int? idDossier = null)
        {
            var client = await VerifierToken(token);
            if (client == null)
                return Unauthorized("Token invalide");

            if (File == null || File.Length == 0)
                return BadRequest("Aucun fichier envoyé");

            // Whitelist d'extensions autorisées (insensible à la casse)
            var extensionsAutorisees = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(File.FileName).ToLower();
            if (!extensionsAutorisees.Contains(extension))
                return BadRequest("Format non autorisé. Utilisez PDF, JPG ou PNG.");

            if (File.Length > 5 * 1024 * 1024)
                return BadRequest("Fichier trop volumineux. Maximum 5 MB.");

            // Résolution du dossier cible (défaut = le plus récent)
            var dossier = ResoudreDossier(client, idDossier);
            if (dossier == null)
                return NotFound(idDossier.HasValue
                    ? $"Dossier {idDossier} introuvable ou n'appartient pas à ce client."
                    : "Aucun dossier trouvé.");

            // Stockage dans un sous-dossier propre à chaque dossier de recouvrement
            var uploadsPath = Path.Combine(
                _env.ContentRootPath, "uploads", dossier.IdDossier.ToString());
            Directory.CreateDirectory(uploadsPath);

            // Nom unique : horodatage + nom client → évite toute collision de fichier
            var nomFichier = $"{DateTime.Now:yyyyMMddHHmmss}_{client.Nom}{extension}";
            var cheminComplet = Path.Combine(uploadsPath, nomFichier);

            using (var stream = new FileStream(cheminComplet, FileMode.Create))
            {
                await File.CopyToAsync(stream);
            }

            _context.HistoriqueActions.Add(new HistoriqueAction
            {
                IdDossier    = dossier.IdDossier,
                ActionDetail = $"Client a uploadé un justificatif : {nomFichier}",
                Acteur       = "client",
                DateAction   = DateTime.Now
            });

            // Communication automatique vers l'agent pour l'informer du nouveau justificatif
            _context.Communications.Add(new Communication
            {
                IdDossier = dossier.IdDossier,
                Message   = $"Le client a envoyé un justificatif : {nomFichier}",
                Origine   = "client",
                DateEnvoi = DateTime.Now
            });

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Fichier uploadé avec succès",
                nomFichier = nomFichier,
                // Retourne l'idDossier effectivement utilisé pour qu'Angular
                // sache à quel dossier l'upload a été rattaché (utile si idDossier était null)
                idDossierUtilise = dossier.IdDossier
            });
        }

        // ==============================
        // POST api/client/message/{token}
        // POST api/client/message/{token}?idDossier=42
        //
        // Client envoie un message libre à son agence.
        // IdRelance = NULL → pas lié à une relance.
        // ==============================
        [HttpPost("message/{token}")]
        public async Task<IActionResult> EnvoyerMessage(
            string token,
            [FromBody] EnvoyerMessageDto messageDto,
            [FromQuery] int? idDossier = null)
        {
            if (string.IsNullOrWhiteSpace(messageDto?.Contenu))
                return BadRequest("Le contenu du message est requis.");

            var client = await _context.Clients
                .Include(c => c.Dossiers)
                .FirstOrDefaultAsync(c => c.TokenAcces == token);

            if (client == null)
                return Unauthorized("Token invalide");

            var dossier = ResoudreDossier(client, idDossier);
            if (dossier == null)
                return NotFound("Dossier introuvable");

            _context.Communications.Add(new Communication
            {
                IdDossier = dossier.IdDossier,
                IdRelance = null,                 // ← message libre
                Message   = messageDto.Contenu.Trim(),
                Origine   = "client",
                DateEnvoi = DateTime.Now
            });

            _context.HistoriqueActions.Add(new HistoriqueAction
            {
                IdDossier    = dossier.IdDossier,
                ActionDetail = $"Client a envoyé un message : \"{messageDto.Contenu.Trim()}\"",
                Acteur       = "client",
                DateAction   = DateTime.Now
            });

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message          = "Message envoyé avec succès",
                idDossierUtilise = dossier.IdDossier
            });
        }

        // ==============================
        // POST api/client/repondre-relance/{token}/{idRelance}
        
        // ==============================
        [HttpPost("repondre-relance/{token}/{idRelance}")]
        public async Task<IActionResult> RepondreRelance(
            string token,
            int idRelance,
            [FromBody] EnvoyerMessageDto reponseDto)
        {
            if (string.IsNullOrWhiteSpace(reponseDto?.Contenu))
                return BadRequest("Le contenu de la réponse est requis.");

            var client = await _context.Clients
                .Include(c => c.Dossiers)
                    .ThenInclude(d => d.Relances)
                .FirstOrDefaultAsync(c => c.TokenAcces == token);

            if (client == null)
                return Unauthorized("Token invalide");

            // Sécurité anti-IDOR : la relance doit appartenir à un dossier du client
            var dossier = client.Dossiers
                .FirstOrDefault(d => d.Relances.Any(r => r.IdRelance == idRelance));

            if (dossier == null)
                return NotFound("Relance introuvable ou n'appartient pas à ce client.");

            var relance = dossier.Relances.First(r => r.IdRelance == idRelance);

            // Mise à jour du statut
            relance.Statut = "repondu";

            // Communication avec lien FK explicite vers la relance
            _context.Communications.Add(new Communication
            {
                IdDossier = dossier.IdDossier,
                IdRelance = idRelance,            // ← lien explicite relance ↔ communication
                Message   = reponseDto.Contenu.Trim(),
                Origine   = "client",
                DateEnvoi = DateTime.Now
            });

            _context.HistoriqueActions.Add(new HistoriqueAction
            {
                IdDossier    = dossier.IdDossier,
                ActionDetail = $"Client a répondu à la relance #{idRelance}",
                Acteur       = "client",
                DateAction   = DateTime.Now
            });

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message       = "Réponse enregistrée avec succès",
                idRelance     = idRelance,
                nouveauStatut = relance.Statut,
                idDossier     = dossier.IdDossier
            });
        }

        // ==============================
        // POST api/client/intention/{token}
        //
        // Client soumet son intention (promesse, paiement, réclamation).
        // Déclenche un accusé de réception automatique.
        // ==============================
        [HttpPost("intention/{token}")]
        public async Task<IActionResult> PostIntention(string token, [FromBody] SubmitIntentionDto dto)
        {
            var client = await _context.Clients
                .Include(c => c.Dossiers)
                .FirstOrDefaultAsync(c => c.TokenAcces == token);

            if (client == null)
                return Unauthorized("Token invalide");

            var dossier = ResoudreDossier(client, dto.IdDossier);
            if (dossier == null)
                return NotFound("Dossier introuvable");

            // 1. Création de l'intention sécurisée
            var intention = new IntentionClient
            {
                IdDossier = dossier.IdDossier,
                TypeIntention = dto.TypeIntention,
                DateIntention = DateTime.Now,
                DatePaiementPrevue = dto.DatePaiementPrevue,
                MontantPropose = dto.MontantPropose,
                ConfianceClient = dto.ConfianceClient,
                Statut = "En attente"
            };

            _context.Intentions.Add(intention);

            // 2. Enregistrement du commentaire dans les communications
            if (!string.IsNullOrWhiteSpace(dto.Commentaire))
            {
                _context.Communications.Add(new Communication
                {
                    IdDossier = dossier.IdDossier,
                    Message = dto.Commentaire.Trim(),
                    Origine = "client",
                    DateEnvoi = DateTime.Now
                });
            }

            // 3. ACCUSÉ DE RÉCEPTION SYSTÈME (Communication auto)
            _context.Communications.Add(new Communication
            {
                IdDossier = dossier.IdDossier,
                Message = $"[ACCUSÉ DE RÉCEPTION] Nous avons bien enregistré votre '{dto.TypeIntention.Replace("_", " ")}'. Votre demande est en cours de traitement par votre agence.",
                Origine = "systeme",
                DateEnvoi = DateTime.Now.AddSeconds(1) // Juste après pour l'ordre d'affichage
            });

            // 4. Historisation
            _context.HistoriqueActions.Add(new HistoriqueAction
            {
                IdDossier = dossier.IdDossier,
                ActionDetail = $"Soumission d'intention : {dto.TypeIntention}",
                Acteur = "client",
                DateAction = DateTime.Now
            });

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Intention enregistrée avec succès",
                idIntention = intention.IdIntention,
                idDossier = dossier.IdDossier
            });
        }

        // ==============================
        // GET api/client/accuse-reception/{token}/{idIntention}
        //
        // Génère un PDF officiel d'accusé de réception pour le client.
        // = [LIVRABLE PFE] =
        // ==============================
        [HttpGet("accuse-reception/{token}/{idIntention}")]
        public async Task<IActionResult> GenerateAccuseReception(string token, int idIntention)
        {
            var client = await _context.Clients
                .Include(c => c.Agence)
                .Include(c => c.Dossiers)
                    .ThenInclude(d => d.Intentions)
                .FirstOrDefaultAsync(c => c.TokenAcces == token);

            if (client == null) return Unauthorized("Token invalide");

            var intention = client.Dossiers
                .SelectMany(d => d.Intentions)
                .FirstOrDefault(i => i.IdIntention == idIntention);

            if (intention == null) return NotFound("Accusé de réception introuvable.");

            var dossier = client.Dossiers.First(d => d.IdDossier == intention.IdDossier);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(50);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    // Header avec branding STB
                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("ACCUSÉ DE RÉCEPTION").FontSize(24).ExtraBold().FontColor(Colors.Blue.Medium);
                            col.Item().Text("SOCIÉTÉ TUNISIENNE DE BANQUE").FontSize(10).SemiBold();
                            col.Item().Text($"Réf : ACK-INT-{intention.IdIntention:D5}").FontSize(9).Italic();
                        });

                        row.ConstantItem(100).AlignRight().Column(col => {
                            col.Item().Height(40).Background(Colors.Blue.Medium); // Placeholder pour logo
                            col.Item().AlignCenter().Text("STB BANK").FontSize(8);
                        });
                    });

                    page.Content().PaddingVertical(30).Column(col =>
                    {
                        col.Spacing(15);

                        // Bloc Identité
                        col.Item().Row(row => {
                            row.RelativeItem().Column(c => {
                                c.Item().Text("Détails Client").Bold().Underline();
                                c.Item().Text($"{client.Nom} {client.Prenom}");
                                c.Item().Text($"CIN : {client.CIN}");
                            });
                            row.RelativeItem().AlignRight().Column(c => {
                                c.Item().Text("Agence de Rattachement").Bold().Underline();
                                c.Item().Text(client.Agence?.Ville ?? "Direction Générale");
                            });
                        });

                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        // Corps de l'accusé
                        col.Item().PaddingTop(10).Text(text => {
                            text.Span("Objet : ").Bold();
                            text.Span($"Confirmation de réception d'intention de {intention.TypeIntention.Replace("_", " ")}.");
                        });

                        col.Item().Text($"Nous confirmons avoir reçu votre déclaration le {intention.DateIntention:dd/MM/yyyy} à {intention.DateIntention:HH:mm} concernant votre dossier de crédit n°{dossier.IdDossier}.");

                        // Détails de la soumission
                        col.Item().Background(Colors.Grey.Lighten4).Padding(15).Column(inner => {
                            inner.Spacing(5);
                            inner.Item().Text("Récapitulatif de votre déclaration :").Bold().FontSize(12);
                            inner.Item().Text($"• Type d'action : {intention.TypeIntention}");
                            
                            if (intention.DatePaiementPrevue.HasValue)
                                inner.Item().Text($"• Date de règlement prévue : {intention.DatePaiementPrevue.Value:dd/MM/yyyy}");
                            
                            if (intention.MontantPropose.HasValue)
                                inner.Item().Text($"• Montant proposé : {intention.MontantPropose.Value:F3} TND");

                            inner.Item().Text($"• Indice de confiance déclaré : {intention.ConfianceClient ?? 0}%");
                        });

                        col.Item().PaddingTop(20).Text("Informations Importantes :").Bold();
                        col.Item().Text("Cet accusé de réception atteste de votre volonté de régulariser votre situation, mais ne constitue pas une quittance de paiement ou une mainlevée. Votre dossier reste sous surveillance active jusqu'au règlement effectif des sommes dues.");

                        col.Item().PaddingTop(40).AlignRight().Column(sig => {
                            sig.Item().Text("Généré numériquement par le").FontSize(9);
                            sig.Item().Text("Moteur de Recouvrement STB").FontSize(9).Bold();
                            sig.Item().PaddingTop(10).AlignCenter().Width(80).Height(80).Background(Colors.Grey.Lighten3); // Simulation QR Code
                            sig.Item().AlignCenter().Text("Certifié conforme").FontSize(7).Italic();
                        });
                    });

                    page.Footer().AlignCenter().Column(f => {
                        f.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                        f.Item().PaddingTop(5).Text("Ceci est un document officiel généré par le système d'information de la STB BANK.").FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });
            });

            byte[] pdfBytes = document.GeneratePdf();
            return File(pdfBytes, "application/pdf", $"Accuse_Reception_{intention.IdIntention}.pdf");
        }

        // ==============================
        // GET api/client/historique-pdf/{token}/{idDossier}
        // ==============================
        [HttpGet("historique-pdf/{token}/{idDossier}")]
        public async Task<IActionResult> GenerateHistoriquePdf(string token, int idDossier)
        {
            var client = await _context.Clients
                .Include(c => c.Agence)
                .Include(c => c.Dossiers)
                    .ThenInclude(d => d.Echeances)
                .Include(c => c.Dossiers)
                    .ThenInclude(d => d.HistoriquePaiements)
                .Include(c => c.Dossiers)
                    .ThenInclude(d => d.Relances)
                .Include(c => c.Dossiers)
                    .ThenInclude(d => d.Communications)
                .FirstOrDefaultAsync(c => c.TokenAcces == token);

            if (client == null)
                return Unauthorized("Token invalide");

            var dossier = client.Dossiers.FirstOrDefault(d => d.IdDossier == idDossier);

            if (dossier == null)
                return NotFound("Dossier introuvable");

            int joursRetard = CalculerJoursRetard(dossier);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text("HISTORIQUE DU DOSSIER")
                                .FontSize(20).SemiBold().FontColor(Colors.Blue.Medium);
                            row.RelativeItem().AlignRight()
                                .Text($"Édité le {DateTime.Now:dd/MM/yyyy}");
                        });
                        col.Item().Text($"Client : {client.Nom} {client.Prenom}  |  Agence : {client.Agence?.Ville}");
                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });

                    page.Content().PaddingVertical(20).Column(col =>
                    {
                        col.Spacing(16);

                        col.Item().Background(Colors.Grey.Lighten4).Padding(12).Column(inner =>
                        {
                            inner.Item().Text("INFORMATIONS DU DOSSIER").Bold().FontSize(12);
                            inner.Item().Text($"Montant initial : {dossier.MontantInitial} TND");
                            inner.Item().Text($"Montant impayé : {dossier.MontantImpaye} TND");
                            inner.Item().Text($"Jours de retard : {joursRetard}");
                            inner.Item().Text($"Statut : {dossier.StatutDossier.ToUpper()}");
                            inner.Item().Text($"Type : {dossier.TypeEmprunt}");
                        });

                        col.Item().Text("ÉCHÉANCES").Bold().FontSize(12).FontColor(Colors.Blue.Medium);
                        foreach (var e in dossier.Echeances)
                        {
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Text($"{e.DateEcheance:dd/MM/yyyy}");
                                row.RelativeItem().Text($"{e.Montant} TND");
                                row.RelativeItem().Text(e.Statut.ToUpper());
                            });
                        }

                        col.Item().Text("PAIEMENTS").Bold().FontSize(12).FontColor(Colors.Blue.Medium);
                        foreach (var p in dossier.HistoriquePaiements)
                        {
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Text($"{p.DatePaiement:dd/MM/yyyy}");
                                row.RelativeItem().Text($"{p.MontantPaye} TND");
                                row.RelativeItem().Text(p.TypePaiement);
                            });
                        }

                        col.Item().Text("COMMUNICATIONS").Bold().FontSize(12).FontColor(Colors.Blue.Medium);
                        foreach (var c in dossier.Communications)
                        {
                            col.Item().Column(inner =>
                            {
                                inner.Item().Text($"{c.DateEnvoi:dd/MM/yyyy HH:mm} — {c.Origine.ToUpper()}")
                                    .FontSize(10).FontColor(Colors.Grey.Medium);
                                inner.Item().Text(c.Message);
                            });
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Document généré automatiquement — Page ");
                        x.CurrentPageNumber();
                    });
                });
            });

            byte[] pdfBytes = document.GeneratePdf();
            return File(pdfBytes, "application/pdf", $"Historique_Dossier_{idDossier}.pdf");
        }
    }
}