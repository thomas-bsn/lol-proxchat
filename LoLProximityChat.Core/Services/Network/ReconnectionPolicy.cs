namespace LoLProximityChat.Core.Core
{
    public class ReconnectionPolicy
    {
        private const int MaxRetries     = 5;
        private const int DelayMs        = 2000;
        private const int BackoffFactor  = 2;

        public async Task<bool> TryReconnectAsync(Func<Task> reconnect, CancellationToken ct)
        {
            var delay = DelayMs;

            for (int i = 0; i < MaxRetries; i++)
            {
                try
                {
                    await Task.Delay(delay, ct);
                    await reconnect();
                    Console.WriteLine($"[Reconnection] Reconnecté après {i + 1} tentative(s)");
                    return true;
                }
                catch
                {
                    Console.WriteLine($"[Reconnection] Tentative {i + 1}/{MaxRetries} échouée");
                    delay *= BackoffFactor; // 2s → 4s → 8s → 16s → 32s
                }
            }

            Console.WriteLine("[Reconnection] Toutes les tentatives ont échoué");
            return false;
        }
    }
}