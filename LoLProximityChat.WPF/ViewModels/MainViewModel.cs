using System.ComponentModel;
using System.Runtime.CompilerServices;
using LoLProximityChat.Core.Models;
using LoLProximityChat.Core.Services;

namespace LoLProximityChat.WPF.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IAsyncDisposable
    {
        private readonly LiveApiPoller _poller = new();
        private readonly SignalRClient _signalR = new(AppConfig.Load().ServerUrl);
        public AudioViewModel Audio { get; } = new();

        public PlayerListViewModel PlayerList { get; } = new();
        public GameSessionViewModel Session { get; private set; } = null!;

        // ── Bindable properties ───────────────────────────────────────────────
        private bool _isInGame;
        public bool IsInGame
        {
            get => _isInGame;
            set { _isInGame = value; OnPropertyChanged(); }
        }
        
        private bool _isServerConnected;
        public bool IsServerConnected
        {
            get => _isServerConnected;
            set { _isServerConnected = value; OnPropertyChanged(); OnPropertyChanged(nameof(ServerStatusText)); }
        }
        public string ServerStatusText => _isServerConnected ? "Serveur connecté" : "Serveur déconnecté";

        private string _statusText = "En attente d'une game...";
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        // ── Init ──────────────────────────────────────────────────────────────
        public MainViewModel()
        {
            Session = new GameSessionViewModel(_signalR, Audio);
            Session.OnServerConnectionChanged += connected =>
                App.Current.Dispatcher.Invoke(() => IsServerConnected = connected);

            _poller.OnGameStarted  += () => App.Current.Dispatcher.Invoke(OnGameStarted);
            _poller.OnGameEnded    += () => App.Current.Dispatcher.Invoke(OnGameEnded);
            _poller.OnStateChanged += s  => App.Current.Dispatcher.Invoke(() => OnStateChanged(s));
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
            IsInGame   = false;
            StatusText = "En attente d'une game...";

            await Session.OnGameEndedAsync();
            PlayerList.Clear();
        }

        private async void OnStateChanged(GameState state)
        {
            if (!state.IsInGame) return;

            await Session.OnStateAsync(state);
            PlayerList.Update(state);
        }

        // ── INotifyPropertyChanged ────────────────────────────────────────────
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public async ValueTask DisposeAsync()
        {
            _poller.Dispose();
            await Session.DisposeAsync();
        }
    }
}