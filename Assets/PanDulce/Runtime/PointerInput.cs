using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PanDulce.Runtime
{
    /// <summary>
    /// New Input System → sim coordinates. Pointer.current covers mouse AND touch, so there
    /// is deliberately no Touchscreen special-case (§9.2).
    ///
    /// Polling the device rather than raycasting a collider is what lets aim keep tracking
    /// past the play area's edge while held.
    /// </summary>
    public sealed class PointerInput : MonoBehaviour
    {
        [SerializeField] Camera cam;
        [SerializeField] Transform playRoot;

        /// <summary>Aim position in sim px, y-down. Clamped to the walls by the sim on drop.</summary>
        public float AimX { get; private set; } = Core.SimField.CX;
        public Vector2 SimPosition { get; private set; }
        public bool IsDown { get; private set; }

        /// <summary>Fired on pointer-up with the sim-space release position.</summary>
        public event Action<Vector2> Released;

        bool startedOverUI;

        public void Init(Camera camera, Transform root)
        {
            cam = camera;
            playRoot = root;
        }

        void Update()
        {
            var ptr = Pointer.current;
            if (ptr == null || cam == null || playRoot == null) return;

            Vector2 screen = ptr.position.ReadValue();
            SimPosition = ScreenToSim(screen);

            if (ptr.press.wasPressedThisFrame)
            {
                // Reject taps that begin on the top bar or boost bar, so a mistimed press
                // near the bottom edge does not also drop a pastry (§9.2).
                startedOverUI = IsOverUI(screen);
                IsDown = !startedOverUI;
                if (IsDown) AimX = SimPosition.x;
            }
            else if (ptr.press.isPressed && IsDown)
            {
                AimX = SimPosition.x;
            }
            else if (ptr.press.wasReleasedThisFrame)
            {
                if (IsDown && !startedOverUI) Released?.Invoke(SimPosition);
                IsDown = false;
                startedOverUI = false;
            }
        }

        Vector2 ScreenToSim(Vector2 screen)
        {
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            Vector3 local = playRoot.InverseTransformPoint(world);
            return new Vector2(local.x / StageCoords.PX, -local.y / StageCoords.PX);
        }

        static bool IsOverUI(Vector2 screen)
        {
            if (EventSystem.current == null) return false;
            var data = new PointerEventData(EventSystem.current) { position = screen };
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(data, results);
            return results.Count > 0;
        }
    }
}
