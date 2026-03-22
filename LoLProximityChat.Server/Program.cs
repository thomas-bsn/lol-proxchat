using LoLProximityChat.Server.Hubs;
using LoLProximityChat.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<RoomService>();

var app = builder.Build();

app.MapHub<ProximityHub>("/proximity");

app.Run();