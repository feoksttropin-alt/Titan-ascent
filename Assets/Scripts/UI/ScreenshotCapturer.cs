using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TitanAscent.UI
{
    /// <summary>
    /// Screenshot tool with HUD toggle.
    ///   F12 (configurable) — standard screenshot with white-flash confirmation
    ///   F11               — 4x super-resolution screenshot
    ///   F10               — toggle HUD Canvas
    /// Only active in Editor / Development builds.
    /// </summary>
    public class ScreenshotCapturer : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [Header("Capture Key")]
        [SerializeField] private KeyCode captureKey = KeyCode.F12;

        [Header("Flash Overlay")]
        [Tooltip("CanvasGroup on a full-screen white Image used for the capture flash.")]
        [SerializeField] private CanvasGroup flashOverlay;

        private const float FlashDuration = 0.3f;

        // -----------------------------------------------------------------------
        // State
        // -----------------------------------------------------------------------

        private bool    _showToast       = false;
        private float   _toastTimer      = 0f;
        private const float ToastDuration = 1.5f;

        private int     _sessionCount    = 0;

        private Canvas  _hudCanvas       = null;
        private bool    _hudHidden       = false;
        private string  _screenshotDir;

        private GUIStyle _toastStyle;

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            _screenshotDir = Application.persistentDataPath;

            // Ensure flash overlay starts invisible
            if (flashOverlay != null)
            {
                flashOverlay.alpha          = 0f;
                flashOverlay.blocksRaycasts = false;
            }
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (IsKeyDownNIS(captureKey))
                CaptureStandard();

            if (Keyboard.current != null && Keyboard.current.f11Key.wasPressedThisFrame)
                StartCoroutine(CaptureSuper(4));

            if (Keyboard.current != null && Keyboard.current.f10Key.wasPressedThisFrame)
                ToggleHUD();

            // Toast countdown
            if (_showToast)
            {
                _toastTimer -= Time.unscaledDeltaTime;
                if (_toastTimer <= 0f)
                    _showToast = false;
            }
#endif
        }

        private void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_showToast) return;

            if (_toastStyle == null)
            {
                _toastStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize  = 14,
                    alignment = TextAnchor.MiddleCenter
                };
                _toastStyle.normal.textColor = Color.white;
                _toastStyle.normal.background = MakeTex(new Color(0f, 0f, 0f, 0.75f));
            }

            float w = 220f;
            float h = 32f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - h - 24f;
            GUI.Label(new Rect(x, y, w, h), "Screenshot saved!", _toastStyle);
#endif
        }

        // -----------------------------------------------------------------------
        // Capture methods
        // -----------------------------------------------------------------------

        private void CaptureStandard()
        {
            string path = BuildPath();
            ScreenCapture.CaptureScreenshot(path);
            _sessionCount++;
            ShowToast();
            Debug.Log($"[ScreenshotCapturer] Saved: {path}  (session #{_sessionCount})");
        }

        private IEnumerator CaptureSuper(int superSize)
        {
            // Wait until next frame so the current frame is fully rendered
            yield return new WaitForEndOfFrame();

            string path = BuildPath();
            ScreenCapture.CaptureScreenshot(path, superSize);
            _sessionCount++;
            ShowToast();
            Debug.Log($"[ScreenshotCapturer] Saved (x{superSize}): {path}  (session #{_sessionCount})");
        }

        // -----------------------------------------------------------------------
        // HUD toggle
        // -----------------------------------------------------------------------

        private void ToggleHUD()
        {
            if (_hudCanvas == null)
            {
                // Find a Canvas whose name contains "HUD"
                Canvas[] all = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                foreach (Canvas c in all)
                {
                    if (c.name.Contains("HUD"))
                    {
                        _hudCanvas = c;
                        break;
                    }
                }
            }

            if (_hudCanvas == null)
            {
                Debug.LogWarning("[ScreenshotCapturer] No Canvas with 'HUD' in name found.");
                return;
            }

            _hudHidden = !_hudHidden;
            _hudCanvas.enabled = !_hudHidden;
            Debug.Log($"[ScreenshotCapturer] HUD Canvas {(_hudHidden ? "hidden" : "shown")}.");
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        // Maps a legacy KeyCode (F1–F12 range) to the New Input System Keyboard property.
        private static bool IsKeyDownNIS(KeyCode kc)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return false;
            switch (kc)
            {
                case KeyCode.F1:  return kb.f1Key.wasPressedThisFrame;
                case KeyCode.F2:  return kb.f2Key.wasPressedThisFrame;
                case KeyCode.F3:  return kb.f3Key.wasPressedThisFrame;
                case KeyCode.F4:  return kb.f4Key.wasPressedThisFrame;
                case KeyCode.F5:  return kb.f5Key.wasPressedThisFrame;
                case KeyCode.F6:  return kb.f6Key.wasPressedThisFrame;
                case KeyCode.F7:  return kb.f7Key.wasPressedThisFrame;
                case KeyCode.F8:  return kb.f8Key.wasPressedThisFrame;
                case KeyCode.F9:  return kb.f9Key.wasPressedThisFrame;
                case KeyCode.F10: return kb.f10Key.wasPressedThisFrame;
                case KeyCode.F11: return kb.f11Key.wasPressedThisFrame;
                case KeyCode.F12: return kb.f12Key.wasPressedThisFrame;
                default:          return false;
            }
        }

        private string BuildPath()
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return Path.Combine(_screenshotDir, $"TitanAscent_screenshot_{timestamp}.png");
        }

        private void ShowToast()
        {
            _showToast  = true;
            _toastTimer = ToastDuration;
        }

        private static Texture2D MakeTex(Color c)
        {
            Texture2D t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }
}
