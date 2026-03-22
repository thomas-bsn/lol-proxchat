using LoLProximityChat.Core.Audio;
using LoLProximityChat.Core.Models;
using LoLProximityChat.Core.Services;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace LoLProximityChat.WPF.ViewModels
{
    public class GameSessionViewModel : IAsyncDisposable
    {
        private readonly SignalRClient _signalR;
        private readonly ProximityCalculator _proximity = new();
        private readonly AppConfig _config = AppConfig.Load();
        private readonly PositionTracker _tracker = new();
        private readonly VoiceChatService _voice = new();
        private readonly UdpAudioTransport _udp;
        private readonly AudioViewModel _audio;
        public event Action<bool>? OnServerConnectionChanged;

        private readonly Dictionary<string, IPEndPoint> _peerEndpoints = new();

        private string _currentGameId   = "";
        private string _localPlayerName = "";
        private bool   _isConnected     = false;

        public GameSessionViewModel(SignalRClient signalR, AudioViewModel audio)
        {
            _signalR = signalR;
            _audio   = audio;
            _audio.MuteMicRequested += muted => _voice.IsMuted = muted;
            _udp     = new UdpAudioTransport(listenPort: 7777);

            _voice.OnAudioCaptured += async data =>
            {
                foreach (var (_, endpoint) in _peerEndpoints)
                    await _udp.SendAsync(data, _localPlayerName, endpoint);
            };

            _udp.OnAudioReceived += (playerName, data) =>
                _voice.ReceiveAudio(playerName, data);

            _signalR.OnVolumesUpdated += volumes =>
            {
                _voice.UpdateVolumes(volumes);
                _audio.UpdateVolumes(volumes);
            };

            _signalR.OnPlayerJoined += playerName =>
            {
                if (playerName != _localPlayerName)
                {
                    _voice.AddPlayer(playerName);
                    _audio.AddPlayer(playerName);
                }
            };

            _signalR.OnPlayerLeft += playerName =>
            {
                _voice.RemovePlayer(playerName);
                _audio.RemovePlayer(playerName);
                _peerEndpoints.Remove(playerName);
            };

            _signalR.OnPeerEndpoint += (playerName, ip, port) =>
            {
                if (playerName != _localPlayerName)
                    RegisterPeer(playerName, ip, port);
            };
            
            _signalR.OnConnectionChanged += connected => 
                OnServerConnectionChanged?.Invoke(connected);

        }

        public async Task OnStateAsync(GameState state)
        {
            if (!_isConnected)
            {
                _isConnected = true;
                await _signalR.ConnectAsync();
                await Task.Delay(500);
            }

            if (_currentGameId == "" && state.LocalPlayerName != "")
            {
                _localPlayerName = state.LocalPlayerName;
                _currentGameId   = GenerateGameId(state.Players);
                await _signalR.JoinGameAsync(_currentGameId, state.LocalPlayerName);

                var publicIp = await GetPublicIpAsync();
                await _signalR.RegisterEndpointAsync(_currentGameId, _localPlayerName, publicIp, 7777);

                _voice.Start(_audio.SelectedInputIndex, _audio.SelectedOutputIndex);
                _udp.Start();
            }

            var capture  = new MinimapCapture(new MinimapRegion
            {
                X      = _config.MinimapX,
                Y      = _config.MinimapY,
                Width  = _config.MinimapSize,
                Height = _config.MinimapSize
            });
            var blobs    = capture.DetectBlobs();
            var positions = _proximity.MapBlobsToPlayers(blobs, state.Players);

            var localPos = positions.FirstOrDefault(p => p.SummonerName == _localPlayerName);
            if (localPos?.IsVisible == true)
            {
                var stable = _tracker.TryUpdate(localPos.X, localPos.Y);
                if (stable.HasValue)
                    await _signalR.UpdatePositionAsync(
                        _currentGameId, _localPlayerName, stable.Value.x, stable.Value.y);
            }
        }
        
        private static async Task<string> GetPublicIpAsync()
        {
            try
            {
                using var http = new HttpClient();
                var ip = await http.GetStringAsync("https://api.ipify.org");
                return ip.Trim();
            }
            catch
            {
                // Fallback sur IP locale si pas d'internet
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString() ?? "127.0.0.1";
            }
        }

        public void RegisterPeer(string playerName, string ip, int port)
        {
            _peerEndpoints[playerName] = new IPEndPoint(IPAddress.Parse(ip), port);
            _voice.AddPlayer(playerName, _audio.SelectedOutputIndex);
        }

        public async Task OnGameEndedAsync()
        {
            _voice.Dispose();
            _udp.Dispose();

            await _signalR.LeaveGameAsync(_currentGameId, _localPlayerName);

            _currentGameId   = "";
            _localPlayerName = "";
            _isConnected     = false;
            _tracker.Reset();
            _peerEndpoints.Clear();
        }

        private static string GenerateGameId(List<PlayerInfo> players)
        {
            var names = players.Select(p => p.SummonerName).OrderBy(n => n);
            return string.Join("_", names).GetHashCode().ToString("X");
        }

        public async ValueTask DisposeAsync()
        {
            _voice.Dispose();
            _udp.Dispose();
            await _signalR.DisposeAsync();
        }
    }
}