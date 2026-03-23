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

        public async Task JoinGame(string gameId, string playerName, string discordUsername)
        {
            var existing       = _rooms.GetPlayerNames(gameId);
            var discordMapping = _rooms.GetDiscordMapping(gameId);

            await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
            _rooms.AddPlayer(Context.ConnectionId, playerName, gameId, discordUsername);

            // Notifie les autres
            await Clients.OthersInGroup(gameId).SendAsync("PlayerJoined", playerName, discordUsername);

            // Envoie au nouveau : joueurs présents + leur mapping Discord
            await Clients.Caller.SendAsync("ExistingPlayers", existing);
            await Clients.Caller.SendAsync("DiscordMapping", discordMapping);

            // Envoie à tout le monde le mapping mis à jour (le nouveau vient d'arriver)
            var fullMapping = _rooms.GetDiscordMapping(gameId);
            await Clients.Group(gameId).SendAsync("DiscordMapping", fullMapping);

            Console.WriteLine($"[JOIN] {playerName} ({discordUsername}) → room {gameId} | déjà présents : {string.Join(", ", existing)}");
        }

        public async Task UpdatePosition(string gameId, string playerName, float x, float y)
        {
            _rooms.UpdatePosition(gameId, playerName, x, y);

            var volumeMap = _rooms.ComputeVolumes(gameId);
            foreach (var (_, (connId, volumes)) in volumeMap)
                await Clients.Client(connId).SendAsync("VolumesUpdated", volumes);
        }

        public async Task SendAudio(string gameId, string playerName, byte[] audioData)
        {
            await Clients.OthersInGroup(gameId).SendAsync("ReceiveAudio", playerName, audioData);
        }

        public async Task RegisterEndpoint(string gameId, string playerName, string ip, int port)
        {
            await Clients.Group(gameId).SendAsync("PeerEndpoint", playerName, ip, port);
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