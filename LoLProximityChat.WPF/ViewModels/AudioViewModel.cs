using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LoLProximityChat.Core.Models;
using NAudio.Wave;

namespace LoLProximityChat.WPF.ViewModels
{
    public class PlayerAudioEntry : INotifyPropertyChanged
    {
        public string Name { get; set; } = "";

        private float _volume = 1f;
        public float Volume
        {
            get => _volume;
            set { _volume = value; OnPropertyChanged(); OnPropertyChanged(nameof(VolumePercent)); }
        }

        private bool _isMuted;
        public bool IsMuted
        {
            get => _isMuted;
            set { _isMuted = value; OnPropertyChanged(); OnPropertyChanged(nameof(MuteIcon)); }
        }

        private bool _isInFog;
        public bool IsInFog
        {
            get => _isInFog;
            set { _isInFog = value; OnPropertyChanged(); }
        }

        private bool _isSpeaking;
        public bool IsSpeaking
        {
            get => _isSpeaking;
            set { _isSpeaking = value; OnPropertyChanged(); }
        }

        public int VolumePercent => (int)(_volume * 100);
        public string MuteIcon  => _isMuted ? "🔇" : "🔊";

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public class AudioViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<PlayerAudioEntry> Players { get; } = [];

        // ── Devices ───────────────────────────────────────────────────────────
        public ObservableCollection<string> InputDevices  { get; } = [];
        public ObservableCollection<string> OutputDevices { get; } = [];

        private string _selectedInput = "";
        public string SelectedInput
        {
            get => _selectedInput;
            set { _selectedInput = value; OnPropertyChanged(); }
        }

        private string _selectedOutput = "";
        public string SelectedOutput
        {
            get => _selectedOutput;
            set { _selectedOutput = value; OnPropertyChanged(); }
        }

        // ── Volume micro ──────────────────────────────────────────────────────
        private float _micVolume = 1f;
        public float MicVolume
        {
            get => _micVolume;
            set { _micVolume = value; OnPropertyChanged(); OnPropertyChanged(nameof(MicVolumePercent)); }
        }
        public int MicVolumePercent => (int)(_micVolume * 100);

        // ── Serveur ───────────────────────────────────────────────────────────
        private string _serverUrl = AppConfig.Load().ServerUrl;
        public string ServerUrl
        {
            get => _serverUrl;
            set { _serverUrl = value; OnPropertyChanged(); }
        }

        public bool SaveServerUrl()
        {
            var config = AppConfig.Load();
            var changed = config.ServerUrl != ServerUrl;
            config.ServerUrl = ServerUrl;
            config.Save();
            return changed;
        }

        // ── Init ──────────────────────────────────────────────────────────────
        public AudioViewModel()
        {
            LoadDevices();
        }

        private void LoadDevices()
        {
            InputDevices.Clear();
            for (int i = 0; i < WaveInEvent.DeviceCount; i++)
                InputDevices.Add(WaveInEvent.GetCapabilities(i).ProductName);

            OutputDevices.Clear();
            for (int i = 0; i < WaveOut.DeviceCount; i++)
                OutputDevices.Add(WaveOut.GetCapabilities(i).ProductName);

            if (InputDevices.Count > 0)  SelectedInput  = InputDevices[0];
            if (OutputDevices.Count > 0) SelectedOutput = OutputDevices[0];
        }

        // ── Appelé par GameSessionViewModel ───────────────────────────────────
        public void UpdateVolumes(Dictionary<string, float> volumes)
        {
            foreach (var (name, volume) in volumes)
            {
                var entry = Players.FirstOrDefault(p => p.Name == name);
                if (entry is null) continue;
                if (!entry.IsMuted)
                    entry.Volume = volume;
                entry.IsSpeaking = volume > 0.05f;
            }
        }

        public void AddPlayer(string name)
        {
            if (Players.Any(p => p.Name == name)) return;
            Players.Add(new PlayerAudioEntry { Name = name });
        }

        public void RemovePlayer(string name)
        {
            var entry = Players.FirstOrDefault(p => p.Name == name);
            if (entry != null) Players.Remove(entry);
        }

        public void UpdateFog(string name, bool inFog)
        {
            var entry = Players.FirstOrDefault(p => p.Name == name);
            if (entry != null) entry.IsInFog = inFog;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}