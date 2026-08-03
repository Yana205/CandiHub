using UnityEngine;

namespace PanDulce.Core
{
    /// <summary>
    /// Eases closeT 0↔1 over 1.1s and answers the three gating questions (§7.8).
    /// Also the game-over presentation: a top-out forces the cloth shut, reusing the fold.
    /// </summary>
    public sealed class DayCycle
    {
        public const float EaseSeconds = 1.1f;

        public float CloseT { get; private set; }

        /// <summary>Set when the run has ended — forces the fold regardless of the endOfDay knob.</summary>
        public bool ForcedClosed { get; set; }

        public void Tick(float dt, bool endOfDay)
        {
            float target = (endOfDay || ForcedClosed) ? 1f : 0f;
            float d = target - CloseT;
            if (Mathf.Abs(d) > 0.001f)
                CloseT += Mathf.Sign(d) * Mathf.Min(Mathf.Abs(d), dt / EaseSeconds);
            else
                CloseT = target;
        }

        public bool CanDrop => CloseT <= 0.12f;
        public bool CanShake => CloseT <= 0.12f;
        public bool TimerRuns => CloseT < 0.4f;
        public bool DrawFold => CloseT > 0.002f;

        public void Reset()
        {
            CloseT = 0f;
            ForcedClosed = false;
        }
    }
}
