using PanDulce.Core;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// The desk behind the cloth — §8.5 step 1. Does NOT shake.
    /// V2 layout: the plank texture is drawn by the LayoutArt prefab now; only the contact
    /// shadow that grounds the pastry pile remains procedural.
    /// </summary>
    public sealed class DeskView : GeneratedView
    {
        protected override void Build()
        {
            var shadow = ViewFactory.Rect(Content, "ContactShadow", Shapes.Circle(64),
                                          SimField.CX - 206f, 62f, 412f, 60f,
                                          new Color(64f/255f, 38f/255f, 16f/255f, 0.22f), "PlayArea", 2);
            shadow.drawMode = SpriteDrawMode.Simple;
            shadow.transform.localScale = new Vector3(412f / 64f, 60f / 64f, 1f);
            shadow.transform.localPosition = StageCoords.Stage(SimField.CX, 92f);
        }
    }
}
