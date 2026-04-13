using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecouvrementAPI.Data;
using RecouvrementAPI.DTOs;
using RecouvrementAPI.Models;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace RecouvrementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AdminClientController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminClientController> _logger;

        public AdminClientController(ApplicationDbContext context, ILogger<AdminClientController> logger)
        {
            _context = context;
            _logger = logger;
        }

       
        /// Ajout d'un nouveau client STB avec un dossier par défaut.
        [HttpPost]
        public async Task<IActionResult> CreateClient([FromBody] CreateClientDto dto)
        {
            try
            {
                if (await _context.Clients.AnyAsync(c => c.CIN == dto.CIN))
                    return BadRequest(new { message = "Un client avec ce CIN existe déjà." });

                if (await _context.Clients.AnyAsync(c => c.Email == dto.Email))
                    return BadRequest(new { message = "Un client avec cet Email existe déjà." });

                var client = new Client
                {
                    Nom = dto.Nom,
                    Prenom = dto.Prenom,
                    CIN = dto.CIN,
                    Adresse = dto.Adresse,
                    Email = dto.Email,
                    Telephone = dto.Telephone,
                    IdAgence = dto.IdAgence ?? 1, // Direction Générale par défaut si non spécifié
                    TokenAcces = "stb_" + Guid.NewGuid().ToString("N"),
                    TokenExpireLe = DateTime.Now.AddDays(7)
                };

                _context.Clients.Add(client);
                await _context.SaveChangesAsync();

                // Création optionnelle du premier dossier de recouvrement
                if (dto.PremierDossier != null)
                {
                    var dossier = new DossierRecouvrement
                    {
                        IdClient = client.IdClient,
                        MontantInitial = dto.PremierDossier.MontantInitial,
                        MontantImpaye = dto.PremierDossier.MontantInitial,
                        TypeEmprunt = dto.PremierDossier.TypeEmprunt,
                        TauxInteret = dto.PremierDossier.TauxInteret,
                        StatutDossier = dto.PremierDossier.StatutDossier , 
                        DateCreation = DateTime.Now
                    };
                    _context.Dossiers.Add(dossier);
                    await _context.SaveChangesAsync();
                }

                return Ok(new { message = "Client créé avec succès.", idClient = client.IdClient });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création client.");
                return StatusCode(500, new { message = "Erreur lors de la création du client." });
            }
        }

        /// <summary>
        /// Export Excel de tous les dossiers impayés pour la comptabilité STB.
        /// </summary>
        [HttpGet("export/excel")]
        public async Task<IActionResult> ExportExcel()
        {
            try
            {
                var dossiers = await _context.Dossiers
                    .Include(d => d.Client)
                        .ThenInclude(c => c.Agence)
                    .Include(d => d.Echeances)
                    .Where(d => d.MontantImpaye > 0)
                    .ToListAsync();

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Dossiers Impayés STB");

                    // En-tête
                    worksheet.Cell(1, 1).Value = "ID Dossier";
                    worksheet.Cell(1, 2).Value = "Client";
                    worksheet.Cell(1, 3).Value = "CIN";
                    worksheet.Cell(1, 4).Value = "Type Crédit";
                    worksheet.Cell(1, 5).Value = "Montant Initial";
                    worksheet.Cell(1, 6).Value = "Montant Impayé";
                    worksheet.Cell(1, 7).Value = "Retard (Jours)";
                    worksheet.Cell(1, 8).Value = "Statut";
                    worksheet.Cell(1, 9).Value = "Agence";

                    var headerRange = worksheet.Range("A1:I1");
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.Navy;
                    headerRange.Style.Font.FontColor = XLColor.White;

                    int row = 2;
                    foreach (var d in dossiers)
                    {
                        int retard = (int)(DateTime.Now - (d.Echeances.Where(e => e.Statut == "impaye").Min(e => (DateTime?)e.DateEcheance) ?? DateTime.Now)).TotalDays;
                        if (retard < 0) retard = 0;

                        worksheet.Cell(row, 1).Value = d.IdDossier;
                        worksheet.Cell(row, 2).Value = $"{d.Client.Nom} {d.Client.Prenom}";
                        worksheet.Cell(row, 3).Value = d.Client.CIN;
                        worksheet.Cell(row, 4).Value = d.TypeEmprunt;
                        worksheet.Cell(row, 5).Value = d.MontantInitial;
                        worksheet.Cell(row, 6).Value = d.MontantImpaye;
                        worksheet.Cell(row, 7).Value = retard;
                        worksheet.Cell(row, 8).Value = d.StatutDossier;
                        worksheet.Cell(row, 9).Value = d.Client.Agence?.Ville ?? "Siège";

                        // Couleur si retard critique (> 90 jours)
                        if (retard > 90)
                        {
                            worksheet.Cell(row, 7).Style.Font.FontColor = XLColor.Red;
                            worksheet.Cell(row, 7).Style.Font.Bold = true;
                        }

                        row++;
                    }

                    worksheet.Columns().AdjustToContents();

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();
                        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Impayes_STB_{DateTime.Now:yyyyMMdd}.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur export Excel.");
                return StatusCode(500, new { message = "Erreur lors de la génération du fichier Excel." });
            }
        }

        /// <summary>
        /// Génération d'une lettre de "Mise en Demeure" pour les dossiers critiques.
        /// </summary>
        [HttpGet("mise-en-demeure/{idDossier}")]
        public async Task<IActionResult> GenerateMiseEnDemeure(int idDossier)
        {
            try
            {
                var dossier = await _context.Dossiers
                    .Include(d => d.Client)
                        .ThenInclude(c => c.Agence)
                    .FirstOrDefaultAsync(d => d.IdDossier == idDossier);

                if (dossier == null) return NotFound();

                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(50);
                        page.Size(PageSizes.A4);
                        page.DefaultTextStyle(x => x.FontSize(12).FontFamily("Times New Roman"));

                        page.Header().Column(col =>
                        {
                            col.Item().Text("SOCIÉTÉ TUNISIENNE DE BANQUE (STB)").FontSize(16).Bold().FontColor(Colors.Blue.Medium);
                            col.Item().Text($"Agence : {dossier.Client.Agence?.Ville ?? "Direction Générale"}").FontSize(10);
                            col.Item().PaddingVertical(10).LineHorizontal(1);
                        });

                        page.Content().PaddingVertical(30).Column(col =>
                        {
                            col.Item().AlignRight().Text($"{dossier.Client.Agence?.Ville ?? "Tunis"}, le {DateTime.Now:dd/MM/yyyy}").Italic();
                            
                            col.Item().PaddingTop(20).Column(dest => {
                                dest.Item().Text("À l'attention de :").Bold();
                                dest.Item().Text($"{dossier.Client.Nom} {dossier.Client.Prenom}");
                                dest.Item().Text($"{dossier.Client.Adresse}");
                                dest.Item().Text($"CIN: {dossier.Client.CIN}");
                            });

                            col.Item().PaddingVertical(30).AlignCenter().Text("OBJET : MISE EN DEMEURE AVANT POURSUITES JUDICIAIRES").FontSize(14).Bold().Underline();

                            col.Item().PaddingVertical(10).Text(text => {
                                text.Span("Monsieur/Madame, \n\nSauf erreur ou omission de notre part, votre dossier de crédit ");
                                text.Span($"n° {dossier.IdDossier} ({dossier.TypeEmprunt})").Bold();
                                text.Span(" accuse à ce jour un impayé de ");
                                text.Span($"{dossier.MontantImpaye:F3} TND").Bold().FontColor(Colors.Red.Medium);
                                text.Span(" au titre du principal.");
                            });

                            col.Item().Text("Malgré nos relances précédentes, nous constatons que vous n'avez toujours pas régularisé votre situation.");

                            col.Item().PaddingTop(10).Text("En conséquence, PAR LA PRÉSENTE, LA STB BANK VOUS MET EN DEMEURE de nous régler ladite somme sous un délai de 48 heures à compter de la réception de la présente.");

                            col.Item().PaddingTop(10).Text("À défaut de règlement intégral dans ce délai, nous serons contraints de transmettre votre dossier à notre département contentieux pour engagement de poursuites judiciaires, dont les frais seront à votre charge exclusive.");

                            col.Item().PaddingTop(30).AlignRight().Column(sig => {
                                sig.Item().Text("Le Responsable d'Agence").Bold();
                                sig.Item().PaddingTop(40).Text("_____________________");
                                sig.Item().Text("(Signature et Cachet)");
                            });
                        });

                        page.Footer().AlignCenter().Text(x => {
                            x.Span("STB Bank - Le Partenaire de votre réussite - Page ");
                            x.CurrentPageNumber();
                        });
                    });
                });

                byte[] pdfBytes = document.GeneratePdf();
                return File(pdfBytes, "application/pdf", $"Mise_En_Demeure_{dossier.IdDossier}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur PDF Mise en demeure.");
                return StatusCode(500, new { message = "Erreur lors de la génération de la lettre juridique." });
            }
        }

        /// <summary>
        /// Vérifie et archive automatiquement les clients qui ont payé la totalité de leurs dettes.
        /// Un client est archivé si TOUS ses dossiers sont au statut 'regularise'.
        /// </summary>
        [HttpPost("archiver-soldes")]
        public async Task<IActionResult> ArchiverClientsSoldes()
        {
            try
            {
                var clients = await _context.Clients
                    .Include(c => c.Dossiers)
                    .Where(c => c.Statut != "Archivé")
                    .ToListAsync();

                int archivesCount = 0;

                foreach (var client in clients)
                {
                    if (client.Dossiers != null && client.Dossiers.Any())
                    {
                        bool toutSolder = client.Dossiers.All(d => d.StatutDossier == "regularise" || d.MontantImpaye <= 0);
                        if (toutSolder)
                        {
                            client.Statut = "Archivé";
                            archivesCount++;
                        }
                    }
                }

                if (archivesCount > 0)
                    await _context.SaveChangesAsync();

                return Ok(new { 
                    message = $"{archivesCount} client(s) ont été archivés avec succès car leurs comptes sont soldés.",
                    count = archivesCount 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'archivage automatique.");
                return StatusCode(500, new { message = "Une erreur est survenue lors de l'archivage." });
            }
        }

        /// <summary>
        /// 🟡 BOUTON "ARCHIVER" DE LA MAQUETTE — Archive UN seul client par son ID.
        /// Appelé quand l'agent clique sur le bouton "Archiver" sur la fiche client.
        /// Route API : PUT http://localhost:5203/api/AdminClient/{idClient}/archiver
        /// </summary>
        [HttpPut("{idClient}/archiver")]
        public async Task<IActionResult> ArchiverClient(int idClient)
        {
            try
            {
                var client = await _context.Clients
                    .Include(c => c.Dossiers)
                    .FirstOrDefaultAsync(c => c.IdClient == idClient);

                if (client == null)
                    return NotFound(new { message = "Client introuvable." });

                if (client.Statut == "Archivé")
                    return BadRequest(new { message = "Ce client est déjà archivé." });

                client.Statut = "Archivé";
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = $"Le client {client.Nom} {client.Prenom} a été archivé avec succès.",
                    idClient = client.IdClient,
                    statut = client.Statut
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors de l'archivage du client {idClient}.");
                return StatusCode(500, new { message = "Une erreur est survenue lors de l'archivage." });
            }
        }

        /// <summary>
        /// 🔵 BOUTON "DÉSARCHIVER" — Réactive un client archivé par son ID.
        /// Route API : PUT http://localhost:5203/api/AdminClient/{idClient}/desarchiver
        /// </summary>
        [HttpPut("{idClient}/desarchiver")]
        public async Task<IActionResult> DesarchiverClient(int idClient)
        {
            try
            {
                var client = await _context.Clients.FindAsync(idClient);

                if (client == null)
                    return NotFound(new { message = "Client introuvable." });

                if (client.Statut != "Archivé")
                    return BadRequest(new { message = "Ce client n'est pas archivé." });

                client.Statut = "Actif";
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = $"Le client {client.Nom} {client.Prenom} a été réactivé avec succès.",
                    idClient = client.IdClient,
                    statut = client.Statut
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors de la désarchivation du client {idClient}.");
                return StatusCode(500, new { message = "Une erreur est survenue." });
            }
        }

        /// <summary>
        /// 🔴 Liste les clients dont le token d'accès est EXPIRÉ.
        /// L'admin utilise cette liste pour savoir à qui renvoyer un nouveau token.
        /// Route API : GET http://localhost:5203/api/AdminClient/tokens-expires
        /// </summary>
        [HttpGet("tokens-expires")]
        public async Task<IActionResult> GetClientsTokenExpires()
        {
            try
            {
                var maintenant = DateTime.Now;

                // Chargement en base (EF Core) — comparaison directe sur nullable
                var clientsRaw = await _context.Clients
                    .Include(c => c.Dossiers)
                    .Where(c =>
                        c.Statut != "Archivé" &&
                        c.TokenExpireLe.HasValue &&
                        c.TokenExpireLe.Value < maintenant)
                    .ToListAsync();

                // Projection en mémoire (C#) — TotalDays n'est pas traduisible en SQL
                var clientsExpires = clientsRaw
                    .Select(c => new
                    {
                        idClient      = c.IdClient,
                        nomComplet    = c.Nom + " " + c.Prenom,
                        email         = c.Email,
                        telephone     = c.Telephone,
                        tokenExpireLe = c.TokenExpireLe,
                        joursExpire   = (int)(maintenant - c.TokenExpireLe!.Value).TotalDays,
                        nbDossiers    = c.Dossiers?.Count ?? 0
                    })
                    .OrderByDescending(c => c.joursExpire)
                    .ToList();

                return Ok(new
                {
                    total   = clientsExpires.Count,
                    clients = clientsExpires
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des tokens expirés.");
                return StatusCode(500, new { message = "Erreur lors de la récupération." });
            }
        }

        /// <summary>
        /// 🔁 RENOUVELLEMENT MANUEL DU TOKEN CLIENT par l'admin.
        /// Génère un nouveau token et remet l'expiration à +7 jours.
        /// L'admin doit ensuite envoyer le lien manuellement au client (SMS ou email).
        /// Route API : POST http://localhost:5203/api/AdminClient/{idClient}/renouveler-token
        /// </summary>
        [HttpPost("{idClient}/renouveler-token")]
        public async Task<IActionResult> RenouvelerTokenClient(int idClient)
        {
            try
            {
                var client = await _context.Clients.FindAsync(idClient);

                if (client == null)
                    return NotFound(new { message = "Client introuvable." });

                if (client.Statut == "Archivé")
                    return BadRequest(new { message = "Impossible de renouveler le token d'un client archivé." });

                // Génération d'un nouveau token UUID unique
                string nouveauToken = "stb_" + Guid.NewGuid().ToString("N");

                client.TokenAcces    = nouveauToken;
                client.TokenExpireLe = DateTime.Now.AddDays(7);

                await _context.SaveChangesAsync();

                // Lien à envoyer au client
                string lien = $"https://stbbank.tn/portail/{nouveauToken}";

                _logger.LogInformation($"Token renouvelé manuellement pour le client {client.IdClient} ({client.Nom} {client.Prenom}). Nouveau lien : {lien}");

                return Ok(new
                {
                    message      = $"Token renouvelé avec succès pour {client.Nom} {client.Prenom}.",
                    lienClient   = lien,
                    tokenGenere  = nouveauToken,
                    expireLe     = client.TokenExpireLe,
                    // Message prêt à copier-coller pour l'envoi SMS ou email
                    messageSms   = $"[STB BANK] Cher(e) {client.Nom}, accédez à votre espace client sécurisé : {lien} (valable 7 jours)",
                    messageEmail = $"Bonjour {client.Nom} {client.Prenom},\n\nVotre accès à l'espace client STB a été renouvelé.\nCliquez sur le lien suivant pour accéder à votre dossier :\n{lien}\n\nCe lien est valable 7 jours.\n\nCordialement,\nSTB BANK"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur renouvellement token client {idClient}.");
                return StatusCode(500, new { message = "Erreur lors du renouvellement du token." });
            }
        }

        /// <summary>
        /// ✅ L'ADMIN/AGENT ACCEPTE OU REFUSE UNE INTENTION
        /// Endpoint ajouté ici pour éviter de modifier le IntentionController.
        /// Route API : PUT http://localhost:5203/api/AdminClient/intention/{id}/decision
        /// </summary>
        [HttpPut("intention/{id}/decision")]
        public async Task<IActionResult> MakeDecision(int id, [FromBody] IntentionDecisionDto decisionDto)
        {
            try
            {
                var intention = await _context.Intentions.FindAsync(id);
                if (intention == null)
                    return NotFound(new { message = "Intention introuvable." });

                if (decisionDto.Decision != "Accepter" && decisionDto.Decision != "Refuser")
                    return BadRequest(new { message = "La décision doit être 'Accepter' ou 'Refuser'." });

                intention.Statut = decisionDto.Decision == "Accepter" ? "Accepté" : "Refusé";

                _context.HistoriqueActions.Add(new HistoriqueAction
                {
                    IdDossier    = intention.IdDossier,
                    ActionDetail = $"[ADMIN] L'intention (ID: {id}) de type '{intention.TypeIntention}' a été {intention.Statut.ToLower()}.",
                    Acteur       = "agent",
                    DateAction   = DateTime.Now
                });

                await _context.SaveChangesAsync();

                return Ok(new { 
                    message = $"Intention traitée par l'admin ({intention.Statut}).",
                    idIntention = id,
                    nouveauStatut = intention.Statut
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors de la décision sur l'intention {id}.");
                return StatusCode(500, new { message = "Impossible d'appliquer la décision." });
            }
        }
    }
}
