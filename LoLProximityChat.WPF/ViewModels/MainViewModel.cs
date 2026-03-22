using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LoLProximityChat.Core.Models;
using LoLProximityChat.Core.Services;

namespace LoLProximityChat.WPF.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly LiveApiPoller _poller = new();
        private readonly AppConfig _config = AppConfig.Load();
        private readonly SignalRClient _signalR = new("http://localhost:5128");
        private readonly ProximityCalculator _proximity = new();
        private string _currentGameId = "";
        private string _localPlayerName = "";
        private bool _isConnectedToServer = false;

        // ── Bindable properties ───────────────────────────────────────────────
        private bool _isInGame;
        public bool IsInGame
        {
            get => _isInGame;
            set { _isInGame = value; OnPropertyChanged(); }
        }

        private string _statusText = "En attente d'une game...";
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        private string _localPlayerInfo = "";
        public string LocalPlayerInfo
        {
            get => _localPlayerInfo;
            set { _localPlayerInfo = value; OnPropertyChanged(); }
        }

        public ObservableCollection<PlayerInfo> OrderTeam { get; } = [];
        public ObservableCollection<PlayerInfo> ChaosTeam { get; } = [];

        // ── Init ──────────────────────────────────────────────────────────────
        public MainViewModel()
        {
            _poller.OnGameStarted  += () => App.Current.Dispatcher.Invoke(OnGameStarted);
            _poller.OnGameEnded    += () => App.Current.Dispatcher.Invoke(OnGameEnded);
            _poller.OnStateChanged += s  => App.Current.Dispatcher.Invoke(() => ApplyState(s));
            _poller.Start();
        }

        // ── Handlers ──────────────────────────────────────────────────────────
        private void OnGameStarted()
        {
            IsInGame   = true;
            StatusText = "Game en cours";
        }

        private async void OnGameEnded()
        {
            IsInGame             = false;
            StatusText           = "En attente d'une game...";
            LocalPlayerInfo      = "";
            _isConnectedToServer = false;
            OrderTeam.Clear();
            ChaosTeam.Clear();

            await _signalR.LeaveGameAsync(_currentGameId, _localPlayerName);
            _currentGameId = "";
        }

        private async void ApplyState(GameState state)
        {
            if (!state.IsInGame) return;

            // Connecte au serveur si pas encore fait
            if (!_isConnectedToServer)
            {
                _isConnectedToServer = true;
                await _signalR.ConnectAsync();
                await Task.Delay(500);
            }

            // Première fois qu'on reçoit le state → rejoindre la room
            if (_currentGameId == "" && state.LocalPlayerName != "")
            {
                _localPlayerName = state.LocalPlayerName;
                _currentGameId   = GenerateGameId(state.Players);
                await _signalR.JoinGameAsync(_currentGameId, state.LocalPlayerName);
            }

            // Capture minimap + mapping joueurs
            var capture   = new MinimapCapture(new MinimapRegion
            {
                X      = _config.MinimapX,
                Y      = _config.MinimapY,
                Width  = _config.MinimapSize,
                Height = _config.MinimapSize
            });
            var blobs     = capture.DetectBlobs();
            var positions = _proximity.MapBlobsToPlayers(blobs, state.Players);

            // Envoyer sa propre position au serveur
            var localPos = positions.FirstOrDefault(p => p.SummonerName == _localPlayerName);
            if (localPos?.IsVisible == true)
                await _signalR.UpdatePositionAsync(_currentGameId, _localPlayerName, localPos.X, localPos.Y);

            LocalPlayerInfo = state.LocalPlayer is { } lp
                ? $"{lp.SummonerName}  ·  {lp.ChampionName}  ·  Équipe {lp.TeamLabel}"
                : state.LocalPlayerName;

            Sync(OrderTeam, state.OrderTeam);
            Sync(ChaosTeam, state.ChaosTeam);
        }

        private static string GenerateGameId(List<PlayerInfo> players)
        {
            var names = players.Select(p => p.SummonerName).OrderBy(n => n);
            return string.Join("_", names).GetHashCode().ToString("X");
        }

        private static void Sync(ObservableCollection<PlayerInfo> col, List<PlayerInfo> fresh)
        {
            foreach (var p in fresh.Where(p => col.All(c => c.SummonerName != p.SummonerName)))
                col.Add(p);

            foreach (var gone in col.Where(c => fresh.All(p => p.SummonerName != c.SummonerName)).ToList())
                col.Remove(gone);

            foreach (var existing in col)
            {
                var updated = fresh.FirstOrDefault(p => p.SummonerName == existing.SummonerName);
                if (updated is null) continue;
                existing.IsDead        = updated.IsDead;
                existing.IsLocalPlayer = updated.IsLocalPlayer;
            }
        }

        // ── INotifyPropertyChanged ────────────────────────────────────────────
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public async void Dispose()
        {
            _poller.Dispose();
            await _signalR.DisposeAsync();
        }
    }
}