namespace LoLProximityChat.Server.Services
{
    public class RoomService
    {
        private readonly Dictionary<string, Dictionary<string, (float x, float y)>> _rooms = new();
        private readonly Dictionary<string, string> _connectionToPlayer = new();
        private readonly Dictionary<string, string> _playerToConnection = new();
        private readonly object _lock = new();

        public void AddPlayer(string connectionId, string playerName, string gameId)
        {
            lock (_lock)
            {
                if (!_rooms.ContainsKey(gameId))
                    _rooms[gameId] = new();
                _connectionToPlayer[connectionId] = playerName;
                _playerToConnection[playerName]   = connectionId;
            }
        }

        public void RemovePlayer(string connectionId)
        {
            lock (_lock)
            {
                if (!_connectionToPlayer.TryGetValue(connectionId, out var playerName)) return;
                _connectionToPlayer.Remove(connectionId);
                _playerToConnection.Remove(playerName);

                foreach (var room in _rooms.Values)
                    room.Remove(playerName);

                foreach (var emptyRoom in _rooms.Where(r => r.Value.Count == 0).ToList())
                    _rooms.Remove(emptyRoom.Key);
            }
        }

        public string? GetPlayerName(string connectionId)
        {
            lock (_lock)
            {
                _connectionToPlayer.TryGetValue(connectionId, out var name);
                return name;
            }
        }

        public void UpdatePosition(string gameId, string playerName, float x, float y)
        {
            lock (_lock)
            {
                if (!_rooms.ContainsKey(gameId))
                    _rooms[gameId] = new();
                _rooms[gameId][playerName] = (x, y);
            }
        }

        // Retourne les volumes pour chaque listener de la room
        public Dictionary<string, (string connectionId, Dictionary<string, float> volumes)> ComputeVolumes(string gameId)
        {
            Dictionary<string, (float x, float y)> positions;
            Dictionary<string, string> connections;

            lock (_lock)
            {
                if (!_rooms.TryGetValue(gameId, out var room)) return new();
                positions   = new(room);
                connections = new(_playerToConnection);
            }

            var result = new Dictionary<string, (string, Dictionary<string, float>)>();

            foreach (var listener in positions)
            {
                if (!connections.TryGetValue(listener.Key, out var connId)) continue;

                var volumes = new Dictionary<string, float>();
                foreach (var speaker in positions)
                {
                    if (speaker.Key == listener.Key) continue;
                    volumes[speaker.Key] = CalculateVolume(listener.Value, speaker.Value);
                }

                result[listener.Key] = (connId, volumes);
            }

            return result;
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