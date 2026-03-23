using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace LoLProximityChat.Core.Audio
{
    public class WindowsAudioService
    {
        // Ajuste le volume d'un processus Discord par username
        // discordUserVolumes : username Discord → volume 0.0 à 1.0
        public void UpdateDiscordVolumes(Dictionary<string, float> discordUserVolumes)
        {
            try
            {
                var sessionManager = GetAudioSessionManager();
                if (sessionManager == null) return;

                sessionManager.GetSessionEnumerator(out var enumerator);
                enumerator.GetCount(out int count);

                for (int i = 0; i < count; i++)
                {
                    enumerator.GetSession(i, out var session);
                    var session2 = session as IAudioSessionControl2;
                    if (session2 == null) continue;

                    session2.GetProcessId(out uint pid);
                    var process = System.Diagnostics.Process.GetProcessById((int)pid);

                    // Cherche les processus Discord
                    if (!process.ProcessName.ToLower().Contains("discord")) continue;

                    var simpleVolume = session as ISimpleAudioVolume;
                    if (simpleVolume == null) continue;

                    // Pour l'instant applique le volume moyen de tous les users Discord
                    // TODO : mapper par user Discord individuel via leur display name
                    var avgVolume = discordUserVolumes.Values.Any()
                        ? discordUserVolumes.Values.Average()
                        : 1f;

                    simpleVolume.SetMasterVolume(Math.Clamp(avgVolume, 0f, 1f), Guid.Empty);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AUDIO] Erreur volume Discord: {ex.Message}");
            }
        }

        private IAudioSessionManager2? GetAudioSessionManager()
        {
            var deviceEnumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var device);
            device.Activate(typeof(IAudioSessionManager2).GUID, 0, IntPtr.Zero, out var obj);
            return obj as IAudioSessionManager2;
        }

        // ── COM Interfaces ────────────────────────────────────────────────────

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumerator { }

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int NotImpl1();
            [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppDevice);
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig] int Activate(Guid iid, int dwClsCtx, IntPtr pActivationParams, out object ppInterface);
        }

        [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionManager2
        {
            int NotImpl1();
            int NotImpl2();
            [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator SessionEnum);
        }

        [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionEnumerator
        {
            [PreserveSig] int GetCount(out int SessionCount);
            [PreserveSig] int GetSession(int SessionCount, out IAudioSessionControl Session);
        }

        [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionControl { }

        [Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionControl2 : IAudioSessionControl
        {
            int NotImpl1();
            int NotImpl2();
            int NotImpl3();
            int NotImpl4();
            int NotImpl5();
            int NotImpl6();
            int NotImpl7();
            int NotImpl8();
            [PreserveSig] int GetProcessId(out uint pRetVal);
        }

        [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ISimpleAudioVolume
        {
            [PreserveSig] int SetMasterVolume(float fLevel, Guid EventContext);
            [PreserveSig] int GetMasterVolume(out float pfLevel);
        }

        private enum EDataFlow { eRender, eCapture, eAll }
        private enum ERole    { eConsole, eMultimedia, eCommunications }
    }
}