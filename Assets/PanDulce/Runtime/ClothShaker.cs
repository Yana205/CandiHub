using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>Offsets ClothShakeRoot during a shake — desk and NEXT plaque stay still (§7.7).</summary>
    public sealed class ClothShaker : MonoBehaviour
    {
        public void Sync(Vector2 simOffset)
        {
            transform.localPosition = new Vector3(simOffset.x * StageCoords.PX,
                                                   -simOffset.y * StageCoords.PX, 0f);
        }
    }
}
