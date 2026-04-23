#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TitanAscent.Systems;
using TitanAscent.Player;
using TitanAscent.Audio;
using TitanAscent.UI;
using TitanAscent.Input;
using TitanAscent.Physics;

namespace TitanAscent.Scene
{
    /// <summary>
    /// Editor utility to build the full scene hierarchy programmatically.
    /// Access via TitanAscent > Setup Main Scene in the Unity menu.
    /// </summary>
    public static class MainSceneSetup
    {
        [MenuItem("TitanAscent/Setup Main Scene")]
        public static void SetupMainScene()
        {
            // ── [MANAGERS] ────────────────────────────────────────────────────
            GameObject managers = new GameObject("[MANAGERS]");

            CreateChild<GameManager>(managers,           "GameManager");
            CreateChild<AudioManager>(managers,          "AudioManager");
            CreateChild<SaveManager>(managers,           "SaveManager");
            CreateChild<InputHandler>(managers,          "InputHandler");
            CreateChild<SceneBootstrapper>(managers,     "SceneBootstrapper");
            CreateChild<BuildVersionManager>(managers,   "BuildVersionManager");
            CreateChild<CrashSafetyHandler>(managers,   "CrashSafetyHandler");
            CreateChild<SessionManager>(managers,        "SessionManager");
            CreateChild<TimeScaleManager>(managers,      "TimeScaleManager");

            // ── [PLAYER] ─────────────────────────────────────────────────────
            GameObject player = new GameObject("[PLAYER]");
            player.tag = "Player";

            Rigidbody rb = player.AddComponent<Rigidbody>();
            rb.mass             = 75f;
            rb.linearDamping    = 0.08f;
            rb.angularDamping   = 0.5f;

            player.AddComponent<CapsuleCollider>();
            player.AddComponent<PlayerController>();
            player.AddComponent<ThrusterSystem>();
            player.AddComponent<GripSystem>();
            player.AddComponent<EmergencyRecovery>();
            player.AddComponent<CoyoteTimeSystem>();
            player.AddComponent<MomentumConservationSystem>();
            player.AddComponent<Physics.CollisionSafety>();
            player.AddComponent<FallTracker>();
            player.AddComponent<SessionStatsTracker>();

            // ── [CAMERA] ─────────────────────────────────────────────────────
            GameObject cam = new GameObject("[CAMERA]");
            cam.AddComponent<CameraController>();
            cam.AddComponent<AudioListener>();

            // ── [ENVIRONMENT] ─────────────────────────────────────────────────
            GameObject environment = new GameObject("[ENVIRONMENT]");
            new GameObject("ZoneManager").transform.SetParent(environment.transform);
            new GameObject("WindSystem").transform.SetParent(environment.transform);
            new GameObject("AtmosphereController").transform.SetParent(environment.transform);
            new GameObject("TitanMovement").transform.SetParent(environment.transform);
            new GameObject("WorldEventScheduler").transform.SetParent(environment.transform);

            // ── [HUD_CANVAS] ──────────────────────────────────────────────────
            GameObject hudGO = new GameObject("[HUD_CANVAS]");
            Canvas hudCanvas = hudGO.AddComponent<Canvas>();
            hudCanvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            hudCanvas.sortingOrder = 10;

            CanvasScaler scaler = hudGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            hudGO.AddComponent<GraphicRaycaster>();

            // ── [TITAN_GEOMETRY] ──────────────────────────────────────────────
            new GameObject("[TITAN_GEOMETRY]");

            Debug.Log("[MainSceneSetup] Scene hierarchy created. Assign serialized references in Inspector.");
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static T CreateChild<T>(GameObject parent, string name) where T : Component
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            return go.AddComponent<T>();
        }
    }
}
#endif
