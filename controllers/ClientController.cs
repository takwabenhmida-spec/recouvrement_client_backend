using Microsoft.AspNetCore.Mvc;
using RecouvrementAPI;
using Microsoft.EntityFrameworkCore;
using RecouvrementAPI.Data;
using RecouvrementAPI.DTOs;
using RecouvrementAPI.Models;
using RecouvrementAPI.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace RecouvrementAPI.Controllers
{
    [ApiController]
    [Route("api/client")]
    public class ClientController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ClientController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==============================
        // MÉTHODE PRIVÉE : Chargement complet du client
        // Centralise TOUS les Include/ThenInclude pour éviter la duplication.
        // Utilisée par toutes les méthodes qui ont besoin des sous-relations.
        // Vérifie aussi l'expiration du token.
        // ==============================
        private async Task<Client> ChargerClientComplet(string token)
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

            if (client == null) return null;

            // Vérification expiration du token (7 jours)
            if (client.TokenExpireLe.HasValue && client.TokenExpireLe.Value < DateTime.UtcNow)
                return null;

            return client;
        }

        // ==============================
        // MÉTHODE PRIVÉE : Résoudre le dossier cible
        // • idDossier fourni  → ce dossier précis (anti-IDOR)
        // • idDossier null    → dossier le plus récent (comportement par défaut)
        // ==============================
        private static DossierRecouvrement ResoudreDossier(Client client, int? idDossier)
        {
            if (idDossier.HasValue && client.Dossiers != null)
                return client.Dossiers.FirstOrDefault(d => d.IdDossier == idDossier.Value);

            return client.Dossiers
                .OrderByDescending(d => d.DateCreation)
                .FirstOrDefault();
        }

        // ==============================
        // MÉTHODE PRIVÉE : Mapper DossierRecouvrement → DossierDto
        // ==============================
        private DossierDto MapDossierToDto(DossierRecouvrement dossier)
        {
            int joursRetard = RecouvrementHelper.CalculerJoursRetard(dossier.Echeances);

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

                MontantInterets   = RecouvrementHelper.CalculerInteretsRetard(dossier.MontantImpaye, dossier.TauxInteret, joursRetard),
                NombreJoursRetard = joursRetard,

                DateEcheance = dossier.Echeances
                    .OrderBy(e => e.DateEcheance)
                    .Select(e => e.DateEcheance)
                    .FirstOrDefault(),

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

                Relances = dossier.Relances.Select(r => new RelanceDto
                {
                    IdRelance   = r.IdRelance,
                    DateRelance = r.DateRelance,
                    Moyen       = r.Moyen,
                    Statut      = r.Statut,
                    Contenu     = r.Contenu,
                }).ToList(),

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
        // Retourne TOUS les dossiers du client en JSON.
        // ==============================
        [HttpGet("historique/{token?}")]
        public async Task<IActionResult> GetHistorique(string token)
        {
            if (string.IsNullOrEmpty(token))
                return Unauthorized("Token manquant");

            var client = await ChargerClientComplet(token);

            if (client == null)
                return Unauthorized(AppConstants.TokenInvalid);

            var dossierPrincipal = client.Dossiers
                .OrderByDescending(d => d.DateCreation)
                .FirstOrDefault();

            if (dossierPrincipal != null)
            {
                _context.HistoriqueActions.Add(new HistoriqueAction
                {
                    IdDossier    = dossierPrincipal.IdDossier,
                    ActionDetail = $"Accès client via token UUID — IP : {HttpContext.Connection.RemoteIpAddress}",
                    Acteur       = AppConstants.ClientActor,
                    DateAction   = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }

            foreach (var dossier in client.Dossiers)
            {
                await RecouvrementHelper.VerifierRetard3Mois(dossier, _context);
            }

            var dto = new ClientHistoriqueDto
            {
                NomComplet  = client.Nom + " " + client.Prenom,
                IdAgence    = client.Agence != null ? client.Agence.IdAgence : 0,
                VilleAgence = client.Agence?.Ville,

                Dossiers = client.Dossiers
                    .OrderByDescending(d => d.DateCreation)
                    .Select(dossier => MapDossierToDto(dossier))
                    .ToList()
            };

            return Ok(dto);
        }

        // ==============================
        // GET api/client/dossier/{token}/{idDossier}
        // Retourne UN SEUL dossier (anti-IDOR).
        // ==============================
        [HttpGet("dossier/{token}/{idDossier}")]
        public async Task<IActionResult> GetDossierPrecis(string token, int idDossier)
        {
            if (string.IsNullOrEmpty(token))
                return Unauthorized("Token manquant");

            var client = await ChargerClientComplet(token);

            if (client == null)
                return Unauthorized(AppConstants.TokenInvalid);

            var dossier = client.Dossiers.FirstOrDefault(d => d.IdDossier == idDossier);

            if (dossier == null)
                return NotFound($"Dossier {idDossier} introuvable ou n'appartient pas à ce client.");

            _context.HistoriqueActions.Add(new HistoriqueAction
            {
                IdDossier    = dossier.IdDossier,
                ActionDetail = $"Accès client au dossier #{idDossier} — IP : {HttpContext.Connection.RemoteIpAddress}",
                Acteur       = AppConstants.ClientActor,
                DateAction   = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return Ok(MapDossierToDto(dossier));
        }

        // ==============================
        // GET api/client/recu/{token}
        // GET api/client/recu/{token}?idDossier=42
        //
        // Génère et retourne le PDF du reçu de situation.
        // RESPONSABILITÉ BACK UNIQUEMENT — le front se contente d'ouvrir l'URL.
        // ==============================
        [HttpGet("recu/{token}")]
        public async Task<IActionResult> GenerateRecu(string token, [FromQuery] int? idDossier = null)
        {
            // 1. Chargement unique — inclut Echeances, pas besoin de 2ème appel DB
            var client = await ChargerClientComplet(token);
            if (client == null)
                return Unauthorized(AppConstants.TokenInvalid);

            // 2. Sélection du dossier (cible ou le plus récent)
            var dossier = ResoudreDossier(client, idDossier);
            if (dossier == null)
                return NotFound(AppConstants.DossierNotFound);

            // 3. Calculs métier
            int joursRetard       = RecouvrementHelper.CalculerJoursRetard(dossier.Echeances);
            decimal montantPaye   = dossier.MontantInitial - dossier.MontantImpaye;
            decimal montantInterets = RecouvrementHelper.CalculerInteretsRetard(dossier.MontantImpaye, dossier.TauxInteret, joursRetard);
            decimal totalARegler  = dossier.MontantImpaye + montantInterets;

            // 4. Couleur selon statut
            string colorHex = Colors.Blue.Medium;
            if (dossier.StatutDossier == "regularise") colorHex = Colors.Green.Medium;
            else if (dossier.StatutDossier == "contentieux") colorHex = Colors.Red.Medium;

            // 5. Génération PDF (responsabilité exclusive du back)
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(50);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("REÇU DE SITUATION").FontSize(22).SemiBold().FontColor(Colors.Blue.Medium);
                            col.Item().Text($"Dossier n° {dossier.IdDossier}").FontSize(10);
                        });
                        row.RelativeItem().AlignRight().Text($"STB BANK - {client.Agence?.Ville}").Bold();
                    });

                    page.Content().PaddingVertical(25).Column(col =>
                    {
                        col.Spacing(10);
                        col.Item().Text($"Client : {client.Nom} {client.Prenom}").Bold();

                        col.Item().Text(text => {
                            text.Span("Retard constaté : ").Bold();
                            text.Span($"{joursRetard} jours")
                                .FontColor(joursRetard > 0 ? Colors.Red.Medium : Colors.Green.Medium)
                                .Bold();
                        });

                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        col.Item().Text($"Type de crédit : {dossier.TypeEmprunt}");
                        col.Item().Text($"Montant initial : {dossier.MontantInitial:F3} TND");

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

                        col.Item().PaddingTop(15).Background(Colors.Grey.Lighten4).Padding(15).Column(inner =>
                        {
                            inner.Item().Text("Montant à payer").FontSize(11).Bold();
                            inner.Item().Text($"{totalARegler:F3} TND")
                                .FontSize(28).Bold().FontColor(colorHex);
                        });
                    });

                    page.Footer().AlignCenter().Text($"Document généré le {DateTime.UtcNow:dd/MM/yyyy HH:mm}");
                });
            });

            byte[] pdfBytes = document.GeneratePdf();
            return File(pdfBytes, "application/pdf", $"Recu_STB_Dossier_{dossier.IdDossier}.pdf");
        }

        // ==============================
        // GET api/client/historique-pdf/{token}/{idDossier}
        //
        // Génère et retourne le PDF de l'historique complet d'un dossier.
        // RESPONSABILITÉ BACK UNIQUEMENT — le front se contente d'ouvrir l'URL.
        // ==============================
        [HttpGet("historique-pdf/{token}/{idDossier}")]
        public async Task<IActionResult> GenerateHistoriquePdf(string token, int idDossier)
        {
            // Chargement unique via ChargerClientComplet — supprime le bloc Include dupliqué
            var client = await ChargerClientComplet(token);
            if (client == null)
                return Unauthorized(AppConstants.TokenInvalid);

            var dossier = client.Dossiers.FirstOrDefault(d => d.IdDossier == idDossier);
            if (dossier == null)
                return NotFound(AppConstants.DossierNotFound);

            int joursRetard = RecouvrementHelper.CalculerJoursRetard(dossier.Echeances);

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
                                .Text($"Édité le {DateTime.UtcNow:dd/MM/yyyy}");
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

        // ==============================
        // GET api/client/accuse-reception/{token}/{idIntention}
        // Génère le PDF officiel d'accusé de réception.
        // ==============================
        [HttpGet("accuse-reception/{token}/{idIntention}")]
        public async Task<IActionResult> GenerateAccuseReception(string token, int idIntention)
        {
            var client = await _context.Clients
                .Include(c => c.Agence)
                .Include(c => c.Dossiers)
                    .ThenInclude(d => d.Intentions)
                .FirstOrDefaultAsync(c => c.TokenAcces == token);

            if (client == null)
                return Unauthorized(AppConstants.TokenInvalid);

            if (client.TokenExpireLe.HasValue && client.TokenExpireLe.Value < DateTime.UtcNow)
                return Unauthorized(AppConstants.TokenInvalid);

            var intention = client.Dossiers
                .SelectMany(d => d.Intentions)
                .FirstOrDefault(i => i.IdIntention == idIntention);

            if (intention == null)
                return NotFound("Accusé de réception introuvable.");

            var dossier = client.Dossiers.First(d => d.IdDossier == intention.IdDossier);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(50);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("ACCUSÉ DE RÉCEPTION").FontSize(24).ExtraBold().FontColor(Colors.Blue.Medium);
                            col.Item().Text("SOCIÉTÉ TUNISIENNE DE BANQUE").FontSize(10).SemiBold();
                            col.Item().Text($"Réf : ACK-INT-{intention.IdIntention:D5}").FontSize(9).Italic();
                        });

                        row.ConstantItem(100).AlignRight().Column(col => {
                            col.Item().Height(40).Background(Colors.Blue.Medium);
                            col.Item().AlignCenter().Text("STB BANK").FontSize(8);
                        });
                    });

                    page.Content().PaddingVertical(30).Column(col =>
                    {
                        col.Spacing(15);

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

                        col.Item().PaddingTop(10).Text(text => {
                            text.Span("Objet : ").Bold();
                            text.Span($"Confirmation de réception d'intention de {intention.TypeIntention.Replace("_", " ")}.");
                        });

                        col.Item().Text($"Nous confirmons avoir reçu votre déclaration le {intention.DateIntention:dd/MM/yyyy} à {intention.DateIntention:HH:mm} concernant votre dossier de crédit n°{dossier.IdDossier}.");

                        col.Item().Background(Colors.Grey.Lighten4).Padding(15).Column(inner => {
                            inner.Spacing(5);
                            inner.Item().Text("Récapitulatif de votre déclaration :").Bold().FontSize(12);
                            inner.Item().Text($"• Type d'action : {intention.TypeIntention}");
                            if (intention.DatePaiementPrevue.HasValue)
                                inner.Item().Text($"• Date de règlement prévue : {intention.DatePaiementPrevue.Value:dd/MM/yyyy}");
                            if (intention.MontantPropose.HasValue)
                                inner.Item().Text($"• Montant proposé : {intention.MontantPropose.Value:F3} TND");
                        });

                        col.Item().PaddingTop(20).Text("Informations Importantes :").Bold();
                        col.Item().Text("Cet accusé de réception atteste de votre volonté de régulariser votre situation, mais ne constitue pas une quittance de paiement ou une mainlevée. Votre dossier reste sous surveillance active jusqu'au règlement effectif des sommes dues.");

                        col.Item().PaddingTop(40).AlignRight().Column(sig => {
                            sig.Item().Text("Généré numériquement par le").FontSize(9);
                            sig.Item().Text("Moteur de Recouvrement STB").FontSize(9).Bold();
                            sig.Item().PaddingTop(10).AlignCenter().Width(80).Height(80).Background(Colors.Grey.Lighten3);
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
        // POST api/client/message/{token}
        // Client envoie un message libre à son agence.
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
                return Unauthorized(AppConstants.TokenInvalid);

            var dossier = ResoudreDossier(client, idDossier);
            if (dossier == null)
                return NotFound(AppConstants.DossierNotFound);

            _context.Communications.Add(new Communication
            {
                IdDossier = dossier.IdDossier,
                IdRelance = null,
                Message   = messageDto.Contenu.Trim(),
                Origine   = AppConstants.ClientActor,
                DateEnvoi = DateTime.UtcNow
            });

            _context.HistoriqueActions.Add(new HistoriqueAction
            {
                IdDossier    = dossier.IdDossier,
                ActionDetail = $"Client a envoyé un message : \"{messageDto.Contenu.Trim()}\"",
                Acteur       = AppConstants.ClientActor,
                DateAction   = DateTime.UtcNow
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
                return Unauthorized(AppConstants.TokenInvalid);

            // Anti-IDOR : la relance doit appartenir à un dossier du client
            var dossier = client.Dossiers
                .FirstOrDefault(d => d.Relances.Any(r => r.IdRelance == idRelance));

            if (dossier == null)
                return NotFound("Relance introuvable ou n'appartient pas à ce client.");

            var relance = dossier.Relances.First(r => r.IdRelance == idRelance);
            relance.Statut = AppConstants.RelanceStatut.Replied;

            _context.Communications.Add(new Communication
            {
                IdDossier = dossier.IdDossier,
                IdRelance = idRelance,
                Message   = reponseDto.Contenu.Trim(),
                Origine   = AppConstants.ClientActor,
                DateEnvoi = DateTime.UtcNow
            });

            _context.HistoriqueActions.Add(new HistoriqueAction
            {
                IdDossier    = dossier.IdDossier,
                ActionDetail = $"Client a répondu à la relance #{idRelance}",
                Acteur       = AppConstants.ClientActor,
                DateAction   = DateTime.UtcNow
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

    }
}