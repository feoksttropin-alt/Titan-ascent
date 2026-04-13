using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TitanAscent.Systems
{
    /// <summary>
    /// First-time tutorial overlay. 7 contextual steps shown only on the
    /// player's very first climb (totalClimbs == 0 at session start).
    /// Steps persist in PlayerPrefs so a crash mid-tutorial resumes correctly.
    /// </summary>
    public class TutorialSystem : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Step definitions
        // ------------------------------------------------------------------

        private enum TutorialStepId
        {
            FireGrapple       = 0,
            AimForAnchors     = 1,
            ReelIn            = 2,
            ReleaseAtApex     = 3,
            WasdMidAir        = 4,
            GripSlide         = 5,
            EmergencyReGrapple= 6
        }

        private const int TotalSteps = 7;
        private const string PrefKey = "TutorialStep";

        // ------------------------------------------------------------------
        // Inspector
        // ------------------------------------------------------------------

        [Header("UI Elements")]
        [SerializeField] private CanvasGroup    tutorialPanel;
        [SerializeField] private Text           stepTitle;
        [SerializeField] private Text           stepBody;
        [SerializeField] private RectTransform  directionalArrow;
        [SerializeField] private GameObject     highlightOverlay;

        [Header("Dismiss")]
        [SerializeField] private KeyCode        dismissKey = KeyCode.Return;

        // ------------------------------------------------------------------
        // Private state
        // ------------------------------------------------------------------

        private int        currentStep;
        private bool       stepActive;
        private bool       tutorialComplete;

        private SaveManager saveManager;

        // Step-specific dismiss conditions
        private bool grappleFired;
        private bool grappleAttached;
        private float reelInDistance;
        private Vector3 reelInStartPos;
        private int swingCount;
        private float startHeight;
        private bool thrusterUsed;
        private bool muscleSkinTouched;
        private bool slidingActive;

        // Airborne tracking
        private float airborneStartTime;
        private bool isAirborne;

        // Fall tracking for step 7
        private float fallStartHeight;
        private bool longFallActive;

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            saveManager = FindFirstObjectByType<SaveManager>();
        }

        private void Start()
        {
            // Skip tutorial if player has climbed before
            if (saveManager != null && saveManager.CurrentData.totalClimbs > 0)
            {
                gameObject.SetActive(false);
                return;
            }

            currentStep = PlayerPrefs.GetInt(PrefKey, 0);

            if (currentStep >= TotalSteps)
            {
                tutorialComplete = true;
                SetPanelVisible(false);
                return;
            }

            SetPanelVisible(false);
            StartStep(currentStep);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GrappleAttachedEvent>(OnGrappleAttached);
            EventBus.Subscribe<GrappleReleasedEvent>(OnGrappleReleased);
            EventBus.Subscribe<FallStartedEvent>(OnFallStarted);
            EventBus.Subscribe<FallEndedEvent>(OnFallEnded);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GrappleAttachedEvent>(OnGrappleAttached);
            EventBus.Unsubscribe<GrappleReleasedEvent>(OnGrappleReleased);
            EventBus.Unsubscribe<FallStartedEvent>(OnFallStarted);
            EventBus.Unsubscribe<FallEndedEvent>(OnFallEnded);
        }

        private void Update()
        {
            if (tutorialComplete || !stepActive) return;

            CheckDismissCondition();
        }

        // ------------------------------------------------------------------
        // Step management
        // ------------------------------------------------------------------

        private void StartStep(int index)
        {
            if (index >= TotalSteps)
            {
                CompleteTutorial();
                return;
            }

            currentStep = index;
            stepActive  = false;

            TutorialStepId id = (TutorialStepId)index;

            switch (id)
            {
                case TutorialStepId.FireGrapple:
                    // Show immediately on game start
                    ShowStep("FIRE YOUR GRAPPLE",
                             "Left-click to fire your grapple hook.\nAim at glowing anchor points on the titan's surface.",
                             Vector2.up);
                    break;

                case TutorialStepId.AimForAnchors:
                    // Shown after first grapple miss (event-driven)
                    break;

                case TutorialStepId.ReelIn:
                    // Shown after first grapple attach (event-driven)
                    break;

                case TutorialStepId.ReleaseAtApex:
                    // Shown after 3 swings (tracked in Update)
                    break;

                case TutorialStepId.WasdMidAir:
                    // Shown after first 3-second airborne (tracked in Update)
                    break;

                case TutorialStepId.GripSlide:
                    // Shown on first MuscleSkin contact (event-driven via CallFromOutside)
                    break;

                case TutorialStepId.EmergencyReGrapple:
                    // Shown on first fall >50m (FallStartedEvent)
                    break;
            }
        }

        private void ShowStep(string title, string body, Vector2 arrowDir)
        {
            stepActive = true;

            if (stepTitle != null) stepTitle.text = title;
            if (stepBody  != null) stepBody.text  = body;

            if (directionalArrow != null)
            {
                float angle = Mathf.Atan2(arrowDir.y, arrowDir.x) * Mathf.Rad2Deg;
                directionalArrow.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            SetPanelVisible(true);
        }

        private bool IsDismissKeyDown()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return false;
            // Map the most common dismiss keys; fall back gracefully for others
            switch (dismissKey)
            {
                case KeyCode.Return:      return kb.enterKey.wasPressedThisFrame;
                case KeyCode.KeypadEnter: return kb.numpadEnterKey.wasPressedThisFrame;
                case KeyCode.Space:       return kb.spaceKey.wasPressedThisFrame;
                case KeyCode.Escape:      return kb.escapeKey.wasPressedThisFrame;
                case KeyCode.Tab:         return kb.tabKey.wasPressedThisFrame;
                default:                  return false;
            }
        }

        private void DismissCurrentStep()
        {
            if (!stepActive) return;

            stepActive = false;
            SetPanelVisible(false);

            int next = currentStep + 1;
            PlayerPrefs.SetInt(PrefKey, next);
            PlayerPrefs.Save();

            if (next < TotalSteps)
                StartStep(next);
            else
                CompleteTutorial();
        }

        private void CompleteTutorial()
        {
            tutorialComplete = true;
            PlayerPrefs.SetInt(PrefKey, TotalSteps);
            PlayerPrefs.Save();
            SetPanelVisible(false);
            Debug.Log("[TutorialSystem] Tutorial complete.");
        }

        // ------------------------------------------------------------------
        // Dismiss condition checks (called every Update)
        // ------------------------------------------------------------------

        private void CheckDismissCondition()
        {
            Player.PlayerController player = FindFirstObjectByType<Player.PlayerController>();
            float height = player != null ? player.transform.position.y : 0f;

            switch ((TutorialStepId)currentStep)
            {
                // Step 0: dismiss when grapple is fired
                case TutorialStepId.FireGrapple:
                    if (grappleFired) DismissCurrentStep();
                    break;

                // Step 1: dismiss when grapple attaches (after a miss)
                case TutorialStepId.AimForAnchors:
                    if (grappleAttached) DismissCurrentStep();
                    break;

                // Step 2: dismiss when player has reeled in 5m
                case TutorialStepId.ReelIn:
                    if (player != null)
                    {
                        float retracted = Vector3.Distance(player.transform.position, reelInStartPos);
                        if (retracted >= 5f) DismissCurrentStep();
                    }
                    break;

                // Step 3: dismiss when player reaches 30m height
                case TutorialStepId.ReleaseAtApex:
                    if (height >= 30f) DismissCurrentStep();
                    break;

                // Step 4: dismiss when thruster is used
                case TutorialStepId.WasdMidAir:
                    if (thrusterUsed) DismissCurrentStep();
                    break;

                // Step 5: dismiss when player begins sliding
                case TutorialStepId.GripSlide:
                    if (slidingActive) DismissCurrentStep();
                    break;

                // Step 6: dismiss when fall ends or player recovers
                case TutorialStepId.EmergencyReGrapple:
                    if (!longFallActive && grappleAttached) DismissCurrentStep();
                    break;
            }

            // Allow keyboard dismiss at any time
            if (IsDismissKeyDown())
                DismissCurrentStep();
        }

        // ------------------------------------------------------------------
        // External call-ins from other systems
        // ------------------------------------------------------------------

        /// <summary>Called by GrappleController when a shot is fired.</summary>
        public void NotifyGrappleFired()
        {
            grappleFired = true;
        }

        /// <summary>Called by GrappleController when a shot misses.</summary>
        public void NotifyGrappleMiss()
        {
            if (currentStep == (int)TutorialStepId.AimForAnchors && !stepActive)
            {
                ShowStep("AIM FOR GLOWING ANCHORS",
                         "Anchor points glow to show they are grappleable.\nAim at them for a solid connection.",
                         Vector2.up);
            }
        }

        /// <summary>Called by GrappleController after rope has been reeled 5m.</summary>
        public void NotifySwing()
        {
            swingCount++;
            if (swingCount >= 3 &&
                currentStep == (int)TutorialStepId.ReleaseAtApex && !stepActive)
            {
                ShowStep("RELEASE AT THE SWING APEX",
                         "Let go at the top of your swing arc to launch yourself upward.\nLeft-click again to release.",
                         Vector2.up);
            }
        }

        /// <summary>Called by ThrusterSystem when thrusters fire.</summary>
        public void NotifyThrusterUsed() => thrusterUsed = true;

        /// <summary>Called when player contacts a MuscleSkin surface.</summary>
        public void NotifyMuscleSkinContact()
        {
            muscleSkinTouched = true;
            if (currentStep == (int)TutorialStepId.GripSlide && !stepActive)
            {
                ShowStep("GRIP SLOWS YOUR SLIDE",
                         "Hold Right-click to engage grip claws.\nThis slows your slide on slick surfaces like muscle-skin.",
                         Vector2.down);
            }
        }

        /// <summary>Called when the player starts sliding on a surface.</summary>
        public void NotifySliding() => slidingActive = true;

        // ------------------------------------------------------------------
        // EventBus handlers
        // ------------------------------------------------------------------

        private void OnGrappleAttached(GrappleAttachedEvent evt)
        {
            grappleAttached = true;

            if (currentStep == (int)TutorialStepId.ReelIn && !stepActive)
            {
                Player.PlayerController player = FindFirstObjectByType<Player.PlayerController>();
                reelInStartPos = player != null ? player.transform.position : Vector3.zero;

                ShowStep("HOLD SHIFT TO REEL IN",
                         "Hold SHIFT to pull yourself toward the anchor.\nReel in at least 5 metres to continue.",
                         Vector2.up);
            }
        }

        private void OnGrappleReleased(GrappleReleasedEvent evt)
        {
            grappleAttached = false;
        }

        private void OnFallStarted(FallStartedEvent evt)
        {
            fallStartHeight  = evt.height;
            longFallActive   = false;

            // Step 4: detect 3s airborne
            isAirborne       = true;
            airborneStartTime = Time.time;

            // Step 6: only trigger on fall > 50m
            StartCoroutine(WatchForLongFall());
        }

        private void OnFallEnded(FallEndedEvent evt)
        {
            isAirborne      = false;
            longFallActive  = false;

            if (currentStep == (int)TutorialStepId.EmergencyReGrapple && stepActive)
                DismissCurrentStep();
        }

        private IEnumerator WatchForLongFall()
        {
            // Step 4: WASD / thruster hint after 3s airborne
            yield return new WaitForSeconds(3f);

            if (isAirborne && currentStep == (int)TutorialStepId.WasdMidAir && !stepActive)
            {
                ShowStep("WASD ADJUSTS YOUR ARC",
                         "Use WASD to steer mid-air and align for your next grapple shot.\nActivate thrusters with SPACE for a quick boost.",
                         Vector2.right);
            }

            // Keep watching to see if this turns into a 50m fall
            while (isAirborne)
            {
                Player.PlayerController player = FindFirstObjectByType<Player.PlayerController>();
                if (player != null)
                {
                    float fallen = fallStartHeight - player.transform.position.y;
                    if (fallen >= 50f && !longFallActive)
                    {
                        longFallActive = true;

                        if (currentStep == (int)TutorialStepId.EmergencyReGrapple && !stepActive)
                        {
                            ShowStep("EMERGENCY WINDOW",
                                     "During a big fall there's a brief window to fire your grapple and recover.\nDon't panic — find an anchor and shoot!",
                                     Vector2.up);
                        }
                    }
                }

                yield return null;
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private void SetPanelVisible(bool visible)
        {
            if (tutorialPanel == null) return;
            tutorialPanel.alpha          = visible ? 1f : 0f;
            tutorialPanel.interactable   = visible;
            tutorialPanel.blocksRaycasts = visible;
        }
    }
}
