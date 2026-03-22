using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace LoLProximityChat.Core.Services
{
    public class MinimapRegion
    {
        public int X      { get; set; } = 1726;
        public int Y      { get; set; } = 800;
        public int Width  { get; set; } = 194;
        public int Height { get; set; } = 194;
    }

    public class MinimapCapture : IDisposable
    {
        private readonly MinimapRegion _region;
        
        private static readonly Scalar BlueMin  = new(95,  80,  80);
        private static readonly Scalar BlueMax  = new(135, 255, 255);
        private static readonly Scalar RedMin1  = new(0,   80,  80);
        private static readonly Scalar RedMax1  = new(15,  255, 255);
        private static readonly Scalar RedMin2  = new(165, 80,  80);
        private static readonly Scalar RedMax2  = new(180, 255, 255);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string? className, string windowName);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        public MinimapCapture(MinimapRegion? region = null)
        {
            _region = region ?? new MinimapRegion();
        }

        public List<(float x, float y, string team)> DetectBlobs()
        {
            using var screen = CaptureScreen();

            using var hsv = new Mat();
            Cv2.CvtColor(screen, hsv, ColorConversionCodes.BGR2HSV);

            using var maskBlue = new Mat();
            Cv2.InRange(hsv, BlueMin, BlueMax, maskBlue);

            using var maskRed1 = new Mat();
            using var maskRed2 = new Mat();
            using var maskRed  = new Mat();
            Cv2.InRange(hsv, RedMin1, RedMax1, maskRed1);
            Cv2.InRange(hsv, RedMin2, RedMax2, maskRed2);
            Cv2.BitwiseOr(maskRed1, maskRed2, maskRed);

            var results = new List<(float x, float y, string team)>();
            results.AddRange(FindBlobs(maskBlue, "ORDER"));
            results.AddRange(FindBlobs(maskRed,  "CHAOS"));

            return results;
        }

        private List<(float x, float y, string team)> FindBlobs(Mat mask, string team)
        {
            var results = new List<(float, float, string)>();

            using var kernel  = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(3, 3));
            using var dilated = new Mat();
            Cv2.Dilate(mask, dilated, kernel, iterations: 2);

            var parameters = new SimpleBlobDetector.Params
            {
                FilterByArea        = true,
                MinArea             = 30,
                MaxArea             = 300,
                FilterByCircularity = true,
                MinCircularity      = 0.6f,
                FilterByInertia     = true,
                MinInertiaRatio     = 0.5f,
                FilterByConvexity   = true,
                MinConvexity        = 0.8f,
                FilterByColor       = true,
                BlobColor           = 255
            };

            using var detector = SimpleBlobDetector.Create(parameters);
            var keypoints = detector.Detect(dilated);

            foreach (var kp in keypoints)
            {
                var lolX = (kp.Pt.X / _region.Width)  * 15000f;
                var lolY = (1f - kp.Pt.Y / _region.Height) * 15000f;
                results.Add((lolX, lolY, team));
            }

            return results;
        }
        private static IntPtr FindLoLWindow()
        {
            // Cherche par nom de process
            var processes = System.Diagnostics.Process.GetProcessesByName("League of Legends");
            if (processes.Length > 0 && processes[0].MainWindowHandle != IntPtr.Zero)
                return processes[0].MainWindowHandle;

            // Fallbacks sur le titre de fenêtre
            var hwnd = FindWindow(null, "League of Legends (TM) Client");
            if (hwnd != IntPtr.Zero) return hwnd;

            return FindWindow(null, "League of Legends");
        }

        private Mat CaptureScreen()
        {
            var hwnd = FindLoLWindow();

            if (hwnd == IntPtr.Zero)
            {
                Console.WriteLine("[CAPTURE] Fenêtre LoL introuvable — fallback écran");
                using var bmp = new Bitmap(_region.Width, _region.Height);
                using var g   = Graphics.FromImage(bmp);
                g.CopyFromScreen(_region.X, _region.Y, 0, 0,
                    new System.Drawing.Size(_region.Width, _region.Height));
                return BitmapConverter.ToMat(bmp);
            }

            GetWindowRect(hwnd, out RECT rect);
            int winW = rect.Right  - rect.Left;
            int winH = rect.Bottom - rect.Top;
            
            // Capturer la fenêtre LoL directement via PrintWindow
            using var fullBmp = new Bitmap(winW, winH);
            using var gFull   = Graphics.FromImage(fullBmp);
            var hdc           = gFull.GetHdc();
            PrintWindow(hwnd, hdc, 2); // PW_RENDERFULLCONTENT
            gFull.ReleaseHdc(hdc);

            // Position relative de la minimap dans la fenêtre LoL
            int relX = _region.X - rect.Left;
            int relY = _region.Y - rect.Top;

            // Clamp pour rester dans les limites
            relX = Math.Max(0, Math.Min(relX, winW - _region.Width));
            relY = Math.Max(0, Math.Min(relY, winH - _region.Height));

            var cropRect = new System.Drawing.Rectangle(relX, relY, _region.Width, _region.Height);
            using var cropped = fullBmp.Clone(cropRect, fullBmp.PixelFormat);
            return BitmapConverter.ToMat(cropped);
        }

        public void Dispose() { }
    }
}