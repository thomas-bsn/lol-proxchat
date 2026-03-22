using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LoLProximityChat.Core.Models;

namespace LoLProximityChat.WPF.ViewModels
{
    public class PlayerListViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<PlayerInfo> OrderTeam { get; } = [];
        public ObservableCollection<PlayerInfo> ChaosTeam { get; } = [];

        private string _localPlayerInfo = "";
        public string LocalPlayerInfo
        {
            get => _localPlayerInfo;
            set { _localPlayerInfo = value; OnPropertyChanged(); }
        }

        public void Update(GameState state)
        {
            LocalPlayerInfo = state.LocalPlayer is { } lp
                ? $"{lp.SummonerName}  ·  {lp.ChampionName}  ·  Équipe {lp.TeamLabel}"
                : state.LocalPlayerName;

            Sync(OrderTeam, state.OrderTeam);
            Sync(ChaosTeam, state.ChaosTeam);
        }

        public void Clear()
        {
            LocalPlayerInfo = "";
            OrderTeam.Clear();
            ChaosTeam.Clear();
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

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}