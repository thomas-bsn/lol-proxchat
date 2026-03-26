using Microsoft.Extensions.Logging;

namespace LoLProximityChat.Server.Services
{
    public class RoomService
    {
        private readonly Dictionary<string, Dictionary<string, (float x, float y)>> _rooms        = new();
        private readonly Dictionary<string, string> _connectionToPlayer  = new();
        private readonly Dictionary<string, string> _playerToConnection  = new();
        private readonly Dictionary<string, string> _connectionToGame    = new();
        private readonly Dictionary<string, string> _playerToDiscord     = new();
        private readonly object _lock = new();
        private readonly ILogger<RoomService> _logger;

        public RoomService(ILogger<RoomService> logger)
        {
            _logger = logger;
        }

        public void AddPlayer(string connectionId, string playerName, string gameId, string discordUsername)
        {
            lock (_lock)
            {
                if (!_rooms.ContainsKey(gameId))
                {
                    _rooms[gameId] = new();
                    _logger.LogInformation("[Room] Room {GameId} créée", gameId);
                }

                _connectionToPlayer[connectionId] = playerName;
                _playerToConnection[playerName]   = connectionId;
                _connectionToGame[connectionId]   = gameId;
                _playerToDiscord[playerName]      = discordUsername;
                _rooms[gameId][playerName]        = (0f, 0f);

                _logger.LogInformation("[Room] {Player} ({Discord}) a rejoint {GameId}", playerName, discordUsername, gameId);
            }
        }

        public void RemovePlayer(string connectionId)
        {
            lock (_lock)
            {
                if (!_connectionToPlayer.TryGetValue(connectionId, out var playerName)) return;

                _connectionToPlayer.Remove(connectionId);
                _playerToConnection.Remove(playerName);
                _connectionToGame.Remove(connectionId);
                _playerToDiscord.Remove(playerName);

                foreach (var room in _rooms.Values)
                    room.Remove(playerName);

                var emptyRooms = _rooms.Where(r => r.Value.Count == 0).ToList();
                foreach (var emptyRoom in emptyRooms)
                {
                    _rooms.Remove(emptyRoom.Key);
                    _logger.LogInformation("[Room] Room {GameId} supprimée (vide)", emptyRoom.Key);
                }

                _logger.LogInformation("[Room] {Player} a quitté", playerName);
            }
        }

        public void UpdatePosition(string gameId, string playerName, float x, float y)
        {
            lock (_lock)
            {
                if (!_rooms.ContainsKey(gameId)) return;
                _rooms[gameId][playerName] = (x, y);
            }
        }

        public Dictionary<string, (float x, float y)> GetPositions(string gameId)
        {
            lock (_lock)
            {
                if (!_rooms.TryGetValue(gameId, out var room)) return new();
                return new(room);
            }
        }

        public string? GetConnectionId(string playerName)
        {
            lock (_lock)
            {
                _playerToConnection.TryGetValue(playerName, out var connId);
                return connId;
            }
        }

        public string? GetDiscordUsername(string playerName)
        {
            lock (_lock)
            {
                _playerToDiscord.TryGetValue(playerName, out var discord);
                return discord;
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

        public string? GetGameId(string connectionId)
        {
            lock (_lock)
            {
                _connectionToGame.TryGetValue(connectionId, out var gameId);
                return gameId;
            }
        }
    }
}