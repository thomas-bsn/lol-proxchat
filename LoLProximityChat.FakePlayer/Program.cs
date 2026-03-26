using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

var serverUrl  = "http://localhost:5128";
var roomId     = "testroom123";
var playerId   = "Joueur2";

// 1. Rejoindre la room via HTTP
using var http = new HttpClient { BaseAddress = new Uri(serverUrl) };
var joinResponse = await http.PostAsJsonAsync("/room/join", new { roomId, playerId });
joinResponse.EnsureSuccessStatusCode();

var body  = await joinResponse.Content.ReadFromJsonAsync<JoinRoomResponse>();
var token = body!.Token;
Console.WriteLine($"[OK] Room rejointe — token={token}");

// 2. Connexion WebSocket
var ws  = new ClientWebSocket();
var uri = new Uri($"ws://localhost:5128/room/ws?token={token}");
await ws.ConnectAsync(uri, CancellationToken.None);
Console.WriteLine("[OK] WebSocket connecté");

// 3. Boucle de réception en background
_ = Task.Run(async () =>
{
    var buffer = new byte[4096];
    while (ws.State == WebSocketState.Open)
    {
        var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
        if (result.MessageType == WebSocketMessageType.Close) break;
        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
        Console.WriteLine($"[VOLUME] {json}");
    }
});

// 4. Position initiale
float x = 7000f, y = 4480f;

Console.WriteLine("Commandes : +/- (x), W/S (y), M (midlane), F (far), Q (quit)");

var cts = new CancellationTokenSource();

// 5. Envoi position toutes les secondes
_ = Task.Run(async () =>
{
    while (!cts.Token.IsCancellationRequested)
    {
        var payload = JsonSerializer.Serialize(new { playerId, x, y });
        var bytes   = Encoding.UTF8.GetBytes(payload);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        Console.Write($"\r[POS] ({x:F0}, {y:F0})          ");
        await Task.Delay(1000, cts.Token);
    }
}, cts.Token);

// 6. Contrôles clavier
while (true)
{
    var key = Console.ReadKey(intercept: true).Key;
    switch (key)
    {
        case ConsoleKey.OemPlus:  x += 500; break;
        case ConsoleKey.OemMinus: x -= 500; break;
        case ConsoleKey.W:        y += 500; break;
        case ConsoleKey.S:        y -= 500; break;
        case ConsoleKey.M: x = 7000; y = 7000; Console.WriteLine("\n[MOVE] Même position que Joueur1"); break;
        case ConsoleKey.F: x = 500;  y = 500;  Console.WriteLine("\n[MOVE] Far (hors range)");          break;
        case ConsoleKey.Q:
            cts.Cancel();
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Quit", CancellationToken.None);
            Console.WriteLine("\n[OK] Déconnecté");
            return;
    }
}

record JoinRoomResponse(string Token);