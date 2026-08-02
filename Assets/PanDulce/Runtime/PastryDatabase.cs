using PanDulce.Core;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// One asset holding the tier table's art. Radii and names live in Core's TierTable —
    /// this only binds sprites, so there are no magic numbers scattered through code.
    ///
    /// Swapping placeholder art for final art is a field assignment here and nothing else.
    /// </summary>
    [CreateAssetMenu(fileName = "Pastries", menuName = "Pan Dulce/Pastry Database")]
    public sealed class PastryDatabase : ScriptableObject
    {
        [Tooltip("11 sprites, tier 0..10, each authored at radius 200 in a 512² texture.")]
        [SerializeField] Sprite[] pastries = new Sprite[TierTable.Count];

        [Tooltip("3 regulars, cycled by served % 3.")]
        [SerializeField] Sprite[] customers = new Sprite[3];

        public Sprite Pastry(int tier)
            => (pastries != null && tier >= 0 && tier < pastries.Length) ? pastries[tier] : null;

        public Sprite Customer(int index)
            => (customers != null && customers.Length > 0) ? customers[index % customers.Length] : null;

        public string Name(int tier) => TierTable.Names[tier];

        public int PastryCount => pastries?.Length ?? 0;

#if UNITY_EDITOR
        public void EditorAssign(Sprite[] p, Sprite[] c) { pastries = p; customers = c; }
#endif
    }
}
