using LoLProximityChat.Core.Models;
using LoLProximityChat.Core.Services;

namespace LoLProximityChat.WPF.ViewModels
{
    public class GameSessionViewModel : IAsyncDisposable
    {
        private readonly SignalRClient _signalR;
        private readonly ProximityCalculator _proximity = new();
        private readonly AppConfig _config = AppConfig.Load();

        private string _currentGameId = "";
        private string _localPlayerName = "";
        private bool _isConnected = false;

        public GameSessionViewModel(SignalRClient signalR)
        {
            _signalR = signalR;
        }

        public async Task OnStateAsync(GameState state)
        {
            // Connexion au serveur
            if (!_isConnected)
            {
                _isConnected = true;
                await _signalR.ConnectAsync();
                await Task.Delay(500);
            }

            // Rejoindre la room
            if (_currentGameId == "" && state.LocalPlayerName != "")
            {
                _localPlayerName = state.LocalPlayerName;
                _currentGameId   = GenerateGameId(state.Players);
                await _signalR.JoinGameAsync(_currentGameId, state.LocalPlayerName);
            }

            // Capture minimap + envoi position
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
                await _signalR.UpdatePositionAsync(_currentGameId, _localPlayerName, localPos.X, localPos.Y);
        }

        public async Task OnGameEndedAsync()
        {
            await _signalR.LeaveGameAsync(_currentGameId, _localPlayerName);
            _currentGameId   = "";
            _localPlayerName = "";
            _isConnected     = false;
        }

        private static string GenerateGameId(List<PlayerInfo> players)
        {
            var names = players.Select(p => p.SummonerName).OrderBy(n => n);
            return string.Join("_", names).GetHashCode().ToString("X");
        }

        public async ValueTask DisposeAsync()
        {
            await _signalR.DisposeAsync();
        }
    }
}