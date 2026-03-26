using LoLProximityChat.Core;
using LoLProximityChat.Core.Models;
using LoLProximityChat.Core.Services.Network;
using LoLProximityChat.Core.Core;
using LoLProximityChat.Services.Discord;
using LoLProximityChat.Shared.Constants;

namespace LoLProximityChat.WPF.ViewModels
{
    public class MainViewModel
    {
        private readonly Orchestrator _orchestrator;
        public Orchestrator Orchestrator => _orchestrator;

        public MainViewModel(AppConfig config)
        {
            var roomService        = new RoomService(config);
            var socketService      = new SocketService(config);
            var reconnectionPolicy = new ReconnectionPolicy();
            var discordRpcService  = new DiscordRpcService(
                config.DiscordClientId,
                config.ServerUrl,
                config.DiscordRedirectUri
            );

            _orchestrator = new Orchestrator(
                roomService,
                socketService,
                reconnectionPolicy,
                discordRpcService
            );
        }

        public async Task JoinTestRoomAsync()
        {
            var connected = await _orchestrator.JoinAndConnectAsync(
                roomId:   "testroom123",
                playerId: "Joueur1",
                ct:       CancellationToken.None
            );
            
            await Task.Delay(500);

            while (true)
            {
                await _orchestrator.SendPositionAsync(new PlayerPosition
                {
                    SummonerName = "Joueur1",
                    X            = 7000f,
                    Y            = 7000f
                });
                await Task.Delay(1000);
            }
        }
    }
}