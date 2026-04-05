using System.Collections;
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
            TooltipSystem.Instance?.Hide();
        }

        private IEnumerator ShowAfterDelay()
        {
            yield return new WaitForSecondsRealtime(HOVER_DELAY);
            if (TooltipSystem.Instance != null)
            {
                Vector2 screenPos = Input.mousePosition;
                TooltipSystem.Instance.Show(message, screenPos);
            }
        }
    }

    // -----------------------------------------------------------------------
    // TooltipSystem — singleton that drives the tooltip panel
    // -----------------------------------------------------------------------

    /// <summary>
    /// Singleton tooltip panel. TooltipTrigger components call Show/Hide.
    /// </summary>
    public class TooltipSystem : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Singleton
        // -----------------------------------------------------------------------

        public static TooltipSystem Instance { get; private set; }

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
        }

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>Show tooltip with message near the given screen position.</summary>
        public void Show(string message, Vector2 screenPosition)
        {
            if (tooltipPanel == null) return;

            if (tooltipText != null)
                tooltipText.text = message;

            tooltipPanel.gameObject.SetActive(true);
            PositionNearCursor(screenPosition);

            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeIn());
        }

        /// <summary>Hide the tooltip immediately.</summary>
        public void Hide()
        {
            if (tooltipPanel == null) return;

            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            tooltipPanel.alpha = 0f;
            tooltipPanel.gameObject.SetActive(false);
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

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
