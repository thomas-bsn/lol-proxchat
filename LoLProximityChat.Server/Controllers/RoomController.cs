using Microsoft.AspNetCore.Mvc;
using LoLProximityChat.Server.Services;
using LoLProximityChat.Shared.DTOs;

namespace LoLProximityChat.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RoomController : ControllerBase
    {
        private readonly RoomService _roomService;
        private readonly ILogger<RoomController> _logger;

        public RoomController(RoomService roomService, ILogger<RoomController> logger)
        {
            _roomService = roomService;
            _logger      = logger;
        }

        [HttpPost("join")]
        public IActionResult Join([FromBody] JoinRoomRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RoomId) ||
                string.IsNullOrWhiteSpace(request.PlayerId))
                return BadRequest("RoomId et PlayerId sont requis");

            var discordUsername = string.IsNullOrWhiteSpace(request.DiscordUsername)
                ? request.PlayerId
                : request.DiscordUsername;

            var connectionId = Guid.NewGuid().ToString();

            _roomService.AddPlayer(
                connectionId,
                request.PlayerId,
                request.RoomId,
                discordUsername
            );

            return Ok(new JoinRoomResponse(connectionId));
        }
    }
}