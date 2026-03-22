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

            _signalR.OnAudioReceived += (playerName, data) =>
                _voice.ReceiveAudio(playerName, data);

            _signalR.OnVolumesUpdated += volumes =>
            {
                _voice.UpdateVolumes(volumes);
                _audio.UpdateVolumes(volumes);
            };

            _signalR.OnPlayerJoined += playerName =>
            {
                if (playerName == _localPlayerName) return;
                _voice.AddPlayer(playerName, _audio.SelectedOutputIndex);
                _audio.AddPlayer(playerName);
            };

            // NOUVEAU — joueurs déjà présents quand on rejoint
            _signalR.OnExistingPlayers += players =>
            {
                foreach (var name in players)
                {
                    if (name == _localPlayerName) continue;
                    _voice.AddPlayer(name, _audio.SelectedOutputIndex);
                    _audio.AddPlayer(name);
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

            // FIX : >= 2 au lieu de == "" seulement, pour ne pas hasher une liste incomplète
            if (_currentGameId == "" && state.Players.Count >= 2 && state.LocalPlayerName != "")
            {
                _localPlayerName = state.LocalPlayerName;
                _currentGameId   = GenerateGameId(state.Players);
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

        private static string GenerateGameId(List<PlayerInfo> players)
        {
            var names = players.Select(p => p.SummonerName).OrderBy(n => n);
            return string.Join("_", names).GetHashCode().ToString("X");
        }

        public async ValueTask DisposeAsync()
        {
            _voice.Dispose();
            await _signalR.DisposeAsync();
        }
    }
}