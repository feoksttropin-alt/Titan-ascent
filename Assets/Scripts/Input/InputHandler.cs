using UnityEngine;
using UnityEngine.InputSystem;

namespace TitanAscent.Input
{
    /// <summary>
    /// Centralised input abstraction for Titan Ascent. Wraps all player input behind a clean API
    /// so the rest of the game never touches InputSystem directly.
    /// Singleton — persists across scenes.
    /// </summary>
    public class InputHandler : MonoBehaviour
    {
        public static InputHandler Instance { get; private set; }

        // -----------------------------------------------------------------------
        // Public Properties — updated each frame in Update()
        // -----------------------------------------------------------------------

        /// <summary>Normalised mouse / right-stick direction from screen center.</summary>
        public Vector2 AimDirection { get; private set; }

        /// <summary>True on the frame the grapple button is first pressed (Mouse0 / RightTrigger).</summary>
        public bool GrappleFire { get; private set; }

        /// <summary>True while the grapple button is held.</summary>
        public bool GrappleHeld { get; private set; }

        /// <summary>True on the frame the grapple button is released.</summary>
        public bool GrappleRelease { get; private set; }

        /// <summary>Thruster Up input (W / left-stick up) — while airborne.</summary>
        public bool ThrusterUp { get; private set; }

        /// <summary>Thruster Down input (S / left-stick down) — while airborne.</summary>
        public bool ThrusterDown { get; private set; }

        /// <summary>Thruster Left input (A / left-stick left) — while airborne.</summary>
        public bool ThrusterLeft { get; private set; }

        /// <summary>Thruster Right input (D / left-stick right) — while airborne.</summary>
        public bool ThrusterRight { get; private set; }

        /// <summary>Combined directional thruster vector, normalised. Derived from the four thruster bools.</summary>
        public Vector2 ThrusterInput { get; private set; }

        /// <summary>Held input for retracting the rope (Left Shift / LeftBumper).</summary>
        public bool RetractRope { get; private set; }

        /// <summary>Held input for extending the rope (Left Ctrl / LeftTrigger partial press).</summary>
        public bool ExtendRope { get; private set; }

        /// <summary>Fires once per press — Escape / Start button.</summary>
        public bool Pause { get; private set; }

        /// <summary>
        /// True on the frame the secondary grapple button is first pressed
        /// (Mouse2 / LeftShoulder on gamepad).
        /// </summary>
        public bool SecondaryGrappleFire { get; private set; }

        /// <summary>True on the frame the grip button is first pressed (Mouse1 / RightBumper).</summary>
        public bool GripDown { get; private set; }

        /// <summary>True while the grip button is held.</summary>
        public bool GripHeld { get; private set; }

        /// <summary>True on the frame the grip button is released.</summary>
        public bool GripReleased { get; private set; }

        /// <summary>
        /// Raw mouse delta in screen pixels per frame, suitable for camera look.
        /// On gamepad this stays zero — use AimDirection for analogue look instead.
        /// </summary>
        public Vector2 MouseDelta { get; private set; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Backtick key — editor / dev builds only.</summary>
        public bool DebugToggle { get; private set; }

        /// <summary>R key — editor / dev builds only.</summary>
        public bool ResetPlayer { get; private set; }
#endif

        // -----------------------------------------------------------------------
        // Private state
        // -----------------------------------------------------------------------

        private bool _pausePressedLastFrame;
        private bool _grappleWasPressedLastFrame;

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            ReadKeyboardMouse();
            // Gamepad reading is wired below but left with comment placeholders
            // so it can be completed once a controller layout asset is configured.
            ReadGamepad();
            ComputeDerivedValues();
        }

        // -----------------------------------------------------------------------
        // Input reading — Keyboard & Mouse
        // -----------------------------------------------------------------------

        private void ReadKeyboardMouse()
        {
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (kb == null && mouse == null) return;

            // --- Aim direction (mouse, from screen center) ---
            if (mouse != null)
            {
                Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                Vector2 mousePos = mouse.position.ReadValue();
                Vector2 rawDelta = mousePos - screenCenter;
                AimDirection = rawDelta.sqrMagnitude > 0.001f ? rawDelta.normalized : Vector2.up;

                // Raw frame delta for camera look
                MouseDelta = mouse.delta.ReadValue();
            }

            if (kb == null) return;

            // --- Grapple (Mouse0) ---
            bool grapplePressed = false;
            bool grappleHeld = false;
            if (mouse != null)
            {
                grapplePressed = mouse.leftButton.wasPressedThisFrame;
                grappleHeld    = mouse.leftButton.isPressed;
                GrappleRelease = mouse.leftButton.wasReleasedThisFrame;
            }

            GrappleFire = grapplePressed;
            GrappleHeld = grappleHeld;

            // --- Secondary grapple (Mouse2 / middle button) ---
            SecondaryGrappleFire = mouse != null && mouse.middleButton.wasPressedThisFrame;

            // --- Grip (Mouse1) ---
            if (mouse != null)
            {
                GripDown     = mouse.rightButton.wasPressedThisFrame;
                GripHeld     = mouse.rightButton.isPressed;
                GripReleased = mouse.rightButton.wasReleasedThisFrame;
            }

            // --- Thrusters (WASD) ---
            ThrusterUp    = kb.wKey.isPressed;
            ThrusterDown  = kb.sKey.isPressed;
            ThrusterLeft  = kb.aKey.isPressed;
            ThrusterRight = kb.dKey.isPressed;

            // --- Rope length (Shift = retract, Ctrl = extend) ---
            RetractRope = kb.leftShiftKey.isPressed;
            ExtendRope  = kb.leftCtrlKey.isPressed;

            // --- Pause (Escape) — fires once per press ---
            bool escapeCurrentlyDown = kb.escapeKey.isPressed;
            Pause = escapeCurrentlyDown && !_pausePressedLastFrame;
            _pausePressedLastFrame = escapeCurrentlyDown;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // --- Debug / dev-only bindings ---
            DebugToggle = kb.backquoteKey.wasPressedThisFrame;
            ResetPlayer = kb.rKey.wasPressedThisFrame;
#endif
        }

        // -----------------------------------------------------------------------
        // Input reading — Gamepad
        // -----------------------------------------------------------------------

        private void ReadGamepad()
        {
            // Gamepad.current is null when no controller is connected — keyboard/mouse values are preserved.
            Gamepad gamepad = Gamepad.current;
            if (gamepad == null) return;

            // --- Aim direction (right stick) ---
            Vector2 rightStick = gamepad.rightStick.ReadValue();
            if (rightStick.sqrMagnitude > 0.01f)
                AimDirection = rightStick.normalized;

            // --- Grapple (RightTrigger) — override keyboard/mouse values when gamepad is active ---
            GrappleFire    = gamepad.rightTrigger.wasPressedThisFrame;
            GrappleHeld    = gamepad.rightTrigger.isPressed;
            GrappleRelease = gamepad.rightTrigger.wasReleasedThisFrame;

            // --- Thrusters (left stick) ---
            Vector2 leftStick = gamepad.leftStick.ReadValue();
            ThrusterUp    = leftStick.y >  0.2f;
            ThrusterDown  = leftStick.y < -0.2f;
            ThrusterLeft  = leftStick.x < -0.2f;
            ThrusterRight = leftStick.x >  0.2f;

            // --- Secondary grapple (LeftShoulder on gamepad) ---
            SecondaryGrappleFire = SecondaryGrappleFire || gamepad.leftShoulder.wasPressedThisFrame;

            // --- Grip (RightShoulder) ---
            GripDown     = GripDown     || gamepad.rightShoulder.wasPressedThisFrame;
            GripHeld     = GripHeld     || gamepad.rightShoulder.isPressed;
            GripReleased = GripReleased || gamepad.rightShoulder.wasReleasedThisFrame;

            // --- Rope length ---
            RetractRope = gamepad.leftShoulder.isPressed;
            ExtendRope  = gamepad.leftTrigger.IsActuated(0.15f);

            // --- Pause (Start / Menu button) ---
            bool startCurrently = gamepad.startButton.isPressed;
            if (startCurrently && !_pausePressedLastFrame) Pause = true;
            _pausePressedLastFrame = startCurrently || _pausePressedLastFrame;
        }

        // -----------------------------------------------------------------------
        // Derived values
        // -----------------------------------------------------------------------

        private void ComputeDerivedValues()
        {
            // Build ThrusterInput from the four bool directions
            float horizontal = 0f;
            float vertical   = 0f;

            if (ThrusterRight) horizontal += 1f;
            if (ThrusterLeft)  horizontal -= 1f;
            if (ThrusterUp)    vertical   += 1f;
            if (ThrusterDown)  vertical   -= 1f;

            Vector2 raw = new Vector2(horizontal, vertical);
            ThrusterInput = raw.sqrMagnitude > 1f ? raw.normalized : raw;
        }
    }
}
