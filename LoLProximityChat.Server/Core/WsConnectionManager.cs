using System.Net.WebSockets;

namespace LoLProximityChat.Server.Services
{
    public class WsConnectionManager
    {
        private readonly Dictionary<string, WebSocket> _sockets = new();
        private readonly object _lock = new();

        public void Add(string connectionId, WebSocket ws)
        {
            lock (_lock) _sockets[connectionId] = ws;
        }

        public void Remove(string connectionId)
        {
            lock (_lock) _sockets.Remove(connectionId);
        }

        public WebSocket? GetWebSocket(string connectionId)
        {
            lock (_lock)
            {
                _sockets.TryGetValue(connectionId, out var ws);
                return ws;
            }
        }
    }
}