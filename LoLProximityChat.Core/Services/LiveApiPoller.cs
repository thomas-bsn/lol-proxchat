using System.Text.Json;
using LoLProximityChat.Core.Models;

namespace LoLProximityChat.Core.Services
{
    public class LiveApiPoller : IDisposable
    {
        private const string BaseUrl = "https://127.0.0.1:2999";
        private const int PollIntervalMs = 1000;

        // Riot utilise un certificat auto-signé — on ignore la validation SSL
        private static readonly HttpClient _http = new(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        })
        {
            Timeout = TimeSpan.FromSeconds(2),
            BaseAddress = new Uri(BaseUrl)
        };

        private CancellationTokenSource? _cts;
        private bool _wasInGame;

        // ── Events ────────────────────────────────────────────────────────────
        public event Action<GameState>? OnStateChanged;
        public event Action?            OnGameStarted;
        public event Action?            OnGameEnded;

        // ── Public API ────────────────────────────────────────────────────────
        public void Start()
        {
            _cts = new CancellationTokenSource();
            _ = PollLoopAsync(_cts.Token);
        }

        public void Stop() => _cts?.Cancel();

        // ── Poll loop ─────────────────────────────────────────────────────────
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
                    // API injoignable = pas en game
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
                OnGameStarted?.Invoke();
            }
            else if (!state.IsInGame && _wasInGame)
            {
                _wasInGame = false;
                OnGameEnded?.Invoke();
            }
        }

        // ── Fetch ─────────────────────────────────────────────────────────────
        private static async Task<GameState> FetchStateAsync()
        {
            // Joueur local
            var activeJson = await _http.GetStringAsync("/liveclientdata/activeplayer");
            var active = JsonSerializer.Deserialize<ActivePlayer>(activeJson);
            var localName = active?.SummonerName ?? "";

            // Liste complète
            var listJson = await _http.GetStringAsync("/liveclientdata/playerlist");
            var players = JsonSerializer.Deserialize<List<PlayerInfo>>(listJson) ?? [];

            foreach (var p in players)
                p.IsLocalPlayer = p.SummonerName == localName;

            return new GameState
            {
                IsInGame        = true,
                LocalPlayerName = localName,
                Players         = players
            };
        }

        public void Dispose() => Stop();
    }
}