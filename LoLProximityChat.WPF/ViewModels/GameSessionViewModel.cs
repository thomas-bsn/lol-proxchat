using LoLProximityChat.Core.Audio;
using LoLProximityChat.Core.Models;
using LoLProximityChat.Core.Services;

namespace LoLProximityChat.WPF.ViewModels
{
    public class GameSessionViewModel : IAsyncDisposable
    {
        private readonly SignalRClient    _signalR;
        private readonly ProximityCalculator _proximity = new();
        private readonly AppConfig        _config   = AppConfig.Load();
        private readonly PositionTracker  _tracker  = new();
        private readonly VoiceChatService _voice    = new();
        private readonly AudioViewModel   _audio;

        public event Action<bool>? OnServerConnectionChanged;

        private string _currentGameId   = "";
        private string _localPlayerName = "";
        private bool   _isConnected     = false;

        public GameSessionViewModel(SignalRClient signalR, AudioViewModel audio)
        {
            _signalR = signalR;
            _audio   = audio;

            _audio.MuteMicRequested    += muted  => _voice.IsMuted = muted;
            _audio.MasterVolumeChanged += volume => _voice.SetMasterVolume(volume);
            _audio.MicVolumeChanged    += volume => _voice.SetMicVolume(volume);

            _voice.OnAudioCaptured += async data =>
            {
                if (_currentGameId != "")
                    await _signalR.SendAudioAsync(_currentGameId, _localPlayerName, data);
            };

            // UNIQUE handler OnAudioReceived — avec auto-découverte
            _signalR.OnAudioReceived += (playerName, data) =>
            {
                if (!_voice.GetPlayerNames().Contains(playerName))
                {
                    _voice.AddPlayer(playerName, _audio.SelectedOutputIndex);
                    _audio.AddPlayer(playerName);
                }
                _voice.ReceiveAudio(playerName, data);
            };

            _signalR.OnVolumesUpdated += volumes =>
            {
                _voice.UpdateVolumes(volumes);
                _audio.UpdateVolumes(volumes);
            };
            
            _signalR.OnReconnected += async () =>
            {
                if (_currentGameId != "" && _localPlayerName != "")
                {
                    await _signalR.JoinGameAsync(_currentGameId, _localPlayerName);
                    Console.WriteLine($"[REJOIN] {_localPlayerName} → room {_currentGameId}");
                }
            };

            _signalR.OnPlayerJoined += playerName =>
            {
                if (playerName == _localPlayerName) return;
                if (!_voice.GetPlayerNames().Contains(playerName))
                {
                    _voice.AddPlayer(playerName, _audio.SelectedOutputIndex);
                    _audio.AddPlayer(playerName);
                }
            };

            _signalR.OnExistingPlayers += players =>
            {
                foreach (var name in players)
                {
                    if (name == _localPlayerName) continue;
                    if (!_voice.GetPlayerNames().Contains(name))
                    {
                        _voice.AddPlayer(name, _audio.SelectedOutputIndex);
                        _audio.AddPlayer(name);
                    }
                }
            };

            _signalR.OnPlayerLeft += playerName =>
            {
                _voice.RemovePlayer(playerName);
                _audio.RemovePlayer(playerName);
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
            
            Console.WriteLine($"[DEBUG] Players.Count={state.Players.Count} | gameId={_currentGameId}");

            // FIX : >= 2 au lieu de == "" seulement, pour ne pas hasher une liste incomplète
            if (_currentGameId == "" && state.Players.Count >= 2 && state.LocalPlayerName != "")
            {
                _localPlayerName = state.LocalPlayerName;
                _currentGameId   = GenerateGameId(state.Players, state.GameTime);

                Console.WriteLine($"[GAMEID] {_currentGameId} (gameTime={state.GameTime:F0}s)");

                await _signalR.JoinGameAsync(_currentGameId, _localPlayerName);
                _voice.Start(_audio.SelectedInputIndex, _audio.SelectedOutputIndex);
            }

            var capture = new MinimapCapture(new MinimapRegion
            {
                X      = _config.MinimapX,
                Y      = _config.MinimapY,
                Width  = _config.MinimapSize,
                Height = _config.MinimapSize
            });
            var blobs     = capture.DetectBlobs();
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
        
        private static string GenerateGameId(List<PlayerInfo> players, float gameTime)
        {
            var names = players.Select(p => p.SummonerName).OrderBy(n => n);
            return string.Join("_", names).GetHashCode().ToString("X");
        }

        public async Task ReconnectAsync()
        {
            _isConnected     = false;
            _currentGameId   = "";
            _localPlayerName = "";
            _tracker.Reset();
            await _signalR.ConnectAsync();
        }

        public async Task OnGameEndedAsync()
        {
            _voice.Dispose();
            await _signalR.LeaveGameAsync(_currentGameId, _localPlayerName);

            _currentGameId   = "";
            _localPlayerName = "";
            _isConnected     = false;
            _tracker.Reset();
        }

        public async ValueTask DisposeAsync()
        {
            _voice.Dispose();
            await _signalR.DisposeAsync();
        }
    }
}