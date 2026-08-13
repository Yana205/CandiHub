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

        public void Sync(bool enabled, bool alwaysShow, float lineY, bool blinking, float now,
                         float wallLeft, float wallRight)
        {
            if (!IsBuilt) return;
            SetVisible(enabled && (alwaysShow || blinking));
            if (!Content.gameObject.activeSelf || line == null) return;

            // Idle sits near full strength — at the old 0.28 the hairline vanished into
            // the box art and players met the line for the first time at game over.
            float a = blinking ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(now * 7f)) : 0.9f;
            line.color = Palette.WithAlpha(Palette.Danger, a);
            // Sim px are only true if this view's root sits at the sim origin — the same
            // spot Bodies/FloatingText render from (local zero under ShakeRoot). A stray
            // drag has now offset it TWICE (114 px up during the build, 123 px up on
            // 2026-08-11 — a whole run lost to an invisible lose-height), so the root AND
            // Content are re-pinned every frame: neither has a legitimate position but
            // zero. Moving the line is legitimate — drag it in EDIT mode and save; the
            // authoring fold writes the new height into Tuning.topOutLine, which is what
            // this view renders and what TopOutWatch enforces.
            transform.localPosition = Vector3.zero;
            line.transform.parent.localPosition = Vector3.zero;
            line.transform.localPosition = new Vector3(
                (wallLeft + wallRight) * 0.5f * StageCoords.PX,
                -lineY * StageCoords.PX, 0f);
            // The dashes were sized for the mock's BL..BR; span the live tuned walls.
            line.transform.localScale = new Vector3(
                (wallRight - wallLeft) / (SimField.BR - SimField.BL), 1f, 1f);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Edit-mode feedback: the dashed sprite is a 2.5 px hairline that ducks behind
        /// chrome art the moment it leaves the box, so a scene-view drag could look like
        /// it did nothing at all. This draws wherever the sprite ACTUALLY sits — offsets
        /// and all — so a drag always has something visible following the mouse.
        /// </summary>
        void OnDrawGizmos()
        {
            if (Application.isPlaying) return;
            Transform content = transform.Find("Content");
            Transform lineT = content != null ? content.Find("DangerLine") : null;
            if (lineT == null) return;
            Vector3 p = lineT.position;
            Vector3 half = lineT.TransformVector(
                Vector3.right * (SimField.BR - SimField.BL) * 0.5f * StageCoords.PX);
            Gizmos.color = Palette.Danger;
            Gizmos.DrawLine(p - half, p + half);
        }
#endif
    }
}
