using LoLProximityChat.Server.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

var app = builder.Build();

app.MapHub<ProximityHub>("/proximity");

app.Run();