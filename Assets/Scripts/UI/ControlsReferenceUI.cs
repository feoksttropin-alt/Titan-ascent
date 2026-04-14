using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;

namespace TitanAscent.UI
{
    /// <summary>
    /// In-game controls reference overlay.
    /// Toggle with the Tab key (keyboard) or the Select / Back button (gamepad).
    /// Displays two columns: Keyboard + Mouse bindings and Controller bindings.
    /// </summary>
    public class ControlsReferenceUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject panel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float fadeSpeed = 10f;

        [Header("Text Columns")]
        [SerializeField] private TextMeshProUGUI keyboardColumnText;
        [SerializeField] private TextMeshProUGUI controllerColumnText;

        [Header("Column Headers")]
        [SerializeField] private TextMeshProUGUI keyboardHeader;
        [SerializeField] private TextMeshProUGUI controllerHeader;

        private bool _isVisible = false;
        private float _targetAlpha = 0f;

        // ── Static binding tables ──────────────────────────────────────────────

        private static readonly (string action, string kb, string pad)[] Bindings =
        {
            // Action                  Keyboard + Mouse          Controller
            ("Grapple Fire",          "Left Mouse Button",       "Right Trigger"),
            ("Secondary Grapple",     "Middle Mouse Button",     "Left Shoulder"),
            ("Grip / Attach",         "Right Mouse Button",      "Right Bumper"),
            ("Retract Rope",          "Left Shift",              "Left Bumper"),
            ("Extend Rope",           "Left Ctrl",               "Left Trigger"),
            ("",                      "",                        ""),
            ("Move / Thruster",       "W A S D",                 "Left Stick"),
            ("Aim / Camera",          "Mouse",                   "Right Stick"),
            ("",                      "",                        ""),
            ("Pause",                 "Escape",                  "Start / Menu"),
            ("",                      "",                        ""),
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ("Debug Menu",            "Backtick (`)",            "—"),
            ("Reset Player",          "R",                       "—"),
#endif
            ("Controls Reference",    "Tab",                     "Select / Back"),
        };

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            _targetAlpha = 0f;
            _isVisible   = false;

            PopulateColumns();
        }

        private void Update()
        {
            CheckToggleInput();
            FadePanel();
        }

        // ── Input ──────────────────────────────────────────────────────────────

        private void CheckToggleInput()
        {
            TitanAscent.Input.InputHandler ih = TitanAscent.Input.InputHandler.Instance;

            bool toggle = false;

            // Keyboard: Tab
            UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.tabKey.wasPressedThisFrame) toggle = true;

            // Gamepad: Select / Back button
            UnityEngine.InputSystem.Gamepad gp = UnityEngine.InputSystem.Gamepad.current;
            if (gp != null && gp.selectButton.wasPressedThisFrame) toggle = true;

            if (toggle) SetVisible(!_isVisible);
        }

        private void FadePanel()
        {
            if (canvasGroup == null) return;

            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, _targetAlpha, Time.unscaledDeltaTime * fadeSpeed);

            bool shouldBeActive = canvasGroup.alpha > 0.01f;
            if (panel != null && panel.activeSelf != shouldBeActive)
                panel.SetActive(shouldBeActive);
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public void SetVisible(bool visible)
        {
            _isVisible   = visible;
            _targetAlpha = visible ? 1f : 0f;

            if (visible && panel != null) panel.SetActive(true);
        }

        public void Toggle() => SetVisible(!_isVisible);

        // ── Column population ─────────────────────────────────────────────────

        private void PopulateColumns()
        {
            if (keyboardHeader   != null) keyboardHeader.text   = "KEYBOARD + MOUSE";
            if (controllerHeader != null) controllerHeader.text = "CONTROLLER";

            if (keyboardColumnText == null && controllerColumnText == null) return;

            var sbKb  = new StringBuilder();
            var sbPad = new StringBuilder();

            foreach (var (action, kb, pad) in Bindings)
            {
                if (string.IsNullOrEmpty(action))
                {
                    sbKb.AppendLine();
                    sbPad.AppendLine();
                }
                else
                {
                    sbKb.AppendLine($"<b>{action}</b>  <color=#aaaaaa>{kb}</color>");
                    sbPad.AppendLine($"<b>{action}</b>  <color=#aaaaaa>{pad}</color>");
                }
            }

            if (keyboardColumnText   != null) keyboardColumnText.text   = sbKb.ToString();
            if (controllerColumnText != null) controllerColumnText.text = sbPad.ToString();
        }
    }
}
