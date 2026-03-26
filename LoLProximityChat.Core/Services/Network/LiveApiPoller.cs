using System.Text.Json;
using LoLProximityChat.Core.Models;

namespace LoLProximityChat.Core.Services.Network
{
    public class LiveApiPoller : ILiveApiPoller, IDisposable
    {
        private const string BaseUrl        = "https://127.0.0.1:2999";
        private const int    PollIntervalMs = 1000;

        private static readonly HttpClient _http = new(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        })
        {
            Timeout     = TimeSpan.FromSeconds(2),
            BaseAddress = new Uri(BaseUrl)
        };

        private CancellationTokenSource? _cts;
        private bool _wasInGame;

        public event Action<GameState>? OnStateChanged;
        public event Action<GameState>? OnGameStarted;
        public event Action?            OnGameEnded;

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _ = PollLoopAsync(_cts.Token);
        }

        public void Stop() => _cts?.Cancel();

        private async Task PollLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var state = await FetchStateAsync();
                    HandleTransition(state);
                    OnStateChanged?.Invoke(state);
                }
                catch
                {
                    if (_wasInGame)
                    {
                        _wasInGame = false;
                        OnGameEnded?.Invoke();
                        OnStateChanged?.Invoke(new GameState { IsInGame = false });
                    }
                }

                await Task.Delay(PollIntervalMs, ct).ConfigureAwait(false);
            }
        }

        private void HandleTransition(GameState state)
        {
            if (state.IsInGame && !_wasInGame)
            {
                _wasInGame = true;
                OnGameStarted?.Invoke(state);
            }
            else if (!state.IsInGame && _wasInGame)
            {
                _wasInGame = false;
                OnGameEnded?.Invoke();
            }
        }

        private static async Task<GameState> FetchStateAsync()
        {
            var activeJson = await _http.GetStringAsync("/liveclientdata/activeplayer");
            var active     = JsonSerializer.Deserialize<ActivePlayer>(activeJson);
            var localName  = active?.SummonerName ?? "";

            var listJson = await _http.GetStringAsync("/liveclientdata/playerlist");
            var rawPlayers = JsonSerializer.Deserialize<List<PlayerPosition>>(listJson) ?? [];

            foreach (var p in rawPlayers)
                p.IsLocalPlayer = p.SummonerName == localName;

            var statsJson = await _http.GetStringAsync("/liveclientdata/gamestats");
            var stats     = JsonSerializer.Deserialize<GameStats>(statsJson);

            return new GameState
            {
                IsInGame        = true,
                LocalPlayerName = localName,
                Players         = rawPlayers,
                GameTime        = stats?.GameTime ?? 0f
            };
        }

        public void Dispose() => Stop();
    }
}