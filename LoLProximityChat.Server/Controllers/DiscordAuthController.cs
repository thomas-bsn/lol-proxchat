using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace LoLProximityChat.Server.Controllers
{
    [ApiController]
    [Route("auth/discord")]
    public class DiscordAuthController : ControllerBase
    {
        private const string TokenUrl    = "https://discord.com/api/oauth2/token";
        private readonly IConfiguration  _config;
        private readonly ILogger<DiscordAuthController> _logger;

        public DiscordAuthController(
            IConfiguration config,
            ILogger<DiscordAuthController> logger)
        {
            _config = config;
            _logger = logger;
        }

        [HttpPost("token")]
        public async Task<IActionResult> ExchangeToken([FromBody] TokenRequest request)
        {
            var clientId     = _config["Discord:ClientId"];
            var clientSecret = _config["Discord:ClientSecret"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                return StatusCode(500, "Discord credentials non configurés");

            using var http = new HttpClient();
            var redirectUri = _config["Discord:RedirectUri"];

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id",     clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("grant_type",    "authorization_code"),
                new KeyValuePair<string, string>("code",          request.Code),
                new KeyValuePair<string, string>("redirect_uri", redirectUri ?? "")
            });

            var response = await http.PostAsync(TokenUrl, form);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("[Discord Auth] Échange échoué : {Error}", error);
                return BadRequest("Échange token Discord échoué");
            }

            var json        = await response.Content.ReadAsStringAsync();
            var tokenData   = System.Text.Json.JsonDocument.Parse(json);
            var accessToken = tokenData.RootElement.GetProperty("access_token").GetString();

            _logger.LogInformation("[Discord Auth] Token obtenu ✓");
            return Ok(new { access_token = accessToken });
        }
    }

    public record TokenRequest(string Code);
}