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
        /// <summary>
        /// One parallel art track for the whole chain: a color variant per tier. An empty
        /// sprite slot means "this tier has no variant here" and falls back to the Original
        /// track — that is how Purin and Roll Cake are shared by every skin. Sizes are NOT
        /// per-track on purpose: a skin can never change gameplay.
        /// </summary>
        [System.Serializable]
        public sealed class SkinTrack
        {
            public string trackName = "Skin";

            [Tooltip("Per-tier variant sprite; empty = use the Original track's sprite.")]
            public Sprite[] sprites = new Sprite[TierTable.Count];

            [Tooltip("Per-tier display name; empty = the Original track's name.")]
            public string[] names = new string[TierTable.Count];
        }

        [Tooltip("The Original track: one sprite per tier 0..4, each authored at radius 200 " +
                 "in a 512² texture.")]
        [SerializeField] Sprite[] pastries = new Sprite[TierTable.Count];

        [Tooltip("Original display names, tier 0..4 — reordered together with the sprites by Studio.")]
        [SerializeField] string[] names = new string[0];

        [Tooltip("Parallel skin tracks — same 5 tiers, alternate colors. Blank slots fall " +
                 "back to the Original sprite/name above.")]
        [SerializeField] List<SkinTrack> skins = new List<SkinTrack>();

        [Tooltip("Which track the game draws right now: 0 = Original, 1.. = skins. " +
                 "Presentation only — sizes and physics never change with it.")]
        [SerializeField] int activeSkin;

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
        {
            var skin = ActiveTrack;
            if (skin != null && tier >= 0 && tier < skin.sprites.Length && skin.sprites[tier] != null)
                return skin.sprites[tier];
            return (pastries != null && tier >= 0 && tier < pastries.Length) ? pastries[tier] : null;
        }

        // ---------------------------------------------------------------- skin tracks

        /// <summary>Track count including Original, so valid ActiveSkin values are 0..SkinCount-1.</summary>
        public int SkinCount => 1 + (skins?.Count ?? 0);

        /// <summary>0 = Original, 1.. = skin tracks. Clamped; presentation only.</summary>
        public int ActiveSkin
        {
            get => Mathf.Clamp(activeSkin, 0, SkinCount - 1);
            set => activeSkin = Mathf.Clamp(value, 0, SkinCount - 1);
        }

        public string SkinName(int track)
            => track <= 0 || skins == null || track > skins.Count
               ? "Original"
               : string.IsNullOrEmpty(skins[track - 1].trackName) ? $"Skin {track}" : skins[track - 1].trackName;

        SkinTrack ActiveTrack
            => skins != null && ActiveSkin > 0 && ActiveSkin <= skins.Count ? skins[ActiveSkin - 1] : null;

        public Sprite Customer(int index)
            => (customers != null && customers.Length > 0) ? customers[index % customers.Length] : null;

        public Sprite WindowScene => windowScene;
        public Sprite CounterOpening => counterOpening;

        public string Name(int tier)
        {
            var skin = ActiveTrack;
            if (skin != null && tier >= 0 && tier < skin.names.Length
                && skin.sprites[tier] != null && !string.IsNullOrEmpty(skin.names[tier]))
                return skin.names[tier];
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

        /// <summary>Keeps every per-tier array sized to the tier count without clobbering edits.</summary>
        void EnsureArrays()
        {
            if (pastries == null || pastries.Length != TierTable.Count)
            {
                var p = new Sprite[TierTable.Count];
                for (int i = 0; i < p.Length && pastries != null && i < pastries.Length; i++)
                    p[i] = pastries[i];
                pastries = p;
            }
            if (skins != null)
                foreach (var t in skins)
                {
                    if (t.sprites == null || t.sprites.Length != TierTable.Count)
                        System.Array.Resize(ref t.sprites, TierTable.Count);
                    if (t.names == null || t.names.Length != TierTable.Count)
                        System.Array.Resize(ref t.names, TierTable.Count);
                }
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
        /// Rebinds baked sprites while preserving the authored layout: every incoming sprite
        /// that already lives somewhere (Original slot or a skin slot, matched by name) is
        /// refreshed in place; leftovers fill empty Original slots in filename order. Sprites
        /// with no home left are reported, never silently dropped — park them in a skin slot
        /// via Studio.
        /// </summary>
        public void EditorAssign(Sprite[] p, Sprite[] c)
        {
            customers = c;
            EnsureArrays();

            var pool = new List<Sprite>();
            if (p != null) foreach (var s in p) if (s != null) pool.Add(s);

            Reclaim(pastries, pool);
            if (skins != null) foreach (var t in skins) Reclaim(t.sprites, pool);

            for (int i = 0; i < pastries.Length && pool.Count > 0; i++)
                if (pastries[i] == null) { pastries[i] = pool[0]; pool.RemoveAt(0); }

            if (pool.Count > 0)
                Debug.LogWarning("[PanDulce] baked sprites with no chain or skin slot: " +
                                 string.Join(", ", pool.ConvertAll(s => s.name)) +
                                 " — assign them to a skin track in Studio, or delete the files.");
        }

        /// <summary>Swap each already-placed sprite for its same-named incoming twin.</summary>
        static void Reclaim(Sprite[] slots, List<Sprite> pool)
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                int idx = pool.FindIndex(s => s.name == slots[i].name);
                if (idx >= 0) { slots[i] = pool[idx]; pool.RemoveAt(idx); }
            }
        }

        public void EditorAssignShell(Sprite window, Sprite opening) { windowScene = window; counterOpening = opening; }
#endif
    }
}
