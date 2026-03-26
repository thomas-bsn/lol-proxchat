using LoLProximityChat.Shared.Constants;
using LoLProximityChat.Shared.DTOs;

namespace LoLProximityChat.Server.Services
{
    public class ProximityService
    {
        public Dictionary<string, (string connectionId, VolumePayload payload)> ComputeVolumes(
            string      gameId,
            RoomService roomService)
        {
            var positions = roomService.GetPositions(gameId);
            var result    = new Dictionary<string, (string, VolumePayload)>();

            foreach (var listener in positions)
            {
                var connId = roomService.GetConnectionId(listener.Key);
                if (connId is null) continue;

                var volumes = new Dictionary<string, PlayerVolume>();
                foreach (var speaker in positions)
                {
                    if (speaker.Key == listener.Key) continue;

                    var discord = roomService.GetDiscordUsername(speaker.Key) ?? "";
                    var volume  = CalculateVolume(listener.Value, speaker.Value);
                    volumes[speaker.Key] = new PlayerVolume(volume, discord);
                }

                result[listener.Key] = (connId, new VolumePayload(volumes));
            }

            return result;
        }

        private static float CalculateVolume(
            (float x, float y) listener,
            (float x, float y) speaker)
        {
            var distance = MathF.Sqrt(
                MathF.Pow(listener.x - speaker.x, 2) +
                MathF.Pow(listener.y - speaker.y, 2));

            if (distance >= ProximityConstants.MaxHearingRange) return 0f;

            var normalized = distance / ProximityConstants.MaxHearingRange;
            return MathF.Max(0f, 1f - MathF.Log(1f + normalized * (MathF.E - 1f)));
        }
    }
}