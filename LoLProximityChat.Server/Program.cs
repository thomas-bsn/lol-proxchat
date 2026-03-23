using LoLProximityChat.Server.Hubs;
using LoLProximityChat.Server.Services;

var builder = WebApplication.CreateBuilder(args);
var port = Environment.GetEnvironmentVariable("PORT") ?? "5128";

builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
builder.Services.AddSignalR();
builder.Services.AddSingleton<RoomService>();
builder.Services.AddHttpClient();

var app = builder.Build();

app.MapHub<ProximityHub>("/proximity");

// ── Endpoint OAuth Discord ─────────────────────────────────────────────────
app.MapPost("/auth/discord/token", async (HttpContext ctx, IHttpClientFactory factory) =>
{
    var bodyStr = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
    Console.WriteLine($"[DISCORD AUTH] Body reçu: {bodyStr}");
    
    var body = System.Text.Json.JsonSerializer.Deserialize<TokenRequest>(bodyStr,
        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    if (body?.Code is null)
    {
        Console.WriteLine("[DISCORD AUTH] Code manquant");
        return Results.BadRequest("code manquant");
    }
    
    Console.WriteLine($"[DISCORD AUTH] Code: {body.Code}");

    var clientId     = Environment.GetEnvironmentVariable("DISCORD_CLIENT_ID")
                       ?? builder.Configuration["Discord:ClientId"]!;
    var clientSecret = Environment.GetEnvironmentVariable("DISCORD_CLIENT_SECRET")
                       ?? builder.Configuration["Discord:ClientSecret"]!;
    var redirectUri  = Environment.GetEnvironmentVariable("DISCORD_REDIRECT_URI")
                       ?? builder.Configuration["Discord:RedirectUri"]
                       ?? "http://localhost";

    var http = factory.CreateClient();
    var response = await http.PostAsync("https://discord.com/api/oauth2/token",
        new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"]    = "authorization_code",
            ["code"]          = body.Code,
            ["redirect_uri"]  = redirectUri,
            ["client_id"]     = clientId,
            ["client_secret"] = clientSecret,
        }));

    if (!response.IsSuccessStatusCode)
    {
        var err = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"[DISCORD AUTH] Erreur: {err}");
        return Results.Problem("Échange token échoué");
    }

    var json        = await response.Content.ReadAsStringAsync();
    var tokenResult = System.Text.Json.JsonDocument.Parse(json);
    var accessToken = tokenResult.RootElement
        .GetProperty("access_token").GetString();

    Console.WriteLine("[DISCORD AUTH] Token obtenu avec succès");
    return Results.Ok(new { access_token = accessToken });
});


app.Run();

record TokenRequest(string? Code);