using System.Collections;
using UnityEngine;
using TitanAscent.Systems;
using TitanAscent.Player;
using TitanAscent.Grapple;
using TitanAscent.Audio;

namespace TitanAscent.Scene
{
    [DefaultExecutionOrder(-100)]
    public class SceneBootstrapper : MonoBehaviour
    {
        [Header("Optional Overrides (auto-found if null)")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private SaveManager saveManager;
        [SerializeField] private FallTracker fallTracker;
        [SerializeField] private NarrationSystem narration;
        [SerializeField] private JuiceController juice;
        [SerializeField] private PlayerController player;
        [SerializeField] private GrappleController grapple;
        [SerializeField] private EmergencyRecovery emergencyRecovery;

        private void Awake()
        {
            ResolveReferences();
            ValidateSystems();
            WireEvents();
        }

        private void Start()
        {
            StartCoroutine(DelayedClimbStart());
        }

        private void ResolveReferences()
        {
            if (gameManager == null) gameManager = FindOrCreate<GameManager>("GameManager");
            if (audioManager == null) audioManager = FindOrCreate<AudioManager>("AudioManager");
            if (saveManager == null) saveManager = FindOrCreate<SaveManager>("SaveManager");
            if (fallTracker == null) fallTracker = FindFirstObjectByType<FallTracker>();
            if (narration == null) narration = FindFirstObjectByType<NarrationSystem>();
            if (juice == null) juice = FindFirstObjectByType<JuiceController>();

            GameObject playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null)
            {
                if (player == null) player = playerGO.GetComponent<PlayerController>();
                if (emergencyRecovery == null) emergencyRecovery = playerGO.GetComponent<EmergencyRecovery>();
                if (grapple == null) grapple = playerGO.GetComponentInChildren<GrappleController>();
            }
        }

        private void ValidateSystems()
        {
            bool ok = true;
            if (gameManager == null) { UnityEngine.Debug.LogError("[Bootstrapper] Missing: GameManager"); ok = false; }
            if (saveManager == null) { UnityEngine.Debug.LogError("[Bootstrapper] Missing: SaveManager"); ok = false; }
            if (fallTracker == null) { UnityEngine.Debug.LogError("[Bootstrapper] Missing: FallTracker"); ok = false; }
            if (narration == null) { UnityEngine.Debug.LogWarning("[Bootstrapper] Missing: NarrationSystem (non-critical)"); }
            if (juice == null) { UnityEngine.Debug.LogWarning("[Bootstrapper] Missing: JuiceController (non-critical)"); }
            if (player == null) { UnityEngine.Debug.LogError("[Bootstrapper] Missing: PlayerController (tag 'Player' not found)"); ok = false; }
            if (ok) UnityEngine.Debug.Log("[Bootstrapper] All critical systems found.");
        }

        private void WireEvents()
        {
            // Remove first to prevent duplicate listeners if Awake is called more than once
            UnwireEvents();

            if (fallTracker == null) return;

            if (narration != null)
                fallTracker.OnFallCompleted.AddListener(narration.TriggerForFall);

            if (juice != null)
            {
                fallTracker.OnFallCompleted.AddListener(data => juice.TriggerHardLanding(data.distance));
                fallTracker.OnNewHeightRecord.AddListener(_ => juice.TriggerNewRecord());
            }

            if (emergencyRecovery != null)
                fallTracker.OnEmergencyWindowOpen.AddListener(emergencyRecovery.ActivateWindow);

            if (grapple != null && juice != null)
                grapple.OnGrappleAttached.AddListener(juice.TriggerGrappleImpact);

            if (gameManager != null && juice != null)
                gameManager.OnVictory.AddListener(juice.TriggerVictory);
        }

        private void UnwireEvents()
        {
            if (fallTracker == null) return;
            if (narration != null)
                fallTracker.OnFallCompleted.RemoveListener(narration.TriggerForFall);
            if (emergencyRecovery != null)
                fallTracker.OnEmergencyWindowOpen.RemoveListener(emergencyRecovery.ActivateWindow);
        }

        private void OnDestroy()
        {
            UnwireEvents();
        }

        private IEnumerator DelayedClimbStart()
        {
            yield return new WaitForSeconds(0.5f);
            gameManager?.StartClimb();
        }

        private T FindOrCreate<T>(string goName) where T : Component
        {
            T existing = FindFirstObjectByType<T>();
            if (existing != null) return existing;

            GameObject go = new GameObject(goName);
            DontDestroyOnLoad(go);
            return go.AddComponent<T>();
        }
    }
}
