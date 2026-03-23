using Microsoft.AspNetCore.SignalR.Client;

var serverUrl = "http://localhost:5128/proximity";
var gameId    = "A84FC360"; // <- même valeur à mettre dans ton vrai client
var playerName = "FakePlayer#TEST";
var discordUsername = "sz_kayr"; // un nom visible dans tes logs VoiceMembers

var connection = new HubConnectionBuilder()
    .WithUrl(serverUrl)
    .WithAutomaticReconnect()
    .Build();

connection.On<Dictionary<string, float>>("VolumesUpdated", volumes =>
{
    foreach (var (player, vol) in volumes)
        Console.WriteLine($"[VOLUME] {player} -> {vol * 100:F0}%");
});

connection.On<string, string>("PlayerJoined", (name, discord) =>
    Console.WriteLine($"[EVENT] PlayerJoined: {name}"));

connection.On<string>("PlayerLeft", name =>
    Console.WriteLine($"[EVENT] PlayerLeft: {name}"));

await connection.StartAsync();
Console.WriteLine($"[OK] Connecté — connectionId={connection.ConnectionId}");

await connection.InvokeAsync("JoinGame", gameId, playerName, discordUsername);
Console.WriteLine($"[OK] JoinGame envoyé → room {gameId}");

// Positions à tester — modifie x/y pour simuler différentes distances
float x = 7227f, y = 7400;

Console.WriteLine("Entée pour bouger, Q pour quitter");
Console.WriteLine("Commandes : +/- (x), */_ (y), M (midlane), F (far), Q (quit)");

var cts = new CancellationTokenSource();

// Envoie la position toutes les secondes en background
_ = Task.Run(async () =>
{
    while (!cts.Token.IsCancellationRequested)
    {
        await connection.InvokeAsync("UpdatePosition", gameId, playerName, x, y);
        Console.Write($"\r[POS] ({x:F0}, {y:F0})                    ");
        await Task.Delay(1000, cts.Token);
    }
}, cts.Token);

while (true)
{
    var key = Console.ReadKey(intercept: true).Key;
    switch (key)
    {
        case ConsoleKey.OemPlus: x += 500; break;
        case ConsoleKey.OemMinus: x -= 500; break;
        case ConsoleKey.M: x = 10312; y = 1619; Console.WriteLine("\n[MOVE] Midlane (même position que toi)"); break;
        case ConsoleKey.F: x = 4000;  y = 4000; Console.WriteLine("\n[MOVE] Far (hors range)"); break;
        case ConsoleKey.Q:
            cts.Cancel();
            await connection.InvokeAsync("LeaveGame", gameId, playerName);
            await connection.StopAsync();
            Console.WriteLine("\n[OK] Déconnecté");
            return;
    }
}