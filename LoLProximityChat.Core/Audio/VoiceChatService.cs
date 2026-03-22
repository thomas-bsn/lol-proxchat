using NAudio.Wave;

namespace LoLProximityChat.Core.Audio
{
    public class VoiceChatService : IDisposable
    {
        private WaveInEvent? _microphone;
        private readonly Dictionary<string, BufferedWaveProvider> _playerBuffers = new();
        private readonly Dictionary<string, WaveOutEvent>         _playerOutputs = new();
        private readonly Dictionary<string, float>                _playerVolumes = new();

        private const int   SampleRate    = 48000;
        private const int   Channels      = 1;
        private const int   BitsPerSample = 16;
        private const float VadThreshold  = 0.02f;

        private bool  _isMuted;
        private float _micGain       = 1f;
        private float _masterVolume  = 1f;

        public event Action<byte[]>? OnAudioCaptured;

        // ── VAD ───────────────────────────────────────────────────────────────
        private static float CalculateRms(byte[] buffer)
        {
            float sum = 0;
            for (int i = 0; i < buffer.Length - 1; i += 2)
            {
                short sample     = BitConverter.ToInt16(buffer, i);
                float normalized = sample / 32768f;
                sum += normalized * normalized;
            }
            return (float)Math.Sqrt(sum / (buffer.Length / 2));
        }

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

                // Applique le gain micro
                if (_micGain != 1f)
                {
                    for (int i = 0; i < data.Length - 1; i += 2)
                    {
                        short sample = BitConverter.ToInt16(data, i);
                        sample = (short)Math.Clamp(sample * _micGain, short.MinValue, short.MaxValue);
                        BitConverter.GetBytes(sample).CopyTo(data, i);
                    }
                }

                if (CalculateRms(data) < VadThreshold) return;
                OnAudioCaptured?.Invoke(data);
            };

            _microphone.StartRecording();
        }

        // ── Joueurs ───────────────────────────────────────────────────────────
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

        public IEnumerable<string> GetPlayerNames() => _playerOutputs.Keys;

        // ── Audio entrant ─────────────────────────────────────────────────────
        public void ReceiveAudio(string playerName, byte[] data)
        {
            if (!_playerBuffers.TryGetValue(playerName, out var buffer)) return;
            buffer.AddSamples(data, 0, data.Length);
        }

        // ── Mute ──────────────────────────────────────────────────────────────
        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                _isMuted = value;
                if (_microphone is null) return;
                if (_isMuted) _microphone.StopRecording();
                else          _microphone.StartRecording();
            }
        }

        // ── Volumes ───────────────────────────────────────────────────────────
        public void UpdateVolumes(Dictionary<string, float> volumes)
        {
            foreach (var (playerName, volume) in volumes)
            {
                _playerVolumes[playerName] = volume;
                if (_playerOutputs.TryGetValue(playerName, out var output))
                    output.Volume = Math.Clamp(volume * _masterVolume, 0f, 1f);
            }
        }

        public void SetMasterVolume(float volume)
        {
            _masterVolume = volume;
            foreach (var (name, output) in _playerOutputs)
                output.Volume = Math.Clamp(_playerVolumes.GetValueOrDefault(name, 1f) * volume, 0f, 1f);
        }

        public void SetMicVolume(float volume) => _micGain = volume;

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