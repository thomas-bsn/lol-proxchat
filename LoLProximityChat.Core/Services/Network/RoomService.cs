using System.Net.Http.Json;
using LoLProximityChat.Core.Interfaces;
using LoLProximityChat.Core.Models;
using LoLProximityChat.Shared.DTOs;

namespace LoLProximityChat.Core.Services.Network
{
    public class RoomService : IRoomService
    {
        private readonly HttpClient _http;
        private readonly AppConfig  _config;

        public RoomService(AppConfig config)
        {
            _config = config;
            _http   = new HttpClient
            {
                BaseAddress = new Uri(config.ServerUrl)
            };
        }

        public async Task<string> JoinOrCreateAsync(string roomId, string playerId, CancellationToken ct)
        {
            var response = await _http.PostAsJsonAsync("/room/join", new JoinRoomRequest(
                roomId,
                playerId,
                _config.DiscordUsername
            ), ct);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<JoinRoomResponse>(cancellationToken: ct);
            return result?.Token ?? throw new Exception("Token manquant dans la réponse du serveur");
        }
    }
}