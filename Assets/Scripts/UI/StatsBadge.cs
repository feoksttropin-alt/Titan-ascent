using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TitanAscent.Systems;

namespace TitanAscent.UI
{
    public enum BadgeType
    {
        NewHeightRecord,
        LongestFall,
        FastestZone,
        ChainRecord
    }

    /// <summary>
    /// Personal best badge popup system. Badges slide in from the right edge,
    /// spin on arrival, hold briefly, then slide back out. Positioned top-right
    /// below the achievement popup area. Multiple badges queue sequentially.
    /// </summary>
    public class StatsBadge : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Singleton / static trigger
        // -----------------------------------------------------------------------

        private static StatsBadge _instance;

        public static void TriggerBadge(BadgeType type, string value)
        {
            if (_instance != null)
                _instance.EnqueueBadge(type, value);
        }

        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [Header("References")]
        [SerializeField] private RectTransform  badgeContainer;
        [SerializeField] private Image          badgeIcon;
        [SerializeField] private TextMeshProUGUI badgeValue;
        [SerializeField] private TextMeshProUGUI badgeLabel;

        [Header("Animation")]
        [SerializeField] private AnimationCurve slideInCurve  = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private float slideInDuration  = 0.4f;
        [SerializeField] private float rotateDuration   = 0.3f;
        [SerializeField] private float holdDuration     = 1.2f;
        [SerializeField] private float slideOutDuration = 0.2f;
        [SerializeField] private float gapBetweenBadges = 0.5f;

        [Header("Colors")]
        [SerializeField] private Color colorGold  = new Color(1.0f, 0.84f, 0.0f, 1f);
        [SerializeField] private Color colorRed   = new Color(0.9f, 0.2f,  0.2f, 1f);
        [SerializeField] private Color colorBlue  = new Color(0.2f, 0.5f,  1.0f, 1f);

        // -----------------------------------------------------------------------
        // Private state
        // -----------------------------------------------------------------------

        private readonly Queue<(BadgeType type, string value)> _queue = new Queue<(BadgeType, string)>();
        private bool _isShowing = false;

        // Anchor: hidden position (off right edge), shown position
        private float _hiddenX;
        private float _shownX;

        private FallTracker _fallTracker;

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;

            // Start hidden (off screen to the right)
            if (badgeContainer != null)
            {
                _shownX  = badgeContainer.anchoredPosition.x;
                _hiddenX = _shownX + badgeContainer.rect.width + 20f;
                badgeContainer.anchoredPosition = new Vector2(_hiddenX, badgeContainer.anchoredPosition.y);
                badgeContainer.gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            _fallTracker = FindFirstObjectByType<FallTracker>();
            if (_fallTracker != null)
                _fallTracker.OnNewHeightRecord.AddListener(OnNewHeightRecord);
        }

        private void OnDestroy()
        {
            if (_fallTracker != null)
                _fallTracker.OnNewHeightRecord.RemoveListener(OnNewHeightRecord);

            if (_instance == this) _instance = null;
        }

        // -----------------------------------------------------------------------
        // FallTracker hook
        // -----------------------------------------------------------------------

        private void OnNewHeightRecord(float height)
        {
            EnqueueBadge(BadgeType.NewHeightRecord, $"{height:F0}m");
        }

        // -----------------------------------------------------------------------
        // Queue
        // -----------------------------------------------------------------------

        public void EnqueueBadge(BadgeType type, string value)
        {
            _queue.Enqueue((type, value));
            if (!_isShowing)
                StartCoroutine(ProcessQueue());
        }

        private IEnumerator ProcessQueue()
        {
            _isShowing = true;
            while (_queue.Count > 0)
            {
                var (type, value) = _queue.Dequeue();
                yield return ShowBadge(type, value);
                if (_queue.Count > 0)
                    yield return new WaitForSeconds(gapBetweenBadges);
            }
            _isShowing = false;
        }

        // -----------------------------------------------------------------------
        // Show animation
        // -----------------------------------------------------------------------

        private IEnumerator ShowBadge(BadgeType type, string value)
        {
            if (badgeContainer == null) yield break;

            // Configure content
            Color badgeColor = GetColor(type);
            if (badgeIcon  != null) badgeIcon.color  = badgeColor;
            if (badgeValue != null)
            {
                badgeValue.text  = value;
                badgeValue.color = badgeColor;
            }
            if (badgeLabel != null)
            {
                badgeLabel.text  = GetLabel(type);
                badgeLabel.color = Color.white;
            }

            // Reset position and rotation
            Vector2 anchorPos = badgeContainer.anchoredPosition;
            anchorPos.x = _hiddenX;
            badgeContainer.anchoredPosition = anchorPos;
            badgeContainer.localRotation    = Quaternion.identity;
            badgeContainer.gameObject.SetActive(true);

            // --- Slide in ---
            float elapsed = 0f;
            while (elapsed < slideInDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / slideInDuration);
                float curved = slideInCurve.Evaluate(t);
                anchorPos.x = Mathf.Lerp(_hiddenX, _shownX, curved);
                badgeContainer.anchoredPosition = anchorPos;
                yield return null;
            }
            anchorPos.x = _shownX;
            badgeContainer.anchoredPosition = anchorPos;

            // --- Rotate 360 after arrival ---
            elapsed = 0f;
            while (elapsed < rotateDuration)
            {
                elapsed += Time.deltaTime;
                float t       = Mathf.Clamp01(elapsed / rotateDuration);
                float degrees = Mathf.Lerp(0f, 360f, t);
                badgeContainer.localRotation = Quaternion.Euler(0f, 0f, degrees);
                yield return null;
            }
            badgeContainer.localRotation = Quaternion.identity;

            // --- Hold ---
            yield return new WaitForSeconds(holdDuration);

            // --- Slide out ---
            elapsed = 0f;
            float startX = badgeContainer.anchoredPosition.x;
            while (elapsed < slideOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / slideOutDuration);
                anchorPos   = badgeContainer.anchoredPosition;
                anchorPos.x = Mathf.Lerp(startX, _hiddenX, t);
                badgeContainer.anchoredPosition = anchorPos;
                yield return null;
            }

            badgeContainer.gameObject.SetActive(false);
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private Color GetColor(BadgeType type)
        {
            switch (type)
            {
                case BadgeType.NewHeightRecord: return colorGold;
                case BadgeType.ChainRecord:     return colorGold;
                case BadgeType.LongestFall:     return colorRed;
                case BadgeType.FastestZone:     return colorBlue;
                default:                        return Color.white;
            }
        }

        private static string GetLabel(BadgeType type)
        {
            switch (type)
            {
                case BadgeType.NewHeightRecord: return "NEW HEIGHT RECORD";
                case BadgeType.LongestFall:     return "LONGEST FALL";
                case BadgeType.FastestZone:     return "FASTEST ZONE";
                case BadgeType.ChainRecord:     return "CHAIN RECORD";
                default:                        return type.ToString().ToUpper();
            }
        }
    }
}
