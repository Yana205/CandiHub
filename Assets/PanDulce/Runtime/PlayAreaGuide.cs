using PanDulce.Core;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// Visible guides for the play area — real renderers like the danger line, so they
    /// show in BOTH the Scene view and the Game view, in edit mode and in play mode.
    /// Uncheck this GameObject in the hierarchy to hide them all; per-guide toggles live
    /// in the inspector and apply live.
    ///
    ///  - Floor curve (orange): the sagging line the desserts actually rest on.
    ///  - Container (blue): the walls + floor — the box the pile is held inside.
    ///  - Canvas frame (gray): the full sim canvas rect.
    ///  - Spawn line (green): the height new desserts drop from.
    ///  - Top-out line (amber): pile past this for too long = game over (the danger
    ///    line view already blinks here during play).
    ///
    /// The curve and lines follow the live Tuning asset, so tweaking FloorSag or
    /// TopOutLine moves them immediately.
    /// </summary>
    public sealed class PlayAreaGuide : GeneratedView
    {
        [SerializeField] TuningConfig tuning;

        [Header("Show")]
        [SerializeField] bool floorCurve = true;
        [SerializeField] bool container = true;
        [SerializeField] bool canvasFrame = false;
        [SerializeField] bool spawnLine = false;
        [SerializeField] bool topOutLine = false;

        const int FloorSamples = 32;

        LineRenderer floorLr, containerLr, frameLr, spawnLr, topOutLr;
        float lastSag = float.NaN, lastTopOut = float.NaN, lastFloorY = float.NaN;
        float lastWL = float.NaN, lastWR = float.NaN;

        public void EditorAssign(TuningConfig cfg)
        {
            tuning = cfg;
            Rebuild();
        }

        float FloorSag => tuning != null ? tuning.FloorSag : 26f;
        float FloorY => tuning != null ? tuning.FloorY : SimField.FY;
        float TopOutY => tuning != null ? tuning.TopOutLine : 82f;
        float WallL => tuning != null ? tuning.WallLeft : SimField.WL;
        float WallR => tuning != null ? tuning.WallRight : SimField.WR;

        protected override void Build()
        {
            floorLr = MakeLine("FloorCurve", new Color(1f, 0.34f, 0.13f, 0.85f), 91, 3f);
            containerLr = MakeLine("Container", new Color(0.15f, 0.55f, 1f, 0.6f), 90, 2.5f);
            frameLr = MakeLine("CanvasFrame", new Color(0.45f, 0.45f, 0.45f, 0.55f), 90, 2f);
            frameLr.loop = true;
            spawnLr = MakeLine("SpawnLine", new Color(0.2f, 0.8f, 0.3f, 0.7f), 90, 2f);
            topOutLr = MakeLine("TopOutLine", new Color(1f, 0.75f, 0.1f, 0.7f), 90, 2f);

            frameLr.positionCount = 4;
            frameLr.SetPosition(0, P(0f, 0f));
            frameLr.SetPosition(1, P(SimField.CW, 0f));
            frameLr.SetPosition(2, P(SimField.CW, SimField.CH));
            frameLr.SetPosition(3, P(0f, SimField.CH));

            lastSag = lastTopOut = float.NaN;   // force the first Resample
            Resample();
            ApplyToggles();
        }

        /// <summary>Rebuilds the tuning-dependent shapes: floor curve, container, spawn, top-out.</summary>
        void Resample()
        {
            float sag = FloorSag, fy = FloorY, wl = WallL, wr = WallR;

            floorLr.positionCount = FloorSamples + 1;
            for (int i = 0; i <= FloorSamples; i++)
            {
                float x = Mathf.Lerp(wl, wr, i / (float)FloorSamples);
                floorLr.SetPosition(i, P(x, SimField.FloorAt(x, sag, fy)));
            }

            // Walls down into the floor curve and out again — the holding box, one stroke.
            containerLr.positionCount = FloorSamples + 3;
            containerLr.SetPosition(0, P(wl, 0f));
            for (int i = 0; i <= FloorSamples; i++)
            {
                float x = Mathf.Lerp(wl, wr, i / (float)FloorSamples);
                containerLr.SetPosition(i + 1, P(x, SimField.FloorAt(x, sag, fy)));
            }
            containerLr.SetPosition(FloorSamples + 2, P(wr, 0f));

            spawnLr.positionCount = 2;
            spawnLr.SetPosition(0, P(wl, SimField.DropY));
            spawnLr.SetPosition(1, P(wr, SimField.DropY));

            topOutLr.positionCount = 2;
            topOutLr.SetPosition(0, P(wl, TopOutY));
            topOutLr.SetPosition(1, P(wr, TopOutY));

            lastSag = sag;
            lastFloorY = fy;
            lastTopOut = TopOutY;
            lastWL = wl;
            lastWR = wr;
        }

        void ApplyToggles()
        {
            if (floorLr == null) return;
            floorLr.gameObject.SetActive(floorCurve);
            containerLr.gameObject.SetActive(container);
            frameLr.gameObject.SetActive(canvasFrame);
            spawnLr.gameObject.SetActive(spawnLine);
            topOutLr.gameObject.SetActive(topOutLine);
        }

        // Cheap enough to run every frame; gives live inspector feedback for the toggles
        // and follows FloorSag / TopOutLine tuning changes as they happen.
        void LateUpdate()
        {
            if (!IsBuilt || floorLr == null) return;
            if (!Mathf.Approximately(lastSag, FloorSag) ||
                !Mathf.Approximately(lastFloorY, FloorY) ||
                !Mathf.Approximately(lastTopOut, TopOutY) ||
                !Mathf.Approximately(lastWL, WallL) ||
                !Mathf.Approximately(lastWR, WallR)) Resample();
            ApplyToggles();
        }

        LineRenderer MakeLine(string name, Color c, int order, float widthPx)
        {
            // DontSave, like every generated child — the scene file never sees these.
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(Content, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.sharedMaterial = SpriteMaterials.Unlit;
            lr.widthMultiplier = widthPx * StageCoords.PX;
            lr.startColor = lr.endColor = c;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.sortingLayerName = "PlayArea";
            lr.sortingOrder = order;
            return lr;
        }

        /// <summary>Sim px (y-down) → local units under the play-area parent.</summary>
        static Vector3 P(float x, float y)
            => new Vector3(x * SimField.PX, -y * SimField.PX, 0f);
    }
}
