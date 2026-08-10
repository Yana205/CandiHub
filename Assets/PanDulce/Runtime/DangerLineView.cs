using PanDulce.Core;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// The danger line. Not in the mock — it comes with the top-out layer, and the cloth had
    /// no visual for it, so this is new: a dashed rule across the cloth mouth that sits at
    /// low alpha normally and blinks once the pile is actually over it.
    /// </summary>
    public sealed class DangerLineView : GeneratedView
    {
        SpriteRenderer line;

        protected override void Build()
        {
            line = ViewFactory.Rect(Content, "DangerLine", Shapes.Dashes(9, 7, 3),
                                    SimField.BL, 82f, SimField.BR - SimField.BL, 2.5f,
                                    Palette.Danger, "PlayArea", 15);
        }

        public void Sync(bool enabled, bool alwaysShow, float lineY, bool blinking, float now)
        {
            if (!IsBuilt) return;
            SetVisible(enabled && (alwaysShow || blinking));
            if (!Content.gameObject.activeSelf || line == null) return;

            float a = blinking ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(now * 7f)) : 0.28f;
            line.color = Palette.WithAlpha(Palette.Danger, a);
            line.transform.localPosition = new Vector3(
                (SimField.BL + (SimField.BR - SimField.BL) * 0.5f) * StageCoords.PX,
                -lineY * StageCoords.PX, 0f);
        }
    }
}
