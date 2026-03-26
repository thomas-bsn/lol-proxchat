using LoLProximityChat.Server.Controllers;
using LoLProximityChat.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddSingleton<RoomService>();
builder.Services.AddSingleton<ProximityService>();
builder.Services.AddSingleton<WsConnectionManager>();
builder.Services.AddSingleton<WebSocketController>();

var app = builder.Build();

app.UseWebSockets();

app.Map("/room/ws", async context =>
{
    var controller = context.RequestServices.GetRequiredService<WebSocketController>();
    await controller.HandleAsync(context);
});

app.MapControllers();
app.Run();