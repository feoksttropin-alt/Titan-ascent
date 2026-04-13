using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

// NOTE: InputHandler.LoadBindings() should be called during InputHandler.Awake() after
// the singleton is established.  Suggested implementation:
//
//   private void LoadBindings()
//   {
//       string json = PlayerPrefs.GetString("InputBindings", "");
//       if (string.IsNullOrEmpty(json)) return;
//       try
//       {
//           InputBindingMap loaded = JsonUtility.FromJson<InputBindingMap>(json);
//           if (loaded != null) bindings = loaded;
//       }
//       catch { /* corrupted prefs — use defaults */ }
//   }
//
// InputHandler should expose a public InputBindingMap field or property and call
// LoadBindings() before the first Update().

namespace TitanAscent.UI
{
    // ──────────────────────────────────────────────────────────────────────────────
    // Data model — serialised to / from PlayerPrefs as JSON
    // ──────────────────────────────────────────────────────────────────────────────

    [Serializable]
    public class InputBindingMap
    {
        public KeyCode GrappleFire     = KeyCode.Mouse0;
        public KeyCode GrappleRelease  = KeyCode.Mouse0; // release event — same key, detected on up
        public KeyCode RetractRope     = KeyCode.LeftShift;
        public KeyCode ExtendRope      = KeyCode.LeftControl;
        public KeyCode ThrustUp        = KeyCode.W;
        public KeyCode ThrustDown      = KeyCode.S;
        public KeyCode ThrustLeft      = KeyCode.A;
        public KeyCode ThrustRight     = KeyCode.D;
        public KeyCode Pause           = KeyCode.Escape;
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Controller
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// In-game control remapping screen.
    ///
    /// Setup requirements:
    ///   - Attach to a canvas GameObject that acts as the root panel.
    ///   - Assign <see cref="rowContainer"/> (a VerticalLayoutGroup transform),
    ///     <see cref="rowPrefab"/>, <see cref="keyboardTab"/>, <see cref="gamepadTab"/>,
    ///     <see cref="gamepadPlaceholderText"/>, <see cref="resetButton"/>,
    ///     <see cref="listenOverlay"/>, and <see cref="listenLabel"/> in the Inspector.
    ///   - Call <see cref="Show"/> / <see cref="Hide"/> from PauseMenu or SettingsManager.
    /// </summary>
    public class ControlsRemapUI : MonoBehaviour
    {
        // ── Inspector references ───────────────────────────────────────────────────
        [Header("Layout")]
        [SerializeField] private Transform rowContainer;
        [SerializeField] private GameObject rowPrefab;         // Must have: action label (TMP), key button (Button+TMP), change button (Button+TMP)
        [SerializeField] private Button keyboardTabButton;
        [SerializeField] private Button gamepadTabButton;

        [Header("Panels")]
        [SerializeField] private GameObject keyboardPanel;
        [SerializeField] private GameObject gamepadPanel;
        [SerializeField] private TMP_Text   gamepadPlaceholderText;

        [Header("Bottom Bar")]
        [SerializeField] private Button resetButton;
        [SerializeField] private Button closeButton;

        [Header("Listen Overlay")]
        [SerializeField] private GameObject listenOverlay;    // Full-screen dark panel shown during capture
        [SerializeField] private TMP_Text   listenLabel;      // "Press any key..."

        // ── Runtime state ──────────────────────────────────────────────────────────
        private InputBindingMap liveBindings = new InputBindingMap();
        private bool isListening  = false;
        private string listeningAction = "";
        private IDisposable _listenDisposable;

        // Maps action name → the TMP label on that row's key button
        private readonly Dictionary<string, TMP_Text> keyLabelMap = new Dictionary<string, TMP_Text>();

        // Ordered list of all remappable actions
        private static readonly List<(string actionName, string displayName)> Actions = new List<(string, string)>
        {
            ("GrappleFire",    "Grapple Fire"),
            ("GrappleRelease", "Grapple Release"),
            ("RetractRope",    "Retract Rope"),
            ("ExtendRope",     "Extend Rope"),
            ("ThrustUp",       "Thrust Up"),
            ("ThrustDown",     "Thrust Down"),
            ("ThrustLeft",     "Thrust Left"),
            ("ThrustRight",    "Thrust Right"),
            ("Pause",          "Pause"),
        };

        private const string BindingsPrefKey = "InputBindings";

        // ── Lifecycle ──────────────────────────────────────────────────────────────

        private void Awake()
        {
            LoadBindings();
            BuildRows();

            if (resetButton  != null) resetButton.onClick.AddListener(OnResetToDefaults);
            if (closeButton  != null) closeButton.onClick.AddListener(Hide);
            if (keyboardTabButton != null) keyboardTabButton.onClick.AddListener(ShowKeyboardTab);
            if (gamepadTabButton  != null) gamepadTabButton.onClick.AddListener(ShowGamepadTab);

            if (listenOverlay != null) listenOverlay.SetActive(false);

            if (gamepadPlaceholderText != null)
                gamepadPlaceholderText.text = "(Gamepad support coming)";

            // Start on keyboard tab
            ShowKeyboardTab();
        }

        private void Update()
        {
            if (!isListening) return;

            // Cancel on Escape — poll here for fast response independent of the subscription
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
                StopListening();
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        public void Show()
        {
            gameObject.SetActive(true);
            LoadBindings();
            RefreshAllKeyLabels();
        }

        public void Hide()
        {
            if (isListening) StopListening();
            SaveBindings();
            gameObject.SetActive(false);
        }

        // ── Tab switching ──────────────────────────────────────────────────────────

        private void ShowKeyboardTab()
        {
            if (keyboardPanel != null) keyboardPanel.SetActive(true);
            if (gamepadPanel  != null) gamepadPanel.SetActive(false);

            SetTabHighlight(keyboardTabButton, true);
            SetTabHighlight(gamepadTabButton,  false);
        }

        private void ShowGamepadTab()
        {
            if (keyboardPanel != null) keyboardPanel.SetActive(false);
            if (gamepadPanel  != null) gamepadPanel.SetActive(true);

            SetTabHighlight(keyboardTabButton, false);
            SetTabHighlight(gamepadTabButton,  true);
        }

        private static void SetTabHighlight(Button btn, bool active)
        {
            if (btn == null) return;
            ColorBlock cb = btn.colors;
            cb.normalColor = active ? new Color(0.25f, 0.55f, 1f) : new Color(0.18f, 0.18f, 0.18f);
            btn.colors = cb;
        }

        // ── Row building ───────────────────────────────────────────────────────────

        private void BuildRows()
        {
            if (rowContainer == null || rowPrefab == null) return;

            // Clear any existing rows (e.g. editor preview rows)
            foreach (Transform child in rowContainer)
                Destroy(child.gameObject);

            keyLabelMap.Clear();

            foreach (var (actionName, displayName) in Actions)
            {
                GameObject row = Instantiate(rowPrefab, rowContainer);

                // Locate child components by convention:
                //   Child index 0 — Action name label  (TMP_Text)
                //   Child index 1 — Current key button (Button containing TMP_Text)
                //   Child index 2 — "Change" button    (Button containing TMP_Text)

                TMP_Text[] texts   = row.GetComponentsInChildren<TMP_Text>(true);
                Button[]   buttons = row.GetComponentsInChildren<Button>(true);

                if (texts.Length >= 1)
                    texts[0].text = displayName;

                TMP_Text keyLabel = texts.Length >= 2 ? texts[1] : null;
                if (keyLabel != null)
                {
                    keyLabel.text = GetBoundKey(actionName).ToString();
                    keyLabelMap[actionName] = keyLabel;
                }

                // "Change" button — capture the action name in a local variable for the closure
                if (buttons.Length >= 2)
                {
                    string capturedAction = actionName;
                    buttons[1].onClick.AddListener(() => StartListening(capturedAction));

                    TMP_Text changeBtnLabel = buttons[1].GetComponentInChildren<TMP_Text>();
                    if (changeBtnLabel != null) changeBtnLabel.text = "Change";
                }
            }
        }

        // ── Listen mode ────────────────────────────────────────────────────────────

        private void StartListening(string actionName)
        {
            // Dispose any previous subscription before starting a new one
            _listenDisposable?.Dispose();
            _listenDisposable = null;

            isListening     = true;
            listeningAction = actionName;

            if (listenOverlay != null) listenOverlay.SetActive(true);
            if (listenLabel   != null) listenLabel.text = $"Rebinding: {GetDisplayName(actionName)}\n\nPress any key...\n\n[Escape] to cancel";

            // Subscribe to any button press via New Input System
            _listenDisposable = InputSystem.onAnyButtonPress.Call(OnAnyButtonPressed);
        }

        private void OnAnyButtonPressed(InputControl control)
        {
            if (!isListening) return;

            // Escape — cancel (also handled in Update for responsiveness)
            if (control is KeyControl esc && esc.keyCode == Key.Escape)
            {
                StopListening();
                return;
            }

            // Mouse buttons
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                if (control == mouse.leftButton)   { CommitBinding(listeningAction, KeyCode.Mouse0); return; }
                if (control == mouse.rightButton)  { CommitBinding(listeningAction, KeyCode.Mouse1); return; }
                if (control == mouse.middleButton) { CommitBinding(listeningAction, KeyCode.Mouse2); return; }
            }

            // Keyboard keys
            if (control is KeyControl keyCtrl)
            {
                KeyCode kc = NewKeyToKeyCode(keyCtrl.keyCode);
                if (kc != KeyCode.None)
                {
                    CommitBinding(listeningAction, kc);
                    return;
                }
            }

            // Unrecognised control — dismiss without committing
            StopListening();
        }

        private void StopListening()
        {
            isListening     = false;
            listeningAction = "";
            _listenDisposable?.Dispose();
            _listenDisposable = null;
            if (listenOverlay != null) listenOverlay.SetActive(false);
        }

        private void CommitBinding(string actionName, KeyCode kc)
        {
            SetBoundKey(actionName, kc);
            SaveBindings();
            RefreshKeyLabel(actionName);
            StopListening();
        }

        // ── Binding helpers ────────────────────────────────────────────────────────

        private KeyCode GetBoundKey(string actionName)
        {
            switch (actionName)
            {
                case "GrappleFire":    return liveBindings.GrappleFire;
                case "GrappleRelease": return liveBindings.GrappleRelease;
                case "RetractRope":    return liveBindings.RetractRope;
                case "ExtendRope":     return liveBindings.ExtendRope;
                case "ThrustUp":       return liveBindings.ThrustUp;
                case "ThrustDown":     return liveBindings.ThrustDown;
                case "ThrustLeft":     return liveBindings.ThrustLeft;
                case "ThrustRight":    return liveBindings.ThrustRight;
                case "Pause":          return liveBindings.Pause;
                default:               return KeyCode.None;
            }
        }

        private void SetBoundKey(string actionName, KeyCode kc)
        {
            switch (actionName)
            {
                case "GrappleFire":    liveBindings.GrappleFire    = kc; break;
                case "GrappleRelease": liveBindings.GrappleRelease = kc; break;
                case "RetractRope":    liveBindings.RetractRope    = kc; break;
                case "ExtendRope":     liveBindings.ExtendRope     = kc; break;
                case "ThrustUp":       liveBindings.ThrustUp       = kc; break;
                case "ThrustDown":     liveBindings.ThrustDown     = kc; break;
                case "ThrustLeft":     liveBindings.ThrustLeft     = kc; break;
                case "ThrustRight":    liveBindings.ThrustRight    = kc; break;
                case "Pause":          liveBindings.Pause          = kc; break;
            }
        }

        private void RefreshKeyLabel(string actionName)
        {
            if (!keyLabelMap.TryGetValue(actionName, out TMP_Text label)) return;
            label.text = GetBoundKey(actionName).ToString();
        }

        private void RefreshAllKeyLabels()
        {
            foreach (var (actionName, _) in Actions)
                RefreshKeyLabel(actionName);
        }

        private static string GetDisplayName(string actionName)
        {
            foreach (var (a, d) in Actions)
                if (a == actionName) return d;
            return actionName;
        }

        // ── New Input System Key → legacy KeyCode mapping ─────────────────────────

        private static KeyCode NewKeyToKeyCode(Key key)
        {
            switch (key)
            {
                // Letters
                case Key.A: return KeyCode.A; case Key.B: return KeyCode.B;
                case Key.C: return KeyCode.C; case Key.D: return KeyCode.D;
                case Key.E: return KeyCode.E; case Key.F: return KeyCode.F;
                case Key.G: return KeyCode.G; case Key.H: return KeyCode.H;
                case Key.I: return KeyCode.I; case Key.J: return KeyCode.J;
                case Key.K: return KeyCode.K; case Key.L: return KeyCode.L;
                case Key.M: return KeyCode.M; case Key.N: return KeyCode.N;
                case Key.O: return KeyCode.O; case Key.P: return KeyCode.P;
                case Key.Q: return KeyCode.Q; case Key.R: return KeyCode.R;
                case Key.S: return KeyCode.S; case Key.T: return KeyCode.T;
                case Key.U: return KeyCode.U; case Key.V: return KeyCode.V;
                case Key.W: return KeyCode.W; case Key.X: return KeyCode.X;
                case Key.Y: return KeyCode.Y; case Key.Z: return KeyCode.Z;
                // Digits
                case Key.Digit0: return KeyCode.Alpha0; case Key.Digit1: return KeyCode.Alpha1;
                case Key.Digit2: return KeyCode.Alpha2; case Key.Digit3: return KeyCode.Alpha3;
                case Key.Digit4: return KeyCode.Alpha4; case Key.Digit5: return KeyCode.Alpha5;
                case Key.Digit6: return KeyCode.Alpha6; case Key.Digit7: return KeyCode.Alpha7;
                case Key.Digit8: return KeyCode.Alpha8; case Key.Digit9: return KeyCode.Alpha9;
                // Function keys
                case Key.F1:  return KeyCode.F1;  case Key.F2:  return KeyCode.F2;
                case Key.F3:  return KeyCode.F3;  case Key.F4:  return KeyCode.F4;
                case Key.F5:  return KeyCode.F5;  case Key.F6:  return KeyCode.F6;
                case Key.F7:  return KeyCode.F7;  case Key.F8:  return KeyCode.F8;
                case Key.F9:  return KeyCode.F9;  case Key.F10: return KeyCode.F10;
                case Key.F11: return KeyCode.F11; case Key.F12: return KeyCode.F12;
                // Modifiers
                case Key.LeftShift:   return KeyCode.LeftShift;
                case Key.RightShift:  return KeyCode.RightShift;
                case Key.LeftCtrl:    return KeyCode.LeftControl;
                case Key.RightCtrl:   return KeyCode.RightControl;
                case Key.LeftAlt:     return KeyCode.LeftAlt;
                case Key.RightAlt:    return KeyCode.RightAlt;
                // Common keys
                case Key.Space:       return KeyCode.Space;
                case Key.Enter:       return KeyCode.Return;
                case Key.NumpadEnter: return KeyCode.KeypadEnter;
                case Key.Backspace:   return KeyCode.Backspace;
                case Key.Delete:      return KeyCode.Delete;
                case Key.Tab:         return KeyCode.Tab;
                case Key.Escape:      return KeyCode.Escape;
                case Key.UpArrow:     return KeyCode.UpArrow;
                case Key.DownArrow:   return KeyCode.DownArrow;
                case Key.LeftArrow:   return KeyCode.LeftArrow;
                case Key.RightArrow:  return KeyCode.RightArrow;
                case Key.Home:        return KeyCode.Home;
                case Key.End:         return KeyCode.End;
                case Key.PageUp:      return KeyCode.PageUp;
                case Key.PageDown:    return KeyCode.PageDown;
                case Key.Insert:      return KeyCode.Insert;
                // Numpad
                case Key.Numpad0: return KeyCode.Keypad0; case Key.Numpad1: return KeyCode.Keypad1;
                case Key.Numpad2: return KeyCode.Keypad2; case Key.Numpad3: return KeyCode.Keypad3;
                case Key.Numpad4: return KeyCode.Keypad4; case Key.Numpad5: return KeyCode.Keypad5;
                case Key.Numpad6: return KeyCode.Keypad6; case Key.Numpad7: return KeyCode.Keypad7;
                case Key.Numpad8: return KeyCode.Keypad8; case Key.Numpad9: return KeyCode.Keypad9;
                case Key.NumpadPlus:     return KeyCode.KeypadPlus;
                case Key.NumpadMinus:    return KeyCode.KeypadMinus;
                case Key.NumpadMultiply: return KeyCode.KeypadMultiply;
                case Key.NumpadDivide:   return KeyCode.KeypadDivide;
                case Key.NumpadPeriod:   return KeyCode.KeypadPeriod;
                default: return KeyCode.None;
            }
        }

        // ── Reset to defaults ──────────────────────────────────────────────────────

        private void OnResetToDefaults()
        {
            liveBindings = new InputBindingMap(); // resets all fields to their field initialisers
            SaveBindings();
            RefreshAllKeyLabels();
        }

        // ── Persistence ────────────────────────────────────────────────────────────

        private void LoadBindings()
        {
            string json = PlayerPrefs.GetString(BindingsPrefKey, "");
            if (string.IsNullOrEmpty(json))
            {
                liveBindings = new InputBindingMap();
                return;
            }

            try
            {
                InputBindingMap loaded = JsonUtility.FromJson<InputBindingMap>(json);
                liveBindings = loaded ?? new InputBindingMap();
            }
            catch
            {
                liveBindings = new InputBindingMap();
            }
        }

        private void SaveBindings()
        {
            string json = JsonUtility.ToJson(liveBindings);
            PlayerPrefs.SetString(BindingsPrefKey, json);
            PlayerPrefs.Save();
        }
    }
}
