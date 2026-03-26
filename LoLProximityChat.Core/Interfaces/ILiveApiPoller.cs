using LoLProximityChat.Core.Models;

public interface ILiveApiPoller
{
    event Action<GameState>? OnStateChanged;
    event Action<GameState>? OnGameStarted;  // ← Action<GameState> pas Action
    event Action?            OnGameEnded;
    void Start();
    void Stop();
}