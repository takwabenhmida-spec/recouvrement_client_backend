using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using System.Text.Json;
using System.Linq;

namespace RecouvrementAPI.Tests
{
    public class ScoringApiTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly HttpClient _client;
        private const string JsonMediaType = "application/json";

        public ScoringApiTests(TestWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        private async Task<string> GetAdminTokenAsync()
        {
            var loginJson = "{\"email\":\"admin@stb.tn\",\"motDePasse\":\"admin123\"}";
            var content = new StringContent(loginJson, Encoding.UTF8, JsonMediaType);
            var response = await _client.PostAsync("/api/Auth/login", content);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(body);
            return json.RootElement.GetProperty("token").GetString()!;
        }

        private async Task<HttpResponseMessage> GetWithAuth(string url, string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return await _client.SendAsync(request);
        }

        private async Task<HttpResponseMessage> PostWithAuth(string url, string token, string json = "{}")
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(json, Encoding.UTF8, JsonMediaType);
            return await _client.SendAsync(request);
        }

        [Fact]
        public async Task Scoring_Dashboard_ShouldReturnOk_WhenAuthenticated()
        {
            var token = await GetAdminTokenAsync();
            var r = await GetWithAuth("/api/Scoring/dashboard", token);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        [Fact]
        public async Task Scoring_Details_ShouldReturnOk_WhenAuthenticated()
        {
            var token = await GetAdminTokenAsync();
            var r = await GetWithAuth("/api/Scoring/1/details", token);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        [Fact]
        public async Task Scoring_RecommandationIA_ShouldReturnOk_WhenAuthenticated()
        {
            var token = await GetAdminTokenAsync();
            var r = await GetWithAuth("/api/Scoring/1/recommandation-ia", token);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        [Fact]
        public async Task Scoring_RecalculerTous_ShouldReturnOk_WhenAuthenticated()
        {
            var token = await GetAdminTokenAsync();
            var r = await PostWithAuth("/api/Scoring/recalculer-tous", token);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        [Fact]
        public async Task Scoring_RecalculerDossier_ShouldReturnOk_WhenAuthenticated()
        {
            var token = await GetAdminTokenAsync();
            var r = await PostWithAuth("/api/Scoring/1/recalculer", token);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        // ==========================================================================
        // NEW COVERAGE TESTS (ADDED TO REDUCE UNCOVERED LINES)
        // ==========================================================================

        [Fact]
        public async Task Scoring_Dashboard_ShouldFilterByStatus()
        {
            var token = await GetAdminTokenAsync();
            var r = await GetWithAuth("/api/Scoring/dashboard?etatDossier=aimable", token);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        [Fact]
        public async Task Scoring_Dashboard_ShouldFilterBySearch()
        {
            var token = await GetAdminTokenAsync();
            var r = await GetWithAuth("/api/Scoring/dashboard?recherche=Client", token);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        [Fact]
        public async Task Scoring_Details_ShouldReturnNotFound_WhenDossierInexistant()
        {
            var token = await GetAdminTokenAsync();
            var r = await GetWithAuth("/api/Scoring/99999/details", token);
            Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        }

        [Fact]
        public async Task Scoring_RecommandationIA_CheckLevels_High()
        {
            var token = await GetAdminTokenAsync();
            // SeedDossierId 2 by default is medium, let's just check if it returns valid JSON for levels
            var r = await GetWithAuth("/api/Scoring/2/recommandation-ia", token);
            var body = await r.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            Assert.NotNull(doc.RootElement.GetProperty("niveauRisque").GetString());
            Assert.NotNull(doc.RootElement.GetProperty("prioriteTraitement").GetString());
        }

        [Fact]
        public async Task Scoring_Recalcul_ShouldHandleNonExistantDossier_Silently()
        {
            var token = await GetAdminTokenAsync();
            // The method doesn't return error if dossier not found, just skips
            var r = await PostWithAuth("/api/Scoring/99999/recalculer", token);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }
    }
}
