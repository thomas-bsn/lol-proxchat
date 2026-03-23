using LoLProximityChat.Core.Audio;
using LoLProximityChat.Core.Models;
using LoLProximityChat.Core.Services;

namespace LoLProximityChat.WPF.ViewModels
{
    public class GameSessionViewModel : IAsyncDisposable
    {
        private readonly SignalRClient _signalR;
        private readonly AppConfig _config = AppConfig.Load();
        private readonly DiscordRpcService _discord;
        private readonly MinimapTracker _tracker;

        private readonly AudioViewModel _audioVm;

        private Dictionary<string, string> _discordMapping = new();

        public event Action<bool>? OnServerConnectionChanged;

        private string _currentGameId = "";
        private string _localPlayerName = "";
        public string LocalPlayerName => _localPlayerName;
        private bool _isConnected = false;

        public GameSessionViewModel(SignalRClient signalR, AudioViewModel audioVm)
        {
            _signalR = signalR;
            _audioVm = audioVm;
            _discord = new DiscordRpcService(_config.DiscordClientId, _config.ServerUrl);

            _tracker = new MinimapTracker(
                _config.MinimapX,
                _config.MinimapY,
                _config.MinimapSize
            );

            _signalR.OnDiscordMapping += mapping =>
            {
                _discordMapping = mapping;
                App.Current.Dispatcher.Invoke(() => _audioVm.ApplyDiscordMapping(mapping));
            };

            _signalR.OnVolumesUpdated += async volumes =>
            {
                foreach (var (lolName, volume) in volumes)
                {
                    if (!_discordMapping.TryGetValue(lolName, out var discordUsername))
                        continue;

                    if (!_discord.VoiceMembers.TryGetValue(discordUsername, out var userId))
                        continue;

                    await _discord.SetUserVolumeAsync(userId, volume);
                }

                _audioVm.UpdateVolumes(volumes);
            };

            _signalR.OnPlayerJoined += playerName =>
            {
                if (playerName != _localPlayerName)
                    _audioVm.AddPlayer(playerName);
            };

            _signalR.OnExistingPlayers += players =>
            {
                foreach (var name in players)
                    if (name != _localPlayerName)
                        _audioVm.AddPlayer(name);
            };

            _signalR.OnPlayerLeft += playerName =>
                _audioVm.RemovePlayer(playerName);

            _signalR.OnConnectionChanged += connected =>
                OnServerConnectionChanged?.Invoke(connected);

            _signalR.OnReconnected += async () =>
            {
                if (_currentGameId != "" && _localPlayerName != "")
                {
                    await _signalR.JoinGameAsync(_currentGameId, _localPlayerName, _config.MyDiscordUsername);
                }
            };
        }

        public async Task OnStateAsync(GameState state)
        {
            if (!_isConnected)
            {
                _isConnected = true;
                await _signalR.ConnectAsync();
                await _discord.ConnectAsync();
                await Task.Delay(500);
            }

            if (_currentGameId == "" && state.Players.Count >= 2 && state.LocalPlayerName != "")
            {
                _localPlayerName = state.LocalPlayerName;
                _currentGameId = GenerateGameId(state.Players);

                await _signalR.JoinGameAsync(_currentGameId, _localPlayerName, _config.MyDiscordUsername);

                // 🔥 Load ONNX model ici
                _tracker.LoadModel(
                    "Core/Models/champion_classifier.onnx",
                    "Core/Models/champion_labels.json",
                    state.LocalPlayer.ChampionName
                );
            }

            // 🔥 NOUVELLE LOGIQUE (REMPLACE TOUT)
            var pos = _tracker.Tick();

            if (pos.HasValue && _currentGameId != "")
            {
                Console.WriteLine($"[TRACK] X={pos.Value.x:F0} Y={pos.Value.y:F0}");
                await _signalR.UpdatePositionAsync(
                    _currentGameId,
                    _localPlayerName,
                    pos.Value.x,
                    pos.Value.y
                );
            }
        }

        private static string GenerateGameId(List<PlayerInfo> players)
        {
            var names = players.Select(p => p.SummonerName).OrderBy(n => n);
            var key = string.Join("_", names);
            var bytes = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(key));
            return Convert.ToHexString(bytes)[..8];
        }

        public async Task ReconnectAsync()
        {
            _isConnected = false;
            _currentGameId = "";
            _localPlayerName = "";
            _tracker.Reset();
            await _signalR.ConnectAsync();
        }

        public async Task OnGameEndedAsync()
        {
            foreach (var (_, userId) in _discord.VoiceMembers)
                await _discord.SetUserVolumeAsync(userId, 1f);

            await _signalR.LeaveGameAsync(_currentGameId, _localPlayerName);

            _currentGameId = "";
            _localPlayerName = "";
            _isConnected = false;
            _tracker.Reset();
        }

        public async ValueTask DisposeAsync()
        {
            _tracker.Dispose();
            _discord.Dispose();
            await _signalR.DisposeAsync();
        }
    }
}