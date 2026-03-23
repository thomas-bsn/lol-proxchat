using LoLProximityChat.Core.Models;

namespace LoLProximityChat.Core.Services
{
    public class ProximityCalculator
    {
        private readonly Dictionary<string, (float x, float y)> _lastPositions = new();

        public List<PlayerMapPosition> MapBlobsToPlayers(
            List<(float x, float y, string team)> blobs,
            List<PlayerInfo> players)
        {
            var result = new List<PlayerMapPosition>();

            var orderPlayers = players.Where(p => p.Team == "ORDER").ToList();
            var chaosPlayers = players.Where(p => p.Team == "CHAOS").ToList();
            var orderBlobs   = blobs.Where(b => b.team == "ORDER").ToList();
            var chaosBlobs   = blobs.Where(b => b.team == "CHAOS").ToList();

            AssignBlobsToPlayers(orderBlobs, orderPlayers, result, _lastPositions);
            AssignBlobsToPlayers(chaosBlobs, chaosPlayers, result, _lastPositions);

            // Met à jour les dernières positions connues
            foreach (var p in result.Where(p => p.IsVisible))
                _lastPositions[p.SummonerName] = (p.X, p.Y);

            return result;
        }

        private static void AssignBlobsToPlayers(
            List<(float x, float y, string team)> blobs,
            List<PlayerInfo> players,
            List<PlayerMapPosition> result,
            Dictionary<string, (float x, float y)> lastPositions)
        {
            var usedBlobs = new HashSet<int>();
            var assigned  = new HashSet<string>();

            foreach (var player in players)
            {
                // Référence = dernière position connue, ou centre de map par défaut
                (float x, float y) reference = lastPositions.TryGetValue(player.SummonerName, out var last)
                    ? last
                    : (7500f, 7500f);

                int   bestIdx  = -1;
                float bestDist = float.MaxValue;

                for (int i = 0; i < blobs.Count; i++)
                {
                    if (usedBlobs.Contains(i)) continue;
                    var d = Dist(reference, (blobs[i].x, blobs[i].y));
                    if (d < bestDist) { bestDist = d; bestIdx = i; }
                }

                if (bestIdx >= 0)
                {
                    usedBlobs.Add(bestIdx);
                    assigned.Add(player.SummonerName);
                    result.Add(new PlayerMapPosition
                    {
                        SummonerName = player.SummonerName,
                        ChampionName = player.ChampionName,
                        Team         = player.Team,
                        X            = blobs[bestIdx].x,
                        Y            = blobs[bestIdx].y,
                        IsVisible    = true
                    });
                }
            }

            // Joueurs non assignés = fog of war
            foreach (var player in players.Where(p => !assigned.Contains(p.SummonerName)))
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

        private static float Dist((float x, float y) a, (float x, float y) b)
            => MathF.Sqrt(MathF.Pow(a.x - b.x, 2) + MathF.Pow(a.y - b.y, 2));
    }
}