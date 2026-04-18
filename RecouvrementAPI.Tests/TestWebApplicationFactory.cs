using System;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RecouvrementAPI.Data;
using RecouvrementAPI.Models;
using RecouvrementAPI.Helpers;

namespace RecouvrementAPI.Tests
{
    public class TestWebApplicationFactory : WebApplicationFactory<Program>
    {
        public const string ValidClientToken = "valid_token_123";
        public const string ExpiredClientToken = "expired_token_456";
        public const int SeedClientId = 1;
        public const int SeedAgenceId = 1;
        public const int SeedDossierId = 1;
        public const int SeedDossierId2 = 2;
        public const int SeedDossierId3 = 3;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                // In case any other DbContext registration exists (defensive)
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase("InMemoryDbForTesting");
                });

                var sp = services.BuildServiceProvider();

                using (var scope = sp.CreateScope())
                {
                    var scopedServices = scope.ServiceProvider;
                    var db = scopedServices.GetRequiredService<ApplicationDbContext>();
                    var logger = scopedServices.GetRequiredService<ILogger<TestWebApplicationFactory>>();

                    db.Database.EnsureDeleted();
                    db.Database.EnsureCreated();

                    try
                    {
                        Seed(db);
                        db.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "An error occurred seeding the database with test messages. Error: {Message}", ex.Message);
                    }
                }
            });
        }

        private static string? _cachedAdminHash;

        private static void Seed(ApplicationDbContext db)
        {
            if (db.Agences.Any()) return;

            var agence = new Agence { IdAgence = SeedAgenceId, Ville = "Tunis" };
            db.Agences.Add(agence);

            db.Clients.Add(new Client
            {
                IdClient      = SeedClientId,
                IdAgence      = SeedAgenceId,
                Nom           = "Client",
                Prenom        = "Normal",
                Email         = "client@stb.tn",
                TokenAcces    = ValidClientToken,
                TokenExpireLe = DateTime.UtcNow.AddDays(7),
                Statut        = "actif"
            });

            db.Clients.Add(new Client
            {
                IdClient      = 2,
                IdAgence      = SeedAgenceId,
                Nom           = "Client",
                Prenom        = "Expired",
                Email         = "expired@stb.tn",
                TokenAcces    = ExpiredClientToken,
                TokenExpireLe = DateTime.UtcNow.AddDays(-1),
                Statut        = "actif"
            });

            db.Clients.Add(new Client
            {
                IdClient      = 3,
                IdAgence      = SeedAgenceId,
                Nom           = "Client-3",
                Prenom        = "Test",
                Email         = "c3@stb.tn",
                TokenAcces    = "token-3",
                TokenExpireLe = DateTime.UtcNow.AddDays(7),
                Statut        = "actif"
            });

            db.Clients.Add(new Client
            {
                IdClient      = 4,
                IdAgence      = SeedAgenceId,
                Nom           = "Client-4",
                Prenom        = "Test",
                Email         = "c4@stb.tn",
                TokenAcces    = "token-4",
                TokenExpireLe = DateTime.UtcNow.AddDays(7),
                Statut        = "Archivé"
            });

            for (int i = 1; i <= 25; i++)
            {
                db.Dossiers.Add(new DossierRecouvrement
                {
                    IdDossier      = i,
                    IdClient       = SeedClientId,
                    TypeEmprunt    = i % 2 == 0 ? "Leasing" : "Credit",
                    MontantInitial = 1000m * i,
                    MontantImpaye  = 500m  * i,
                    FraisDossier   = 0m,
                    TauxInteret    = 10m,
                    StatutDossier  = AppConstants.DossierStatut.Amiable,
                    DateCreation   = DateTime.UtcNow.AddDays(-i)
                });

                db.Echeances.Add(new Echeance
                {
                    IdDossier    = i,
                    Montant      = 100m * i,
                    // Dossier 2 aura un retard de >100 jours pour tester les intérêts
                    DateEcheance = DateTime.UtcNow.AddDays(-(i == 2 ? 110 : 10)),
                    Statut       = AppConstants.EcheanceStatut.Impaye
                });

                if (i <= 2)
                {
                    db.HistoriquePaiements.Add(new HistoriquePaiement
                    {
                        IdDossier    = i,
                        MontantPaye  = 200m * i,
                        DatePaiement = DateTime.UtcNow.AddDays(-20),
                        TypePaiement = "virement"
                    });

                    db.HistoriqueActions.Add(new HistoriqueAction
                    {
                        IdDossier    = i,
                        ActionDetail = "Action de test",
                        Acteur       = "systeme",
                        DateAction   = DateTime.UtcNow.AddDays(-5)
                    });
                }

                if (i <= 2)
                {
                    db.Intentions.Add(new IntentionClient
                    {
                        IdDossier          = i,
                        TypeIntention      = i == 1 ? "paiement_partiel" : "promesse_paiement",
                        DateIntention      = DateTime.UtcNow.AddDays(-1),
                        MontantPropose     = i == 1 ? (decimal?)(100m * i) : null,
                        DatePaiementPrevue = i == 2 ? (DateTime?)DateTime.UtcNow.AddDays(10) : null,
                        Statut             = "En attente"
                    });

                    db.Garanties.Add(new Garantie
                    {
                        IdDossier     = i,
                        Description   = "Garantie Test",
                        TypeGarantie  = i == 1 ? "hypotheque" : "salaire"
                    });
                }

                db.ScoresRisque.Add(new ScoreRisque
                {
                    IdDossier        = i,
                    ScoreTotal       = 30 + (i * 5),
                    PointsRetard     = 20,
                    PointsHistorique = 10,
                    PointsGarantie   = 5,
                    PointsIntention  = 0,
                    Niveau           = (30 + (i * 5)) > 60 ? "Élevé" : "Moyen",
                    DateCalcul       = DateTime.UtcNow
                });

                if (i == 1 || i == 3 || i == 4)
                {
                    db.Relances.Add(new RelanceClient
                    {
                        IdRelance   = i,
                        IdDossier   = i,
                        DateRelance = DateTime.UtcNow.AddDays(-2),
                        Moyen       = AppConstants.RelanceMoyen.Email,
                        Statut      = AppConstants.RelanceStatut.Sent,
                        Contenu     = $"Relance dossier {i}"
                    });
                }

                if (i == 3)
                {
                    db.Communications.Add(new Communication
                    {
                        IdDossier = i,
                        IdRelance = i,
                        Message   = "Communication liée à relance",
                        Origine   = AppConstants.SystemActor,
                        DateEnvoi = DateTime.UtcNow.AddDays(-1)
                    });
                }
            }

            _cachedAdminHash ??= BCrypt.Net.BCrypt.HashPassword("admin123");

            db.UtilisateursBack.Add(new UtilisateurBack
            {
                IdAgent    = 1,
                IdAgence   = SeedAgenceId,
                Nom        = "Admin",
                Prenom     = "User",
                Email      = "admin@stb.tn",
                MotDePasse = _cachedAdminHash,
                Role       = "Admin",
                Statut     = "actif"
            });
        }
    }
}
