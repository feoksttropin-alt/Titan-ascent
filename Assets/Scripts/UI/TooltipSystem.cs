using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

namespace TitanAscent.UI
{
    // -----------------------------------------------------------------------
    // TooltipTrigger — attach to any UI element that should show a tooltip
    // -----------------------------------------------------------------------

    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] public string message = "";

        private const float HOVER_DELAY = 0.8f;

        private Coroutine _showCoroutine;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_showCoroutine != null) StopCoroutine(_showCoroutine);
            _showCoroutine = StartCoroutine(ShowAfterDelay());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_showCoroutine != null)
            {
                StopCoroutine(_showCoroutine);
                _showCoroutine = null;
            }
            TooltipSystem.Hide();
        }

        private IEnumerator ShowAfterDelay()
        {
            yield return new WaitForSecondsRealtime(HOVER_DELAY);
            Vector2 screenPos = Input.mousePosition;
            TooltipSystem.Instance?.Show(message, screenPos);
        }
    }

    // -----------------------------------------------------------------------
    // TooltipSystem — singleton that drives the tooltip panel
    // -----------------------------------------------------------------------

    /// <summary>
    /// Singleton tooltip panel.
    /// Static Show(string, float) / Hide() API can be called before Awake —
    /// messages queued before initialisation are flushed once the singleton is ready.
    /// TooltipTrigger components call the instance overload Show(string, Vector2).
    /// </summary>
    public class TooltipSystem : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Singleton
        // -----------------------------------------------------------------------

        public static TooltipSystem Instance { get; private set; }

        // -----------------------------------------------------------------------
        // Pre-Awake queue
        // -----------------------------------------------------------------------

        private struct QueuedMessage
        {
            public string message;
            public float  duration;
        }

        private static QueuedMessage? _pendingMessage = null;

        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [Header("References")]
        [SerializeField] private CanvasGroup       tooltipPanel;
        [SerializeField] private TextMeshProUGUI   tooltipText;
        [SerializeField] private Canvas            canvas;

        [Header("Appearance")]
        [SerializeField] private float fadeDuration  = 0.2f;
        [SerializeField] private float cursorOffset  = 15f;
        [SerializeField] private Vector2 panelSize   = new Vector2(240f, 60f);

        // -----------------------------------------------------------------------
        // Private state
        // -----------------------------------------------------------------------

        private RectTransform _panelRect;
        private Coroutine     _fadeCoroutine;
        private Coroutine     _autoHideCoroutine;

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

            if (tooltipPanel != null)
            {
                _panelRect = tooltipPanel.GetComponent<RectTransform>();
                tooltipPanel.alpha = 0f;
                tooltipPanel.gameObject.SetActive(false);
            }

            // Flush any message that was queued before Awake ran.
            if (_pendingMessage.HasValue)
            {
                QueuedMessage q = _pendingMessage.Value;
                _pendingMessage = null;
                ShowWithDuration(q.message, q.duration);
            }
        }

        // -----------------------------------------------------------------------
        // Static API (safe to call any time, including before Awake)
        // -----------------------------------------------------------------------

        /// <summary>
        /// Show a timed tooltip centred on screen. Safe to call before Awake —
        /// if the singleton is not yet initialised the message is queued and shown
        /// as soon as the instance becomes available.
        /// </summary>
        public static void Show(string message, float duration = 3f)
        {
            if (Instance != null)
            {
                Instance.ShowWithDuration(message, duration);
            }
            else
            {
                // Queue the message; Awake will flush it.
                _pendingMessage = new QueuedMessage { message = message, duration = duration };
            }
        }

        /// <summary>Hide the tooltip immediately (static convenience wrapper).</summary>
        public static void Hide()
        {
            Instance?.HideInstance();
        }

        // -----------------------------------------------------------------------
        // Instance API (used by TooltipTrigger for cursor-positioned tooltips)
        // -----------------------------------------------------------------------

        /// <summary>Show tooltip with message near the given screen position.</summary>
        public void Show(string message, Vector2 screenPosition)
        {
            if (tooltipPanel == null) return;

            CancelAutoHide();

            if (tooltipText != null)
                tooltipText.text = message;

            tooltipPanel.gameObject.SetActive(true);
            PositionNearCursor(screenPosition);

            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeIn());
        }

        /// <summary>Hide the tooltip immediately (instance method).</summary>
        public void HideInstance()
        {
            if (tooltipPanel == null) return;

            CancelAutoHide();
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            tooltipPanel.alpha = 0f;
            tooltipPanel.gameObject.SetActive(false);
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        private void ShowWithDuration(string message, float duration)
        {
            if (tooltipPanel == null) return;

            CancelAutoHide();

            if (tooltipText != null)
                tooltipText.text = message;

            // Centre the panel on screen
            if (_panelRect != null)
            {
                Vector2 canvasSize = GetCanvasSize();
                _panelRect.anchoredPosition = new Vector2(
                    (canvasSize.x - panelSize.x) * 0.5f,
                    canvasSize.y * 0.5f);
            }

            tooltipPanel.gameObject.SetActive(true);

            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeIn());

            if (duration > 0f)
                _autoHideCoroutine = StartCoroutine(AutoHideAfter(duration));
        }

        private void CancelAutoHide()
        {
            if (_autoHideCoroutine != null)
            {
                StopCoroutine(_autoHideCoroutine);
                _autoHideCoroutine = null;
            }
        }

        private IEnumerator AutoHideAfter(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            HideInstance();
        }

        private void PositionNearCursor(Vector2 screenPos)
        {
            if (_panelRect == null) return;

            Vector2 anchoredPos = ScreenToCanvasPos(screenPos);

            // Apply offset
            anchoredPos.x += cursorOffset;
            anchoredPos.y += cursorOffset;

            // Clamp within screen bounds (canvas space)
            Vector2 canvasSize = GetCanvasSize();
            anchoredPos.x = Mathf.Clamp(anchoredPos.x, 0f,       canvasSize.x - panelSize.x);
            anchoredPos.y = Mathf.Clamp(anchoredPos.y, panelSize.y, canvasSize.y);

            _panelRect.anchoredPosition = anchoredPos;
        }

        private Vector2 ScreenToCanvasPos(Vector2 screenPos)
        {
            if (canvas == null) return screenPos;

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            if (canvasRect == null) return screenPos;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, canvas.worldCamera, out Vector2 localPoint);

            // Convert from centered rect coordinates to top-left origin
            Vector2 canvasSize = canvasRect.sizeDelta;
            localPoint.x += canvasSize.x * 0.5f;
            localPoint.y += canvasSize.y * 0.5f;

            return localPoint;
        }

        private Vector2 GetCanvasSize()
        {
            if (canvas == null) return new Vector2(Screen.width, Screen.height);
            RectTransform rt = canvas.GetComponent<RectTransform>();
            return rt != null ? rt.sizeDelta : new Vector2(Screen.width, Screen.height);
        }

        private IEnumerator FadeIn()
        {
            float elapsed = 0f;
            tooltipPanel.alpha = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                tooltipPanel.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }
            tooltipPanel.alpha = 1f;
        }
    }
}
