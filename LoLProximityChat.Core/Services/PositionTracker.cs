namespace LoLProximityChat.Core.Services
{
    public class PositionTracker
    {
        private const float MaxJump = 2000f;

        private (float x, float y) _last = (-1, -1);
        private bool _hasPosition;

        // Retourne la position stabilisée ou null si faux positif
        public (float x, float y)? TryUpdate(float x, float y)
        {
            var candidate = (x, y);

            if (!_hasPosition)
            {
                _last        = candidate;
                _hasPosition = true;
                return candidate;
            }

            if (Distance(_last, candidate) > MaxJump)
                return null; // saut trop grand → faux positif, on ignore

            _last = candidate;
            return candidate;
        }

        public (float x, float y)? LastPosition => _hasPosition ? _last : null;

        public void Reset()
        {
            _last        = (-1, -1);
            _hasPosition = false;
        }

        private static float Distance((float x, float y) a, (float x, float y) b)
            => MathF.Sqrt(MathF.Pow(a.x - b.x, 2) + MathF.Pow(a.y - b.y, 2));
    }
}