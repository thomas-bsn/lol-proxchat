using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Text.Json;

namespace LoLProximityChat.Core.Services
{
    public enum TrackingState { Scanning, Locked }

    public class MinimapTracker : IDisposable
    {
        // ── Win32 ──────────────────────────────────────────────────────────────
        [DllImport("user32.dll")] static extern IntPtr FindWindow(string? cls, string title);
        [DllImport("user32.dll")] static extern bool   GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] static extern bool   PrintWindow(IntPtr h, IntPtr hdc, uint flags);
        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        // ── Config ─────────────────────────────────────────────────────────────
        private readonly int _minimapX, _minimapY, _minimapSize;

        // ── ONNX ───────────────────────────────────────────────────────────────
        private InferenceSession?         _session;
        private Dictionary<int, string>   _labelMap   = new();
        private int                        _localClassIndex = -1;

        // ── State ──────────────────────────────────────────────────────────────
        public  TrackingState State { get; private set; } = TrackingState.Scanning;
        private (float x, float y)?  _lastPixel;
        private (float x, float y)?  _lastGamePos;
        private float _velX, _velY;
        private int   _lockedTick;
        private int   _scanFrames;
        private int   _clsTick;

        // EMA-smoothed classifier scores keyed by "cx,cy"
        private Dictionary<string, float> _smoothedScores = new();
        private Dictionary<string, float> _currentScores  = new();

        // ── Blob struct ────────────────────────────────────────────────────────
        record Blob(string Color, int Pixels, float Cx, float Cy,
                    int MinX, int MaxX, int MinY, int MaxY, float FillRatio);

        // ── Icon size hint (7–9 % of minimap size) ─────────────────────────────
        private int ExpectedIconDiam => Math.Max(6, (int)(_minimapSize * 0.087f));

        // ──────────────────────────────────────────────────────────────────────
        public MinimapTracker(int minimapX, int minimapY, int minimapSize)
        {
            _minimapX    = minimapX;
            _minimapY    = minimapY;
            _minimapSize = minimapSize;
        }

        // ── Load model ─────────────────────────────────────────────────────────
        public void LoadModel(string onnxPath, string labelMapPath, string localChampionName)
        {
            var json = File.ReadAllText(labelMapPath);
            var raw  = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
            _labelMap = raw.ToDictionary(kv => int.Parse(kv.Key), kv => kv.Value);

            foreach (var (idx, name) in _labelMap)
                if (string.Equals(name, localChampionName, StringComparison.OrdinalIgnoreCase))
                { _localClassIndex = idx; break; }

            _session = new InferenceSession(onnxPath);
            Console.WriteLine($"[Tracker] Model loaded — champion={localChampionName} classIdx={_localClassIndex}");
        }

        // ── Main entry point ───────────────────────────────────────────────────
        /// <summary>Returns the local player's position in LoL game units (0–15000),
        /// or null if not yet locked.</summary>
        public (float x, float y)? Tick()
        {
            using var bmp = CaptureMinimapBitmap();
            if (bmp == null) return _lastGamePos;

            var (pixels, w, h) = LockBitmap(bmp);

            byte[] mask  = CreateMask(pixels, w, h);
            mask          = Dilate(mask, w, h);
            var allBlobs  = FindBlobs(mask, w, h);
            var iconBlobs = FilterIconBlobs(allBlobs);

            var (whiteMask, viewportMask) = BuildWhiteMasks(pixels, w, h);

            // Run ONNX every 4 ticks
            _clsTick++;
            var tealBlobs = iconBlobs.Where(b => b.Color == "teal").ToList();
            if (_session != null && _localClassIndex >= 0 && _clsTick % 4 == 0 && tealBlobs.Count > 0)
                UpdateClassifierScores(pixels, w, h, tealBlobs);

            (float x, float y)? result = State switch
            {
                TrackingState.Scanning => HandleScanning(iconBlobs, whiteMask, viewportMask, w),
                _                      => HandleLocked(iconBlobs, whiteMask, viewportMask, w),
            };

            if (result.HasValue) _lastGamePos = result;
            return _lastGamePos;
        }

        // ── Scanning ───────────────────────────────────────────────────────────
        private (float x, float y)? HandleScanning(
            List<Blob> iconBlobs, byte[] whiteMask, byte[] viewportMask, int w)
        {
            var tealBlobs = iconBlobs.Where(b => b.Color == "teal").ToList();
            if (tealBlobs.Count == 0) return null;

            _scanFrames++;
            bool hasClassifier = _session != null && _localClassIndex >= 0;
            int  warmup        = hasClassifier ? 8 : 4;
            if (_scanFrames < warmup) return _lastGamePos;

            Blob best      = tealBlobs[0];
            float bestScore = float.NegativeInfinity;

            foreach (var b in tealBlobs)
            {
                float cls   = GetClassifierScore(b);
                float white = WhitePixelScore(b, whiteMask, viewportMask, w);
                float ring  = Math.Min(1f, b.Pixels * (1f - b.FillRatio) / 200f);

                float score = hasClassifier
                    ? cls * 0.45f + white * 0.25f + ring * 0.30f
                    : white * 0.60f + ring * 0.40f;

                if (score > bestScore) { bestScore = score; best = b; }
            }

            return LockOnBlob(best);
        }

        private (float x, float y) LockOnBlob(Blob b)
        {
            _lastPixel    = (b.Cx, b.Cy);
            _velX = _velY = 0;
            _lockedTick   = 0;
            _scanFrames   = 0;
            State          = TrackingState.Locked;
            var pos        = PixelToGame(b.Cx, b.Cy);
            _lastGamePos   = pos;
            Console.WriteLine($"[Tracker] LOCKED pixel=({b.Cx:F0},{b.Cy:F0}) game=({pos.x:F0},{pos.y:F0})");
            return pos;
        }

        // ── Locked ─────────────────────────────────────────────────────────────
        private (float x, float y)? HandleLocked(
            List<Blob> iconBlobs, byte[] whiteMask, byte[] viewportMask, int w)
        {
            if (!_lastPixel.HasValue) return null;
            var tealBlobs = iconBlobs.Where(b => b.Color == "teal").ToList();
            bool hasClassifier = _session != null && _localClassIndex >= 0;

            if (tealBlobs.Count == 0) return Extrapolate();

            float lastRegX = _lastPixel.Value.x;
            float lastRegY = _lastPixel.Value.y;
            float predX    = lastRegX + _velX;
            float predY    = lastRegY + _velY;

            int baseJump    = Math.Max(20, ExpectedIconDiam * 2);
            int expansion   = _lockedTick > 0 ? (int)(ExpectedIconDiam * (_lockedTick / 8f)) : 0;
            float maxJump   = baseJump + expansion;
            float maxJumpSq = maxJump * maxJump;

            // Phase 1 — nearest blob
            Blob? best      = null;
            float bestScore = float.NegativeInfinity;

            foreach (var b in tealBlobs)
            {
                float dxL = b.Cx - lastRegX, dyL = b.Cy - lastRegY;
                if (dxL * dxL + dyL * dyL > maxJumpSq) continue;

                float dxP    = b.Cx - predX, dyP = b.Cy - predY;
                float posSc  = 1f - (dxP * dxP + dyP * dyP) / maxJumpSq;
                float white  = WhitePixelScore(b, whiteMask, viewportMask, w);
                float cls    = GetClassifierScore(b);

                if (hasClassifier && cls < 0.2f) continue;

                float score = hasClassifier
                    ? posSc * 0.35f + cls * 0.30f + white * 0.35f
                    : posSc * 0.55f + white * 0.45f;

                if (score > bestScore) { bestScore = score; best = b; }
            }

            // Phase 2 — classifier re-acquisition
            if (best == null && hasClassifier)
            {
                float threshold = _lockedTick > 8 ? 0.35f : 0.50f;
                float bestCls   = 0f;
                foreach (var b in tealBlobs)
                {
                    float cls = GetClassifierScore(b);
                    if (cls >= threshold && cls > bestCls) { bestCls = cls; best = b; }
                }
                if (best != null)
                {
                    _lastPixel  = (best.Cx, best.Cy);
                    _velX = _velY = 0;
                    _lockedTick++;
                    var rePos = PixelToGame(best.Cx, best.Cy);
                    _lastGamePos = rePos;
                    return rePos;
                }
            }

            if (best == null) return Extrapolate();

            // Update velocity (EMA)
            _velX = _velX * 0.5f + (best.Cx - lastRegX) * 0.5f;
            _velY = _velY * 0.5f + (best.Cy - lastRegY) * 0.5f;
            _lastPixel   = (best.Cx, best.Cy);
            _lockedTick  = 0;
            var gamePos  = PixelToGame(best.Cx, best.Cy);
            _lastGamePos = gamePos;
            return gamePos;
        }

        private (float x, float y)? Extrapolate()
        {
            _lockedTick++;
            float speed = MathF.Abs(_velX) + MathF.Abs(_velY);
            if (speed > 0.1f && _lastPixel.HasValue)
            {
                float nx = Math.Clamp(_lastPixel.Value.x + _velX, 0, _minimapSize - 1);
                float ny = Math.Clamp(_lastPixel.Value.y + _velY, 0, _minimapSize - 1);
                _lastPixel   = (nx, ny);
                _lastGamePos = PixelToGame(nx, ny);
                _velX *= 0.7f;
                _velY *= 0.7f;
            }
            return _lastGamePos;
        }

        // ── Coordinate conversion ──────────────────────────────────────────────
        private (float x, float y) PixelToGame(float px, float py)
        {
            float relX = Math.Clamp(px / _minimapSize, 0f, 1f);
            float relY = Math.Clamp(py / _minimapSize, 0f, 1f);
            return (relX * 15000f, (1f - relY) * 15000f);
        }

        // ── ONNX classifier ────────────────────────────────────────────────────
        private void UpdateClassifierScores(byte[] pixels, int w, int h, List<Blob> tealBlobs)
        {
            if (_session == null || _localClassIndex < 0) return;

            var rawScores = new float[tealBlobs.Count];
            for (int i = 0; i < tealBlobs.Count; i++)
                rawScores[i] = ScoreBlob(pixels, w, h, tealBlobs[i]);

            float maxRaw = rawScores.Max();
            float[] norm = maxRaw >= 0.005f
                ? rawScores.Select(s => s / maxRaw).ToArray()
                : new float[tealBlobs.Count];

            const float EMA = 0.4f;
            float tol       = Math.Max(5f, ExpectedIconDiam * 0.6f);
            float tolSq     = tol * tol;

            _currentScores.Clear();
            for (int i = 0; i < tealBlobs.Count; i++)
            {
                var b   = tealBlobs[i];
                string key = $"{b.Cx:F0},{b.Cy:F0}";
                float prior = -1f;
                float bestD = float.MaxValue;

                foreach (var (sk, sv) in _smoothedScores)
                {
                    var parts = sk.Split(',');
                    float sx  = float.Parse(parts[0]), sy = float.Parse(parts[1]);
                    float dx  = b.Cx - sx, dy = b.Cy - sy;
                    float d   = dx * dx + dy * dy;
                    if (d < tolSq && d < bestD) { bestD = d; prior = sv; }
                }

                float smoothed = prior >= 0f
                    ? prior * (1f - EMA) + norm[i] * EMA
                    : norm[i];

                _currentScores[key] = smoothed;
            }

            _smoothedScores = new Dictionary<string, float>(_currentScores);
        }

        private float ScoreBlob(byte[] pixels, int w, int h, Blob b)
        {
            if (_session == null) return 0f;

            // Crop + resize to 32×32
            int cropX = Math.Max(0, b.MinX - 1);
            int cropY = Math.Max(0, b.MinY - 1);
            int cropW = Math.Min(w - cropX, b.MaxX - b.MinX + 3);
            int cropH = Math.Min(h - cropY, b.MaxY - b.MinY + 3);
            if (cropW <= 0 || cropH <= 0) return 0f;

            var tensor = new DenseTensor<float>(new[] { 1, 3, 32, 32 });
            for (int ty = 0; ty < 32; ty++)
            {
                for (int tx = 0; tx < 32; tx++)
                {
                    int sx = cropX + (int)(tx / 31f * (cropW - 1));
                    int sy = cropY + (int)(ty / 31f * (cropH - 1));
                    int pi = (sy * w + sx) * 4;
                    tensor[0, 0, ty, tx] = pixels[pi]     / 255f; // R
                    tensor[0, 1, ty, tx] = pixels[pi + 1] / 255f; // G
                    tensor[0, 2, ty, tx] = pixels[pi + 2] / 255f; // B
                }
            }

            var inputs  = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input", tensor) };
            using var results = _session.Run(inputs);
            var logits  = results.First().AsEnumerable<float>().ToArray();

            float maxL  = logits.Max();
            float sumE  = logits.Sum(l => MathF.Exp(l - maxL));
            float localE = MathF.Exp(logits[_localClassIndex] - maxL);
            return localE / sumE;
        }

        private float GetClassifierScore(Blob b)
        {
            string key = $"{b.Cx:F0},{b.Cy:F0}";
            if (_currentScores.TryGetValue(key, out float v)) return v;

            float tol   = Math.Max(5f, ExpectedIconDiam * 0.6f);
            float tolSq = tol * tol;
            float best  = 0f, bestD = float.MaxValue;

            foreach (var (sk, sv) in _currentScores)
            {
                var parts = sk.Split(',');
                float sx  = float.Parse(parts[0]), sy = float.Parse(parts[1]);
                float dx  = b.Cx - sx, dy = b.Cy - sy;
                float d   = dx * dx + dy * dy;
                if (d < tolSq && d < bestD) { bestD = d; best = sv; }
            }
            return best;
        }

        // ── Color mask ─────────────────────────────────────────────────────────
        // 0 = background, 1 = teal, 2 = red
        private static byte[] CreateMask(byte[] px, int w, int h)
        {
            var mask = new byte[w * h];
            for (int i = 0; i < w * h; i++)
            {
                int pi = i * 4;
                byte r = px[pi], g = px[pi + 1], bl = px[pi + 2];
                if (r < 100 && g > 120 && bl > 120 && (g + bl) > 280) mask[i] = 1; // teal
                else if (r > 140 && g < 100 && bl < 100)               mask[i] = 2; // red
            }
            return mask;
        }

        private static byte[] Dilate(byte[] mask, int w, int h)
        {
            var r = (byte[])mask.Clone();
            for (int y = 1; y < h - 1; y++)
            for (int x = 1; x < w - 1; x++)
            {
                int idx = y * w + x;
                if (r[idx] != 0) continue;
                r[idx] = mask[(y-1)*w+x] != 0 ? mask[(y-1)*w+x] :
                         mask[(y+1)*w+x] != 0 ? mask[(y+1)*w+x] :
                         mask[y*w+x-1]   != 0 ? mask[y*w+x-1]   :
                         mask[y*w+x+1];
            }
            return r;
        }

        // ── Blob detection ─────────────────────────────────────────────────────
        private static List<Blob> FindBlobs(byte[] mask, int w, int h)
        {
            var visited = new bool[w * h];
            var blobs   = new List<Blob>();

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                if (visited[idx] || mask[idx] == 0) continue;

                byte  target = mask[idx];
                string color = target == 1 ? "teal" : "red";
                var   stack  = new Stack<(int x, int y)>();
                stack.Push((x, y));

                int sumX = 0, sumY = 0, count = 0;
                int minX = x, maxX = x, minY = y, maxY = y;

                while (stack.Count > 0)
                {
                    var (cx, cy) = stack.Pop();
                    if (cx < 0 || cx >= w || cy < 0 || cy >= h) continue;
                    int ci = cy * w + cx;
                    if (visited[ci] || mask[ci] != target) continue;
                    visited[ci] = true;
                    sumX += cx; sumY += cy; count++;
                    if (cx < minX) minX = cx; if (cx > maxX) maxX = cx;
                    if (cy < minY) minY = cy; if (cy > maxY) maxY = cy;
                    stack.Push((cx-1,cy)); stack.Push((cx+1,cy));
                    stack.Push((cx,cy-1)); stack.Push((cx,cy+1));
                }

                if (count >= 10)
                {
                    int area  = (maxX - minX + 1) * (maxY - minY + 1);
                    float fill = area > 0 ? count / (float)area : 1f;
                    blobs.Add(new Blob(color, count,
                        sumX / (float)count, sumY / (float)count,
                        minX, maxX, minY, maxY, fill));
                }
            }
            return blobs;
        }

        private List<Blob> FilterIconBlobs(List<Blob> blobs)
        {
            float diam   = ExpectedIconDiam;
            if (diam < 5) return blobs;
            float minSz  = diam * 0.6f, maxSz = diam * 1.6f;

            return blobs.Where(b =>
            {
                float bw = b.MaxX - b.MinX + 1, bh = b.MaxY - b.MinY + 1;
                if (bw < minSz || bw > maxSz || bh < minSz || bh > maxSz) return false;
                float asp = bw / bh;
                if (asp < 0.6f || asp > 1.7f) return false;
                if (b.Pixels < 15)            return false;
                if (b.FillRatio > 0.40f)      return false;
                if (b.FillRatio < 0.08f)      return false;
                return true;
            }).ToList();
        }

        // ── White pixel / viewport masks ───────────────────────────────────────
        private static (byte[] whiteMask, byte[] viewportMask) BuildWhiteMasks(byte[] px, int w, int h)
        {
            var white    = new byte[w * h];
            var viewport = new byte[w * h];
            const int RUN = 12;

            for (int i = 0; i < w * h; i++)
            {
                int pi = i * 4;
                if (px[pi] > 200 && px[pi+1] > 200 && px[pi+2] > 200) white[i] = 1;
            }

            // Horizontal runs
            for (int y = 0; y < h; y++)
            {
                int rs = -1;
                for (int x = 0; x <= w; x++)
                {
                    bool isW = x < w && white[y * w + x] == 1;
                    if (isW && rs < 0) rs = x;
                    else if (!isW && rs >= 0)
                    {
                        if (x - rs >= RUN) for (int rx = rs; rx < x; rx++) viewport[y*w+rx] = 1;
                        rs = -1;
                    }
                }
            }

            // Vertical runs
            for (int x = 0; x < w; x++)
            {
                int rs = -1;
                for (int y = 0; y <= h; y++)
                {
                    bool isW = y < h && white[y * w + x] == 1;
                    if (isW && rs < 0) rs = y;
                    else if (!isW && rs >= 0)
                    {
                        if (y - rs >= RUN) for (int ry = rs; ry < y; ry++) viewport[ry*w+x] = 1;
                        rs = -1;
                    }
                }
            }

            return (white, viewport);
        }

        private float WhitePixelScore(Blob b, byte[] white, byte[] viewport, int w)
        {
            int pad = Math.Max(4, (int)(ExpectedIconDiam * 0.3f));
            int x0  = Math.Max(0, b.MinX - pad), y0 = Math.Max(0, b.MinY - pad);
            int x1  = Math.Min(w - 1, b.MaxX + pad), y1 = Math.Min(w - 1, b.MaxY + pad);
            int count = 0;

            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (x >= b.MinX && x <= b.MaxX && y >= b.MinY && y <= b.MaxY) continue;
                int idx = y * w + x;
                if (white[idx] == 1 && viewport[idx] == 0) count++;
            }
            return Math.Min(1f, count / 8f);
        }

        // ── Screen capture ─────────────────────────────────────────────────────
        private Bitmap? CaptureMinimapBitmap()
        {
            var hwnd = FindLoLWindow();
            if (hwnd == IntPtr.Zero)
            {
                // Fallback: screen capture
                try
                {
                    var bmp = new Bitmap(_minimapSize, _minimapSize);
                    using var g = Graphics.FromImage(bmp);
                    g.CopyFromScreen(_minimapX, _minimapY, 0, 0,
                        new System.Drawing.Size(_minimapSize, _minimapSize));
                    return bmp;
                }
                catch { return null; }
            }

            GetWindowRect(hwnd, out RECT rect);
            int winW = rect.Right - rect.Left, winH = rect.Bottom - rect.Top;
            if (winW <= 0 || winH <= 0) return null;

            using var full = new Bitmap(winW, winH);
            using (var g = Graphics.FromImage(full))
            {
                var hdc = g.GetHdc();
                PrintWindow(hwnd, hdc, 2);
                g.ReleaseHdc(hdc);
            }

            int relX = Math.Max(0, Math.Min(_minimapX - rect.Left, winW - _minimapSize));
            int relY = Math.Max(0, Math.Min(_minimapY - rect.Top,  winH - _minimapSize));
            return full.Clone(new Rectangle(relX, relY, _minimapSize, _minimapSize),
                              full.PixelFormat);
        }

        private static IntPtr FindLoLWindow()
        {
            var procs = System.Diagnostics.Process.GetProcessesByName("League of Legends");
            if (procs.Length > 0 && procs[0].MainWindowHandle != IntPtr.Zero)
                return procs[0].MainWindowHandle;
            var h = FindWindow(null, "League of Legends (TM) Client");
            return h != IntPtr.Zero ? h : FindWindow(null, "League of Legends");
        }

        // ── Lock bitmap pixels ─────────────────────────────────────────────────
        private static (byte[] pixels, int w, int h) LockBitmap(Bitmap bmp)
        {
            int w   = bmp.Width, h = bmp.Height;
            var bd  = bmp.LockBits(new Rectangle(0, 0, w, h),
                          ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var px  = new byte[w * h * 4];
            Marshal.Copy(bd.Scan0, px, 0, px.Length);
            bmp.UnlockBits(bd);
            // ARGB → RGBA reorder
            for (int i = 0; i < px.Length; i += 4)
                (px[i], px[i+2]) = (px[i+2], px[i]);
            return (px, w, h);
        }

        public void Reset()
        {
            State        = TrackingState.Scanning;
            _lastPixel   = null;
            _lastGamePos = null;
            _velX = _velY = 0;
            _lockedTick = _scanFrames = _clsTick = 0;
            _smoothedScores.Clear();
            _currentScores.Clear();
        }

        public void Dispose() => _session?.Dispose();
    }
}