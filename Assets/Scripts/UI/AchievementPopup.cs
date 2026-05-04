using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TitanAscent.Systems;

namespace TitanAscent.UI
{
    /// <summary>
    /// Displays a slide-in / fade-out popup in the top-right corner when an
    /// achievement is unlocked.  Multiple achievements are queued and shown
    /// one at a time with a 0.5 s gap between them.
    /// The popup holds for ~4 seconds then fades out.
    /// </summary>
    public class AchievementPopup : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AchievementSystem achievementSystem;

        [Header("Panel")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private CanvasGroup panelCanvasGroup;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI headerText;      // "Achievement Unlocked"
        [SerializeField] private TextMeshProUGUI nameText;        // large
        [SerializeField] private TextMeshProUGUI descriptionText; // small

        [Header("Timing")]
        [SerializeField] private float slideInDuration   = 0.4f;
        [SerializeField] private float holdDuration      = 4.0f;
        [SerializeField] private float fadeOutDuration   = 0.5f;
        [SerializeField] private float slideOutDuration  = 0.3f;
        [SerializeField] private float gapBetweenPopups  = 0.5f;

        [Header("Icon Resources")]
        [SerializeField] private string iconResourceFolder = "AchievementIcons";

        // ── Private state ────────────────────────────────────────────────────────
        private readonly Queue<Achievement> pendingQueue = new Queue<Achievement>();
        private bool isShowing = false;
        private Coroutine _queueCoroutine;
        private readonly Dictionary<string, Sprite> _iconCache = new Dictionary<string, Sprite>();

        // Panel is positioned offscreen to the right
        private float offscreenX;
        private float onscreenX;

        private void Awake()
        {
            if (achievementSystem == null)
                achievementSystem = FindFirstObjectByType<AchievementSystem>();

            if (achievementSystem != null)
                achievementSystem.OnAchievementUnlocked.AddListener(EnqueueAchievement);

            // Auto-add CanvasGroup for fade support if not assigned
            if (panelCanvasGroup == null && panelRoot != null)
                panelCanvasGroup = panelRoot.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null && panelRoot != null)
                panelCanvasGroup = panelRoot.gameObject.AddComponent<CanvasGroup>();

            // Measure offscreen offset from the panel's own width
            if (panelRoot != null)
            {
                onscreenX  = 0f;
                offscreenX = panelRoot.rect.width + 20f; // fully outside canvas
                SetPanelX(offscreenX);
            }

            // Start fully transparent
            SetPanelAlpha(0f);
        }

        private void OnDestroy()
        {
            if (achievementSystem != null)
                achievementSystem.OnAchievementUnlocked.RemoveListener(EnqueueAchievement);
            if (_queueCoroutine != null)
                StopCoroutine(_queueCoroutine);
        }

        // ── Public entry point ────────────────────────────────────────────────────

        public void EnqueueAchievement(Achievement achievement)
        {
            pendingQueue.Enqueue(achievement);
            if (!isShowing)
                _queueCoroutine = StartCoroutine(ProcessQueue());
        }

        // ── Coroutine pipeline ────────────────────────────────────────────────────

        private IEnumerator ProcessQueue()
        {
            isShowing = true;

            while (pendingQueue.Count > 0)
            {
                Achievement a = pendingQueue.Dequeue();
                yield return ShowAchievement(a);

                // Gap between consecutive popups
                if (pendingQueue.Count > 0)
                    yield return new WaitForSecondsRealtime(gapBetweenPopups);
            }

            isShowing = false;
            _queueCoroutine = null;
        }

        private IEnumerator ShowAchievement(Achievement achievement)
        {
            // ── Populate fields ──────────────────────────────────────────────────
            if (headerText != null)
                headerText.text = "Achievement Unlocked";

            // Secret achievements reveal "???" first
            bool isSecret = achievement.isSecret;
            if (nameText != null)
                nameText.text = isSecret ? "???" : achievement.displayName;

            if (descriptionText != null)
                descriptionText.text = isSecret ? "???" : achievement.description;

            if (iconImage != null)
            {
                Sprite icon = LoadIcon(achievement.iconName);
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }

            // Reset alpha before slide-in
            SetPanelAlpha(1f);

            // ── Slide in (ease-out) ──────────────────────────────────────────────
            yield return SlidePanel(offscreenX, onscreenX, slideInDuration, easeOut: true);

            // ── Secret name reveal after 0.5 s ───────────────────────────────────
            if (isSecret)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                if (nameText != null)        nameText.text        = achievement.displayName;
                if (descriptionText != null) descriptionText.text = achievement.description;
                yield return ShineEffect();
            }

            // ── Hold (~4 seconds total) ───────────────────────────────────────────
            float holdRemaining = isSecret ? holdDuration - 0.5f : holdDuration;
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, holdRemaining));

            // ── Fade out ─────────────────────────────────────────────────────────
            yield return FadePanel(1f, 0f, fadeOutDuration);

            // ── Slide back offscreen ─────────────────────────────────────────────
            SetPanelX(offscreenX);
            SetPanelAlpha(0f);
        }

        // ── Animation helpers ─────────────────────────────────────────────────────

        private IEnumerator SlidePanel(float fromX, float toX, float duration, bool easeOut)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = easeOut ? EaseOut(t) : EaseIn(t);
                SetPanelX(Mathf.LerpUnclamped(fromX, toX, eased));
                yield return null;
            }
            SetPanelX(toX);
        }

        private IEnumerator FadePanel(float fromAlpha, float toAlpha, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetPanelAlpha(Mathf.Lerp(fromAlpha, toAlpha, t));
                yield return null;
            }
            SetPanelAlpha(toAlpha);
        }

        /// <summary>Alpha flash shine effect on the name text.</summary>
        private IEnumerator ShineEffect()
        {
            if (nameText == null) yield break;

            const float shineDuration = 0.4f;
            float elapsed = 0f;
            Color baseColor = nameText.color;

            while (elapsed < shineDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / shineDuration;
                // Pulse alpha: 1 → 0 → 1 over the duration
                float alpha = Mathf.Abs(Mathf.Sin(t * Mathf.PI));
                nameText.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                yield return null;
            }

            nameText.color = baseColor;
        }

        private void SetPanelX(float x)
        {
            if (panelRoot == null) return;
            Vector2 pos = panelRoot.anchoredPosition;
            pos.x = x;
            panelRoot.anchoredPosition = pos;
        }

        private void SetPanelAlpha(float alpha)
        {
            if (panelCanvasGroup != null)
                panelCanvasGroup.alpha = alpha;
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
        private static float EaseIn(float t)  => t * t;

        // ── Icon loading ──────────────────────────────────────────────────────────

        private Sprite LoadIcon(string iconName)
        {
            if (string.IsNullOrEmpty(iconName)) return null;
            if (_iconCache.TryGetValue(iconName, out Sprite cached)) return cached;
            Sprite icon = Resources.Load<Sprite>($"{iconResourceFolder}/{iconName}");
            if (icon != null) _iconCache[iconName] = icon;
            return icon;
        }
    }
}
