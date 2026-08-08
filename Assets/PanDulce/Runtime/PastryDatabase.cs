using System.Collections.Generic;
using PanDulce.Core;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// One asset holding the tier table's art. Base radii live in Core's TierTable — this binds
    /// sprites, display names, and two ISOLATED per-tier size percentages: the play size
    /// (pile art + physics circle) and the case size (chrome icons only). Names and sizes
    /// travel WITH their sprite when the Studio window reorders the merge chain, so
    /// "which dessert is tier 3" is a data edit here, never a code change.
    /// </summary>
    [CreateAssetMenu(fileName = "Pastries", menuName = "Pan Dulce/Pastry Database")]
    public sealed class PastryDatabase : ScriptableObject
    {
        [Tooltip("11 sprites, tier 0..10, each authored at radius 200 in a 512² texture.")]
        [SerializeField] Sprite[] pastries = new Sprite[TierTable.Count];

        [Tooltip("Display names, tier 0..10 — reordered together with the sprites by Studio.")]
        [SerializeField] string[] names = new string[0];

        // Serialized name stays `artScale` so existing Pastries.asset values survive the rename
        // to TierSize — the meaning widened from "art only" to "art and physics".
        [Tooltip("Per-tier PLAY size, as a fraction of the authored size (1 = 100%). Scales " +
                 "the play-area sprite AND the physics circle, so an enlarged dessert also " +
                 "takes up more room in the pile.")]
        [SerializeField] float[] artScale = new float[0];

        [Tooltip("Per-tier CASE size (1 = 100%) — presentation only. Scales the icon in the " +
                 "glass case, order bubble, next plaque and serve flight, never the pile or " +
                 "physics. Keeps the shop window tidy however wild the play sizes get.")]
        [SerializeField] float[] caseScale = new float[0];

        [Tooltip("How much bigger each merge's result should be than its parent, as a " +
                 "fraction (0.21 = +21% per merge). The Studio window audits the chain " +
                 "against this and can rewrite Play sizes to match it exactly; the EditMode " +
                 "MergeGrowthTests enforce it per asset.")]
        [SerializeField] float mergeGrowth = 0.21f;

        [Tooltip("Allowed deviation around mergeGrowth before a step is flagged (0.05 = ±5%).")]
        [SerializeField] float mergeGrowthTolerance = 0.05f;

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

        /// <summary>
        /// PLAY size multiplier for a tier, as a fraction of its authored size (1 = 100%).
        ///
        /// Anything sized off the tier table already has this folded in by TierTable's
        /// EffectiveRadius/Er config overloads — never multiply by it again. Chrome drawn at
        /// a fixed radius (case seat, plaque, order bubble, serve flight) uses DisplaySize
        /// instead, so shop-window presentation stays isolated from pile physics.
        /// </summary>
        public float TierSize(int tier)
            => (artScale != null && tier >= 0 && tier < artScale.Length && artScale[tier] > 0f)
               ? artScale[tier] : 1f;

        /// <summary>
        /// CASE size multiplier (1 = 100%) — presentation only, for fixed-radius chrome icons.
        /// Independent of TierSize: resizing a dessert for gameplay never moves the shop
        /// window, and vice versa.
        /// </summary>
        public float DisplaySize(int tier)
            => (caseScale != null && tier >= 0 && tier < caseScale.Length && caseScale[tier] > 0f)
               ? caseScale[tier] : 1f;

        /// <summary>Target growth per merge as a fraction (+0.21 = each result 21% bigger).</summary>
        public float MergeGrowthTarget => mergeGrowth > 0f ? mergeGrowth : 0.21f;

        /// <summary>Allowed deviation around MergeGrowthTarget before a step is flagged.</summary>
        public float MergeGrowthTolerance => mergeGrowthTolerance >= 0f ? mergeGrowthTolerance : 0.05f;

        /// <summary>
        /// The visual/physical growth of the merge INTO this tier: how much bigger tier's
        /// effective radius is than the previous tier's, as a fraction (+0.21 = 21% bigger).
        /// SizeScale cancels out of the ratio, so this is the pure chain shape.
        /// </summary>
        public float MergeGrowth(int tier)
            => (TierTable.BaseRadius[tier] * TierSize(tier))
             / (TierTable.BaseRadius[tier - 1] * TierSize(tier - 1)) - 1f;

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
            if (caseScale == null || caseScale.Length != TierTable.Count)
            {
                var s = new float[TierTable.Count];
                for (int i = 0; i < s.Length; i++)
                    s[i] = (caseScale != null && i < caseScale.Length && caseScale[i] > 0f)
                           ? caseScale[i] : 1f;
                caseScale = s;
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
