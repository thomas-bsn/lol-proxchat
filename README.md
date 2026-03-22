# rift-voice

> Proximity voice chat for League of Legends custom games.  
> The closer you are to another player, the louder they sound.

Built with C# WPF + ASP.NET Core SignalR.

## How it works

Each player runs the app alongside League of Legends. Your position is detected from your minimap and sent to a shared server. The server calculates distances between all players in real time and adjusts audio volumes accordingly — invisible champions (Shaco, Evelynn) can still be heard if they're nearby.

## Features

- Proximity-based voice — volume scales with in-game distance
- Fog of war aware — invisible champions can still be heard nearby
- Calibration overlay — adjust minimap region directly over the game
- Self-hostable server — run on your machine or homelab
- TOS-safe — no memory reading, uses Riot's official Live Client API

## Getting started
```bash
# Server
cd LoLProximityChat.Server
dotnet run

# Client
cd LoLProximityChat.WPF
dotnet run
```

On first launch click **⚙ Calibration** and adjust the red rectangle over your minimap.

## Screenshots

<!-- TODO -->

## Credits

Champion classifier model by [danthi123](https://github.com/danthi123/LoLProxyChat) — used for minimap champion identification.

## License

MIT
