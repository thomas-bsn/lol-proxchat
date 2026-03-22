using LoLProximityChat.Core.Models;

namespace LoLProximityChat.Core.Services
{
    public class PlayerMapPosition
    {
        public string SummonerName { get; set; } = "";
        public string ChampionName { get; set; } = "";
        public string Team         { get; set; } = "";
        public float X             { get; set; }
        public float Y             { get; set; }
        public bool  IsVisible     { get; set; }
    }

    public class ProximityCalculator
    {
        // Rayon max en unités LoL pour entendre quelqu'un (1200 = ~1 écran)
        private const float MaxHearingRange = 3000f;

        // Mappe les blobs détectés sur la minimap aux joueurs connus via la Live API
        public List<PlayerMapPosition> MapBlobsToPlayers(
            List<(float x, float y, string team)> blobs,
            List<PlayerInfo> players)
        {
            var result = new List<PlayerMapPosition>();

            var orderPlayers = players.Where(p => p.Team == "ORDER").ToList();
            var chaosPlayers = players.Where(p => p.Team == "CHAOS").ToList();

            var orderBlobs = blobs.Where(b => b.team == "ORDER").ToList();
            var chaosBlobs = blobs.Where(b => b.team == "CHAOS").ToList();

            // Assigner les blobs ORDER aux joueurs ORDER
            AssignBlobsToPlayers(orderBlobs, orderPlayers, result);
            // Assigner les blobs CHAOS aux joueurs CHAOS
            AssignBlobsToPlayers(chaosBlobs, chaosPlayers, result);

            return result;
        }

        private static void AssignBlobsToPlayers(
            List<(float x, float y, string team)> blobs,
            List<PlayerInfo> players,
            List<PlayerMapPosition> result)
        {
            // Joueurs sans blob = invisibles (fog of war)
            var assignedPlayers = new HashSet<string>();

            // Pour chaque blob, trouver le joueur le plus probable
            // Pour l'instant : assignment simple par ordre
            // TODO : utiliser la position précédente pour matcher
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

                assignedPlayers.Add(player.SummonerName);
            }

            // Joueurs non assignés = dans le fog
            foreach (var player in players.Where(p => !assignedPlayers.Contains(p.SummonerName)))
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

        // Calcule le volume entre deux joueurs selon leur distance
        public float CalculateVolume(PlayerMapPosition listener, PlayerMapPosition speaker)
        {
            if (!speaker.IsVisible) return 0f;
            if (!listener.IsVisible) return 0f;

            var distance = MathF.Sqrt(
                MathF.Pow(listener.X - speaker.X, 2) +
                MathF.Pow(listener.Y - speaker.Y, 2));

            if (distance >= MaxHearingRange) return 0f;

            // Falloff logarithmique
            var volume = 1f - (distance / MaxHearingRange);
            return MathF.Pow(volume, 2); // courbe quadratique
        }
    }
}