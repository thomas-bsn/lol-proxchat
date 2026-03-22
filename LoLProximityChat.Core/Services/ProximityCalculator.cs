using LoLProximityChat.Core.Models;

namespace LoLProximityChat.Core.Services
{
    public class ProximityCalculator
    {
        public List<PlayerMapPosition> MapBlobsToPlayers(
            List<(float x, float y, string team)> blobs,
            List<PlayerInfo> players)
        {
            var result = new List<PlayerMapPosition>();

            var orderPlayers = players.Where(p => p.Team == "ORDER").ToList();
            var chaosPlayers = players.Where(p => p.Team == "CHAOS").ToList();
            var orderBlobs   = blobs.Where(b => b.team == "ORDER").ToList();
            var chaosBlobs   = blobs.Where(b => b.team == "CHAOS").ToList();

            AssignBlobsToPlayers(orderBlobs, orderPlayers, result);
            AssignBlobsToPlayers(chaosBlobs, chaosPlayers, result);

            return result;
        }

        private static void AssignBlobsToPlayers(
            List<(float x, float y, string team)> blobs,
            List<PlayerInfo> players,
            List<PlayerMapPosition> result)
        {
            var assigned = new HashSet<string>();

            // TODO : utiliser la position précédente pour un meilleur matching
            for (int i = 0; i < Math.Min(blobs.Count, players.Count); i++)
            {
                var blob   = blobs[i];
                var player = players[i];

                result.Add(new PlayerMapPosition
                {
                    SummonerName = player.SummonerName,
                    ChampionName = player.ChampionName,
                    Team         = player.Team,
                    X            = blob.x,
                    Y            = blob.y,
                    IsVisible    = true
                });

                assigned.Add(player.SummonerName);
            }

            // Joueurs non assignés = fog of war
            foreach (var player in players.Where(p => !assigned.Contains(p.SummonerName)))
            {
                result.Add(new PlayerMapPosition
                {
                    SummonerName = player.SummonerName,
                    ChampionName = player.ChampionName,
                    Team         = player.Team,
                    X            = -1,
                    Y            = -1,
                    IsVisible    = false
                });
            }
        }
    }
}