using UnityEngine;

namespace PanDulce.Core
{
    /// <summary>
    /// Eases closeT 0↔1 over 1.1s and answers the three gating questions (§7.8).
    /// The end-of-day toggle is its only live driver — the game over used to force the
    /// fold too, until the flaps read as two capsules under the card (Yana, 2026-08-10).
    /// </summary>
    public sealed class DayCycle
    {
        public const float EaseSeconds = 1.1f;

        public float CloseT { get; private set; }

        /// <summary>Forces the fold regardless of the endOfDay knob. Nothing in the game sets
        /// it now that a top-out leaves the shop open — it is the one-line way back.</summary>
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
