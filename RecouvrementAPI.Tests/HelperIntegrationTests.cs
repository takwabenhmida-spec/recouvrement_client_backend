using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using RecouvrementAPI.Data;
using RecouvrementAPI.Helpers;
using RecouvrementAPI.Models;
using Xunit;

namespace RecouvrementAPI.Tests
{
    public class HelperIntegrationTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly TestWebApplicationFactory _factory;

        public HelperIntegrationTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task VerifierRetard3Mois_ShouldNotAddCommunication_WhenRetardIsLessOrEqual90Days()
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                
                // Create a dossier with 10 days of delay
                var dossier = new DossierRecouvrement
                {
                    IdDossier = 100,
                    IdClient = TestWebApplicationFactory.SeedClientId,
                    StatutDossier = "amiable",
                    Echeances = new List<Echeance>
                    {
                        new Echeance { Statut = "impaye", DateEcheance = DateTime.UtcNow.AddDays(-10) }
                    }
                };

                int initialCommCount = context.Communications.Count(c => c.IdDossier == 100);
                
                await RecouvrementHelper.VerifierRetard3Mois(dossier, context);
                
                int finalCommCount = context.Communications.Count(c => c.IdDossier == 100);
                Assert.Equal(initialCommCount, finalCommCount);
            }
        }

        [Fact]
        public async Task VerifierRetard3Mois_ShouldAddCommunication_WhenRetardIsOver90Days()
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                
                // Create a dossier with 100 days of delay
                var dossier = new DossierRecouvrement
                {
                    IdDossier = 101,
                    IdClient = TestWebApplicationFactory.SeedClientId,
                    StatutDossier = "amiable",
                    MontantImpaye = 1500m,
                    Echeances = new List<Echeance>
                    {
                        new Echeance { Statut = "impaye", DateEcheance = DateTime.UtcNow.AddDays(-100) }
                    }
                };
                context.Dossiers.Add(dossier);
                await context.SaveChangesAsync();

                await RecouvrementHelper.VerifierRetard3Mois(dossier, context);
                
                var comm = context.Communications.FirstOrDefault(c => c.IdDossier == 101 && c.Origine == AppConstants.SystemActor);
                Assert.NotNull(comm);
                Assert.Contains("retard de 100 jours", comm.Message);
            }
        }

        [Fact]
        public async Task VerifierRetard3Mois_ShouldNotAddDuplicateCommunication_InSameMonth()
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                
                var dossier = new DossierRecouvrement
                {
                    IdDossier = 102,
                    IdClient = TestWebApplicationFactory.SeedClientId,
                    StatutDossier = "amiable",
                    Echeances = new List<Echeance>
                    {
                        new Echeance { Statut = "impaye", DateEcheance = DateTime.UtcNow.AddDays(-120) }
                    }
                };
                context.Dossiers.Add(dossier);
                
                // Add an existing communication from 5 days ago
                context.Communications.Add(new Communication
                {
                    IdDossier = 102,
                    Message = "Existing alerte",
                    Origine = AppConstants.SystemActor,
                    DateEnvoi = DateTime.UtcNow.AddDays(-5)
                });
                await context.SaveChangesAsync();

                int commCountBefore = context.Communications.Count(c => c.IdDossier == 102);
                
                await RecouvrementHelper.VerifierRetard3Mois(dossier, context);
                
                int commCountAfter = context.Communications.Count(c => c.IdDossier == 102);
                Assert.Equal(commCountBefore, commCountAfter);
            }
        }
    }
}
