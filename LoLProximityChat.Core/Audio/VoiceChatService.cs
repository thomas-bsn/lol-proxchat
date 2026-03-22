using NAudio.Wave;

namespace LoLProximityChat.Core.Audio
{
    public class VoiceChatService : IDisposable
    {
        private WaveInEvent? _microphone;
        private readonly Dictionary<string, BufferedWaveProvider> _playerBuffers = new();
        private readonly Dictionary<string, WaveOutEvent> _playerOutputs = new();
        private readonly Dictionary<string, float> _playerVolumes = new();

        private const int SampleRate   = 48000;
        private const int Channels     = 1;
        private const int BitsPerSample = 16;
        
        private bool _isMuted = false;

        public event Action<byte[]>? OnAudioCaptured;

        // ── Démarrage ─────────────────────────────────────────────────────────
        public void Start(int inputDeviceIndex = 0, int outputDeviceIndex = 0)
        {
            _microphone = new WaveInEvent
            {
                DeviceNumber       = inputDeviceIndex,
                WaveFormat         = new WaveFormat(SampleRate, BitsPerSample, Channels),
                BufferMilliseconds = 20
            };

            _microphone.DataAvailable += (_, e) =>
            {
                if (_isMuted) return;
                var data = new byte[e.BytesRecorded];
                Buffer.BlockCopy(e.Buffer, 0, data, 0, e.BytesRecorded);
                OnAudioCaptured?.Invoke(data);
            };

            _microphone.StartRecording();
        }

        public void AddPlayer(string playerName, int outputDeviceIndex = 0)
        {
            if (_playerBuffers.ContainsKey(playerName)) return;

            var buffer = new BufferedWaveProvider(new WaveFormat(SampleRate, BitsPerSample, Channels))
            {
                BufferDuration          = TimeSpan.FromSeconds(1),
                DiscardOnBufferOverflow = true
            };

            var output = new WaveOutEvent { DeviceNumber = outputDeviceIndex };
            output.Init(buffer);
            output.Play();

            _playerBuffers[playerName] = buffer;
            _playerOutputs[playerName] = output;
            _playerVolumes[playerName] = 1f;
        }

        public void RemovePlayer(string playerName)
        {
            if (_playerOutputs.TryGetValue(playerName, out var output))
            {
                output.Stop();
                output.Dispose();
                _playerOutputs.Remove(playerName);
            }
            _playerBuffers.Remove(playerName);
            _playerVolumes.Remove(playerName);
        }

        // ── Audio entrant (depuis UDP) ────────────────────────────────────────
        public void ReceiveAudio(string playerName, byte[] data)
        {
            if (!_playerBuffers.TryGetValue(playerName, out var buffer)) return;
            buffer.AddSamples(data, 0, data.Length);
        }
        
        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                _isMuted = value;
                if (_microphone != null)
                {
                    if (_isMuted) _microphone.StopRecording();
                    else _microphone.StartRecording();
                }
            }
        }

        // ── Volumes (depuis SignalR) ───────────────────────────────────────────
        public void UpdateVolumes(Dictionary<string, float> volumes)
        {
            foreach (var (playerName, volume) in volumes)
            {
                _playerVolumes[playerName] = volume;

                if (_playerOutputs.TryGetValue(playerName, out var output))
                    output.Volume = Math.Clamp(volume, 0f, 1f);
            }
        }

        // ── Dispose ───────────────────────────────────────────────────────────
        public void Dispose()
        {
            _microphone?.StopRecording();
            _microphone?.Dispose();

            foreach (var output in _playerOutputs.Values)
            {
                output.Stop();
                output.Dispose();
            }

            _playerBuffers.Clear();
            _playerOutputs.Clear();
            _playerVolumes.Clear();
        }
    }
}