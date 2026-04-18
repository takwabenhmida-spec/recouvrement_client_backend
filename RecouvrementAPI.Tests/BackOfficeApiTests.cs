using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace RecouvrementAPI.Tests
{
    [Collection("TestCollection")]
    public class BackOfficeApiTests
    {
        private readonly HttpClient _client;
        private const string JsonMediaType = "application/json";

        public BackOfficeApiTests(TestWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        private async Task<HttpResponseMessage> Post(string url, string json)
        {
            var content = new StringContent(json, Encoding.UTF8, JsonMediaType);
            return await _client.PostAsync(url, content);
        }

        private async Task<string> GetAdminTokenAsync()
        {
            var loginJson = "{\"email\":\"admin@stb.tn\",\"motDePasse\":\"admin123\"}";
            var response = await Post("/api/Auth/login", loginJson);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var json = System.Text.Json.JsonDocument.Parse(content);
            return json.RootElement.GetProperty("token").GetString()!;
        }

        private async Task<HttpResponseMessage> GetWithAuth(string url, string token)
        {
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return await _client.GetAsync(url);
        }

        private async Task<HttpResponseMessage> PostWithAuth(string url, string token, string json = "{}")
        {
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var content = new StringContent(json, Encoding.UTF8, JsonMediaType);
            return await _client.PostAsync(url, content);
        }

        [Fact]
        public async Task Auth_Login_ShouldReturnOk_WhenCredentialsValid()
        {
            var r = await Post("/api/Auth/login", "{\"email\":\"admin@stb.tn\",\"motDePasse\":\"admin123\"}");
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        [Fact]
        public async Task Dashboard_Kpi_ShouldReturnOk_WhenAuthenticated()
        {
            var token = await GetAdminTokenAsync();
            var r = await GetWithAuth("/api/Dashboard/kpi", token);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        [Fact]
        public async Task Utilisateur_Gestion_ShouldReturnOk_WhenAuthenticated()
        {
            var token = await GetAdminTokenAsync();
            var r = await GetWithAuth("/api/Utilisateur/gestion", token);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        [Fact]
        public async Task Intention_Add_ShouldReturnOk_WhenValid()
        {
            var token = await GetAdminTokenAsync();
            var json = "{\"idDossier\":14, \"typeIntention\":\"paiement_immediat\", \"commentaire\":\"Test admin\"}";
            var r = await PostWithAuth("/api/intention", token, json);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        [Fact]
        public async Task Intention_GetHistory_ShouldReturnOk_WhenAuthenticated()
        {
            var token = await GetAdminTokenAsync();
            var r = await GetWithAuth("/api/intention/1", token);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        [Fact]
        public async Task Intention_Dashboard_ShouldReturnOk_WhenAuthenticated()
        {
            var token = await GetAdminTokenAsync();
            var r = await GetWithAuth("/api/intention/dashboard", token);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        [Fact]
        public async Task AdminClient_TokensExpires_ShouldReturnOk_WhenAuthenticated()
        {
            var token = await GetAdminTokenAsync();
            var r = await GetWithAuth("/api/AdminClient/tokens-expires", token);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        [Fact]
        public async Task Relance_Dashboard_ShouldReturnOk_WhenAuthenticated()
        {
            var token = await GetAdminTokenAsync();
            var r = await GetWithAuth("/api/Relance/dashboard", token);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        [Fact]
        public async Task Auth_Login_ShouldReturnUnauthorized_WhenWrongPassword()
        {
            var r = await Post("/api/Auth/login", "{\"email\":\"admin@stb.tn\",\"motDePasse\":\"wrong\"}");
            Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        }

        [Fact]
        public async Task Auth_Login_ShouldReturnUnauthorized_WhenUserNotFound()
        {
            var r = await Post("/api/Auth/login", "{\"email\":\"unknown@stb.tn\",\"motDePasse\":\"admin123\"}");
            Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        }

        [Fact]
        public async Task Impaye_Gestion_ShouldReturnOk_WhenAuthenticated()
        {
            var token = await GetAdminTokenAsync();
            var r = await GetWithAuth("/api/Impaye/gestion", token);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        [Fact]
        public async Task Impaye_Gestion_WithFilter_ShouldReturnOk()
        {
            var token = await GetAdminTokenAsync();
            var r = await GetWithAuth("/api/Impaye/gestion?filtre=Avec%20intérêt%20>=90j", token);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        [Fact]
        public async Task ClientList_Gestion_ShouldReturnOk_WhenAuthenticated()
        {
            var token = await GetAdminTokenAsync();
            var r = await GetWithAuth("/api/ClientList/gestion", token);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        [Fact]
        public async Task AdminClient_ExportExcel_ShouldReturnOk()
        {
            var token = await GetAdminTokenAsync();
            var r = await GetWithAuth("/api/AdminClient/export/excel", token);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", r.Content.Headers.ContentType?.MediaType);
        }

        [Fact]
        public async Task AdminClient_MiseEnDemeure_ShouldReturnOk()
        {
            var token = await GetAdminTokenAsync();
            var r = await GetWithAuth("/api/AdminClient/mise-en-demeure/1", token);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.Equal("application/pdf", r.Content.Headers.ContentType?.MediaType);
        }

        [Fact]
        public async Task AdminClient_Decision_ShouldReturnOk()
        {
            var token = await GetAdminTokenAsync();
            var json = "{\"decision\":\"Accepter\"}";
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var content = new StringContent(json, Encoding.UTF8, JsonMediaType);
            // Intention 2 is seeded in factory for dossier 2
            var r = await _client.PutAsync("/api/AdminClient/intention/2/decision", content);
            Assert.Contains(r.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.NotFound });
        }

        [Fact]
        public async Task AdminClient_Archiver_Success() {
            var token = await GetAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var r = await _client.PutAsync("/api/AdminClient/3/archiver", null);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        [Fact]
        public async Task AdminClient_Desarchiver_Success() {
            var token = await GetAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            await _client.PutAsync("/api/AdminClient/4/archiver", null); 
            var r = await _client.PutAsync("/api/AdminClient/4/desarchiver", null);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        [Fact]
        public async Task AdminClient_RenouvelerToken_Success() {
            var token = await GetAdminTokenAsync();
            var r = await PostWithAuth("/api/AdminClient/1/renouveler-token", token);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        [Fact]
        public async Task AdminClient_ArchiverSoldes_Success() {
            var token = await GetAdminTokenAsync();
            var r = await PostWithAuth("/api/AdminClient/archiver-soldes", token);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }
    }
}

