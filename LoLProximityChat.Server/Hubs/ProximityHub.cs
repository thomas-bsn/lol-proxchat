using Microsoft.AspNetCore.SignalR;

namespace LoLProximityChat.Server.Hubs
{
    public class ProximityHub : Hub
    {
        // Stocke les positions de tous les joueurs par room
        private static readonly Dictionary<string, Dictionary<string, (float x, float y)>> _rooms = new();
        private static readonly object _lock = new();

        public async Task JoinGame(string gameId, string playerName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, gameId);

            lock (_lock)
            {
                if (!_rooms.ContainsKey(gameId))
                    _rooms[gameId] = new();
            }

            await Clients.Group(gameId).SendAsync("PlayerJoined", playerName);
            Console.WriteLine($"[JOIN] {playerName} → room {gameId}");
        }

        public async Task UpdatePosition(string gameId, string playerName, float x, float y)
        {
            // Mettre à jour la position du joueur
            lock (_lock)
            {
                if (!_rooms.ContainsKey(gameId))
                    _rooms[gameId] = new();
                _rooms[gameId][playerName] = (x, y);
            }

            // Calculer et envoyer les volumes à tous les joueurs de la room
            Dictionary<string, (float x, float y)> positions;
            lock (_lock)
            {
                if (!_rooms.TryGetValue(gameId, out var room)) return;
                positions = new Dictionary<string, (float x, float y)>(room);
            }

            // Pour chaque joueur dans la room, calculer ses volumes
            foreach (var listener in positions)
            {
                var volumes = new Dictionary<string, float>();

                foreach (var speaker in positions)
                {
                    if (speaker.Key == listener.Key) continue;
                    volumes[speaker.Key] = CalculateVolume(listener.Value, speaker.Value);
                }

                // Envoyer les volumes au listener
                await Clients.Group(gameId).SendAsync("VolumesUpdated", listener.Key, volumes);
            }

            Console.WriteLine($"[POS] {playerName} → ({x:F0}, {y:F0})");
        }

        public async Task LeaveGame(string gameId, string playerName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameId);

            lock (_lock)
            {
                if (_rooms.TryGetValue(gameId, out var room))
                {
                    room.Remove(playerName);
                    if (room.Count == 0)
                        _rooms.Remove(gameId);
                }
            }

            await Clients.Group(gameId).SendAsync("PlayerLeft", playerName);
            Console.WriteLine($"[LEAVE] {playerName} ← room {gameId}");
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"[DISCONNECT] {Context.ConnectionId}");
            await base.OnDisconnectedAsync(exception);
        }

        private static float CalculateVolume(
            (float x, float y) listener,
            (float x, float y) speaker)
        {
            const float maxRange = 3000f;

            var distance = MathF.Sqrt(
                MathF.Pow(listener.x - speaker.x, 2) +
                MathF.Pow(listener.y - speaker.y, 2));

            if (distance >= maxRange) return 0f;

            var volume = 1f - (distance / maxRange);
            return MathF.Pow(volume, 2);
        }
    }
}