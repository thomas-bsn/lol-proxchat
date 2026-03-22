using LoLProximityChat.Server.Services;
using Microsoft.AspNetCore.SignalR;

namespace LoLProximityChat.Server.Hubs
{
    public class ProximityHub : Hub
    {
        private readonly RoomService _rooms;

        public ProximityHub(RoomService rooms)
        {
            _rooms = rooms;
        }

        public async Task JoinGame(string gameId, string playerName)
        {
            // Récupère les joueurs déjà présents AVANT d'ajouter le nouveau
            var existing = _rooms.GetPlayerNames(gameId);

            await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
            _rooms.AddPlayer(Context.ConnectionId, playerName, gameId);

            // Notifie les AUTRES que ce joueur arrive (pas lui-même)
            await Clients.OthersInGroup(gameId).SendAsync("PlayerJoined", playerName);

            // Envoie au nouveau arrivant la liste des joueurs déjà là
            await Clients.Caller.SendAsync("ExistingPlayers", existing);

            Console.WriteLine($"[JOIN] {playerName} → room {gameId} | déjà présents : {string.Join(", ", existing)}");
        }

        public async Task UpdatePosition(string gameId, string playerName, float x, float y)
        {
            _rooms.UpdatePosition(gameId, playerName, x, y);
            Console.WriteLine($"[POS] {DateTime.Now:HH:mm:ss} {playerName} → ({x:F0}, {y:F0})");

            var volumeMap = _rooms.ComputeVolumes(gameId);
            foreach (var (_, (connId, volumes)) in volumeMap)
                await Clients.Client(connId).SendAsync("VolumesUpdated", volumes);
        }

        public async Task SendAudio(string gameId, string playerName, byte[] audioData)
        {
            Console.WriteLine($"[AUDIO] {playerName} → {audioData.Length} bytes");
            await Clients.OthersInGroup(gameId).SendAsync("ReceiveAudio", playerName, audioData);
        }

        public async Task RegisterEndpoint(string gameId, string playerName, string ip, int port)
        {
            await Clients.Group(gameId).SendAsync("PeerEndpoint", playerName, ip, port);
            Console.WriteLine($"[ENDPOINT] {playerName} → {ip}:{port}");
        }

        public async Task LeaveGame(string gameId, string playerName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameId);
            _rooms.RemovePlayer(Context.ConnectionId);
            await Clients.Group(gameId).SendAsync("PlayerLeft", playerName);
            Console.WriteLine($"[LEAVE] {playerName} ← room {gameId}");
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var playerName = _rooms.GetPlayerName(Context.ConnectionId);
            var gameId     = _rooms.GetGameId(Context.ConnectionId);

            _rooms.RemovePlayer(Context.ConnectionId);

            if (playerName != null && gameId != null)
                await Clients.Group(gameId).SendAsync("PlayerLeft", playerName);

            Console.WriteLine($"[DISCONNECT] {playerName ?? Context.ConnectionId}");
            await base.OnDisconnectedAsync(exception);
        }
    }
}