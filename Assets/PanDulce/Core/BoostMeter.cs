using System;
using UnityEngine;

namespace PanDulce.Core
{
    /// <summary>Charge accumulation, ready state, spend. Every merge charges it (§7.7).</summary>
    public sealed class BoostMeter
    {
        public float Charge { get; private set; }
        public bool Ready => Charge >= 1f;

        public event Action Changed;

        public void AddMerge(float chargePerMerge)
        {
            float next = Mathf.Min(1f, Charge + chargePerMerge);
            if (Mathf.Approximately(next, Charge)) return;
            Charge = next;
            Changed?.Invoke();
        }

        public void Fill()
        {
            if (Ready) return;
            Charge = 1f;
            Changed?.Invoke();
        }

        public void Spend()
        {
            if (Charge == 0f) return;
            Charge = 0f;
            Changed?.Invoke();
        }

        public void Reset() => Spend();
    }
}
