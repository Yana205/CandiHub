using System.Collections.Generic;
using PanDulce.Core;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// One asset holding the tier table's art. Radii live in Core's TierTable — this binds
    /// sprites, display names, and per-tier visual scale. Names and scales travel WITH their
    /// sprite when the Studio window reorders the merge chain, so "which dessert is tier 3"
    /// is a data edit here, never a code change.
    /// </summary>
    [CreateAssetMenu(fileName = "Pastries", menuName = "Pan Dulce/Pastry Database")]
    public sealed class PastryDatabase : ScriptableObject
    {
        [Tooltip("11 sprites, tier 0..10, each authored at radius 200 in a 512² texture.")]
        [SerializeField] Sprite[] pastries = new Sprite[TierTable.Count];

        [Tooltip("Display names, tier 0..10 — reordered together with the sprites by Studio.")]
        [SerializeField] string[] names = new string[0];

        [Tooltip("Per-tier visual scale. Art only — the physics radius never changes.")]
        [SerializeField] float[] artScale = new float[0];

        [Tooltip("3 regulars, cycled by served % 3.")]
        [SerializeField] Sprite[] customers = new Sprite[3];

        [Tooltip("Baked window scenery, 378×206 stage px at 2x.")]
        [SerializeField] Sprite windowScene;

        [Tooltip("The red/white opening the bear pops through, 180×80 stage px at 2x.")]
        [SerializeField] Sprite counterOpening;

        public Sprite Pastry(int tier)
            => (pastries != null && tier >= 0 && tier < pastries.Length) ? pastries[tier] : null;

        public Sprite Customer(int index)
            => (customers != null && customers.Length > 0) ? customers[index % customers.Length] : null;

        public Sprite WindowScene => windowScene;
        public Sprite CounterOpening => counterOpening;

        public string Name(int tier)
        {
            if (names != null && tier >= 0 && tier < names.Length && !string.IsNullOrEmpty(names[tier]))
                return names[tier];
            return (tier >= 0 && tier < TierTable.Count) ? TierTable.Names[tier] : "?";
        }

        /// <summary>Visual-only size multiplier for a tier's sprite (1 = authored size).</summary>
        public float ArtScale(int tier)
            => (artScale != null && tier >= 0 && tier < artScale.Length && artScale[tier] > 0f)
               ? artScale[tier] : 1f;

        public int PastryCount => pastries?.Length ?? 0;

        void OnEnable() => EnsureArrays();
        void OnValidate() => EnsureArrays();

        /// <summary>Keeps names/artScale sized to the tier count without clobbering edits.</summary>
        void EnsureArrays()
        {
            if (names == null || names.Length != TierTable.Count)
            {
                var n = new string[TierTable.Count];
                for (int i = 0; i < n.Length; i++)
                    n[i] = (names != null && i < names.Length && !string.IsNullOrEmpty(names[i]))
                           ? names[i] : TierTable.Names[i];
                names = n;
            }
            if (artScale == null || artScale.Length != TierTable.Count)
            {
                var s = new float[TierTable.Count];
                for (int i = 0; i < s.Length; i++)
                    s[i] = (artScale != null && i < artScale.Length && artScale[i] > 0f)
                           ? artScale[i] : 1f;
                artScale = s;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Rebinds baked sprites while preserving a Studio-authored chain order: incoming
        /// sprites that match current entries by name keep their tier, new ones append in
        /// filename order. A fresh database just takes the incoming order.
        /// </summary>
        public void EditorAssign(Sprite[] p, Sprite[] c)
        {
            pastries = ReorderLikeExisting(pastries, p);
            customers = c;
            EnsureArrays();
        }

        public void EditorAssignShell(Sprite window, Sprite opening) { windowScene = window; counterOpening = opening; }

        static Sprite[] ReorderLikeExisting(Sprite[] existing, Sprite[] incoming)
        {
            if (existing == null || incoming == null) return incoming;
            bool hasAny = false;
            foreach (var e in existing) if (e != null) { hasAny = true; break; }
            if (!hasAny) return incoming;

            var pool = new List<Sprite>(incoming);
            var result = new List<Sprite>(incoming.Length);
            foreach (var e in existing)
            {
                if (e == null) continue;
                int idx = pool.FindIndex(s => s != null && s.name == e.name);
                if (idx >= 0) { result.Add(pool[idx]); pool.RemoveAt(idx); }
            }
            result.AddRange(pool);
            return result.ToArray();
        }
#endif
    }
}
