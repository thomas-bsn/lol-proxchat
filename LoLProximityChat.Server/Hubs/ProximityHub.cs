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
            await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
            _rooms.AddPlayer(Context.ConnectionId, playerName, gameId);
            await Clients.Group(gameId).SendAsync("PlayerJoined", playerName);
            Console.WriteLine($"[JOIN] {playerName} → room {gameId}");
        }

        public async Task UpdatePosition(string gameId, string playerName, float x, float y)
        {
            _rooms.UpdatePosition(gameId, playerName, x, y);
            Console.WriteLine($"[POS] {DateTime.Now:HH:mm:ss} {playerName} → ({x:F0}, {y:F0})");

            var volumeMap = _rooms.ComputeVolumes(gameId);
            foreach (var (_, (connId, volumes)) in volumeMap)
                await Clients.Client(connId).SendAsync("VolumesUpdated", volumes);
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
            _rooms.RemovePlayer(Context.ConnectionId);
            Console.WriteLine($"[DISCONNECT] {playerName ?? Context.ConnectionId}");
            await base.OnDisconnectedAsync(exception);
        }
    }
}