using System.Net;
using System.Net.Sockets;

namespace LoLProximityChat.Core.Audio
{
    public class UdpAudioTransport : IDisposable
    {
        private UdpClient? _udpClient;
        private CancellationTokenSource? _cts;
        private readonly int _listenPort;

        public event Action<string, byte[]>? OnAudioReceived; // playerName, data

        public UdpAudioTransport(int listenPort = 7777)
        {
            _listenPort = listenPort;
        }

        public void Start()
        {
            _udpClient = new UdpClient(_listenPort);
            _cts       = new CancellationTokenSource();
            _ = ReceiveLoopAsync(_cts.Token);
        }

        // Envoie l'audio à un joueur
        public async Task SendAsync(byte[] data, string playerName, IPEndPoint endpoint)
        {
            if (_udpClient is null) return;

            // Format : [longueur nom (1 byte)][nom en UTF8][données audio]
            var nameBytes  = System.Text.Encoding.UTF8.GetBytes(playerName);
            var packet     = new byte[1 + nameBytes.Length + data.Length];
            packet[0]      = (byte)nameBytes.Length;
            Buffer.BlockCopy(nameBytes, 0, packet, 1, nameBytes.Length);
            Buffer.BlockCopy(data,      0, packet, 1 + nameBytes.Length, data.Length);

            await _udpClient.SendAsync(packet, packet.Length, endpoint);
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result    = await _udpClient!.ReceiveAsync(ct);
                    var packet    = result.Buffer;
                    var nameLen   = packet[0];
                    var name      = System.Text.Encoding.UTF8.GetString(packet, 1, nameLen);
                    var audioData = new byte[packet.Length - 1 - nameLen];
                    Buffer.BlockCopy(packet, 1 + nameLen, audioData, 0, audioData.Length);
                    OnAudioReceived?.Invoke(name, audioData);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UDP] Erreur réception : {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _udpClient?.Close();
            _udpClient?.Dispose();
        }
    }
}