using LoLProximityChat.Server.Hubs;
using LoLProximityChat.Server.Services;

var builder = WebApplication.CreateBuilder(args);
var port = Environment.GetEnvironmentVariable("PORT") ?? "5128";

builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
builder.Services.AddSignalR();
builder.Services.AddSingleton<RoomService>();

var app = builder.Build();

app.MapHub<ProximityHub>("/proximity");

app.Run();