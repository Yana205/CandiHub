using PanDulce.Core;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>The desk behind the cloth — §8.5 step 1. Does NOT shake.</summary>
    public sealed class DeskView : GeneratedView
    {
        protected override void Build()
        {
            var t = Content;
            // top band + warm line
            ViewFactory.Rect(t, "TopBand", Shapes.White, 0f, 0f, 418f, 14f,
                             new Color(70f/255f, 44f/255f, 22f/255f, 0.45f), "PlayArea", 0);
            ViewFactory.Rect(t, "WarmLine", Shapes.White, 0f, 16f, 418f, 3f,
                             new Color(1f, 226f/255f, 182f/255f, 0.55f), "PlayArea", 1);
            // vertical plank lines
            foreach (float x in new[] { 58f, 150f, 268f, 390f })
                ViewFactory.Rect(t, "Plank", Shapes.White, x, 0f, 2f, 440f,
                                 new Color(92f/255f, 58f/255f, 26f/255f, 0.30f), "PlayArea", 1);
            // contact shadow under the pile
            var shadow = ViewFactory.Rect(t, "ContactShadow", Shapes.Circle(64),
                                          SimField.CX - 206f, 62f, 412f, 60f,
                                          new Color(64f/255f, 38f/255f, 16f/255f, 0.22f), "PlayArea", 2);
            shadow.drawMode = SpriteDrawMode.Simple;
            shadow.transform.localScale = new Vector3(412f / 64f, 60f / 64f, 1f);
            shadow.transform.localPosition = StageCoords.Stage(SimField.CX, 92f);
        }
    }
}
