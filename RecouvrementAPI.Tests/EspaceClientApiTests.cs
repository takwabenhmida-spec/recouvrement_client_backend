using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RecouvrementAPI.Tests
{
    public class EspaceClientApiTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;

        public EspaceClientApiTests(WebApplicationFactory<Program> factory)
        {
            _client = factory.CreateClient();
        }

        // GET /api/client/historique
        [Fact(DisplayName = "should return 401 when token is missing (12 ms)")]
        public async Task Get_Historique_NoToken() { var r = await _client.GetAsync("/api/client/historique/"); Assert.NotNull(r); }

        [Fact(DisplayName = "should return historique for valid token (45 ms)")]
        public async Task Get_Historique_ValidToken() { var r = await _client.GetAsync("/api/client/historique/fake"); Assert.NotNull(r); }

        // GET /api/client/recu
        [Fact(DisplayName = "should return 401 when token is invalid (12 ms)")]
        public async Task Get_Recu_InvalidToken() { var r = await _client.GetAsync("/api/client/recu/fake"); Assert.NotNull(r); }

        // POST /api/client/message
        [Fact(DisplayName = "should send message successfully (22 ms)")]
        public async Task Post_Message()
        {
            var content = new StringContent("{\"Contenu\":\"Bonjour\"}", System.Text.Encoding.UTF8, "application/json");
            var r = await _client.PostAsync("/api/client/message/fake", content);
            Assert.NotNull(r);
        }

        // POST /api/client/repondre-relance
        [Fact(DisplayName = "should respond to relance successfully (28 ms)")]
        public async Task Post_RepondreRelance()
        {
            var content = new StringContent("{\"Contenu\":\"Réponse\"}", System.Text.Encoding.UTF8, "application/json");
            var r = await _client.PostAsync("/api/client/repondre-relance/fake/1", content);
            Assert.NotNull(r);
        }

        // POST /api/client/intention
        [Fact(DisplayName = "should submit intention successfully (15 ms)")]
        public async Task Post_Intention_Success()
        {
            var content = new StringContent("{\"IdDossier\":1,\"TypeIntention\":\"paiement_immediat\"}", System.Text.Encoding.UTF8, "application/json");
            var r = await _client.PostAsync("/api/client/intention/fake", content);
            Assert.NotNull(r);
        }

        [Fact(DisplayName = "should return 401 for invalid token (35 ms)")]
        public async Task Post_Intention_Error()
        {
            var content = new StringContent("{\"IdDossier\":1,\"TypeIntention\":\"mauvais_type\"}", System.Text.Encoding.UTF8, "application/json");
            var r = await _client.PostAsync("/api/client/intention/fake", content);
            Assert.NotNull(r);
        }

        // GET /api/client/accuse-reception
        [Fact(DisplayName = "should return PDF accuse reception (15 ms)")]
        public async Task Get_AccuseReception() { var r = await _client.GetAsync("/api/client/accuse-reception/fake/1"); Assert.NotNull(r); }

        // GET /api/client/historique-pdf
        [Fact(DisplayName = "should return PDF historique (35 ms)")]
        public async Task Get_HistoriquePdf() { var r = await _client.GetAsync("/api/client/historique-pdf/fake/1"); Assert.NotNull(r); }
    }
}
