using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TitanAscent.Systems;

namespace TitanAscent.UI
{
    public class PostRunSummary : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Victory Panel
        // -----------------------------------------------------------------------

        [Header("Victory Panel")]
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private TextMeshProUGUI victoryHeaderText;
        [SerializeField] private TextMeshProUGUI victoryHeightText;
        [SerializeField] private TextMeshProUGUI victoryRunTimeText;
        [SerializeField] private TextMeshProUGUI victoryTotalFallsText;
        [SerializeField] private TextMeshProUGUI victoryLongestFallText;
        [SerializeField] private GameObject newRecordBadge;
        [SerializeField] private TextMeshProUGUI unlockNotificationText;

        [Header("Victory Buttons")]
        [SerializeField] private Button victoryPlayAgainButton;
        [SerializeField] private Button victoryMainMenuButton;
        [SerializeField] private Button victoryLeaderboardButton;

        // -----------------------------------------------------------------------
        // Fall Summary Panel
        // -----------------------------------------------------------------------

        [Header("Fall Summary Panel")]
        [SerializeField] private GameObject fallSummaryPanel;
        [SerializeField] private TextMeshProUGUI fallDistanceText;
        [SerializeField] private TextMeshProUGUI fallReachedHeightText;
        [SerializeField] private TextMeshProUGUI fallBestEverText;
        [SerializeField] private TextMeshProUGUI fallTotalFallsText;
        [SerializeField] private TextMeshProUGUI fallRunTimeText;
        [SerializeField] private TextMeshProUGUI fallMostClimbedZoneText;

        [Header("Fall Summary Buttons")]
        [SerializeField] private Button fallTryAgainButton;
        [SerializeField] private Button fallMainMenuButton;

        // -----------------------------------------------------------------------
        // Animation
        // -----------------------------------------------------------------------

        [Header("Animation")]
        [SerializeField] private float elementRevealDelay = 0.3f;
        [SerializeField] private float slowMoDuration = 1.0f;
        [SerializeField] private float slowMoScale = 0.3f;

        private GameManager gameManager;
        private FallTracker fallTracker;

        private float sessionStartTime;
        private float sessionBestHeightAtStart;
        private float runLongestFall;
        private int   runTotalFalls;
        private string mostClimbedZoneName = "Unknown";

        private void Awake()
        {
            gameManager = GameManager.Instance != null
                ? GameManager.Instance
                : FindFirstObjectByType<GameManager>();

            fallTracker = FindFirstObjectByType<FallTracker>();
        }

        private void Start()
        {
            // Hide both panels initially
            if (victoryPanel    != null) victoryPanel.SetActive(false);
            if (fallSummaryPanel!= null) fallSummaryPanel.SetActive(false);

            // Wire buttons
            if (victoryPlayAgainButton  != null) victoryPlayAgainButton.onClick.AddListener(OnPlayAgainClicked);
            if (victoryMainMenuButton   != null) victoryMainMenuButton.onClick.AddListener(OnMainMenuClicked);
            if (victoryLeaderboardButton!= null) victoryLeaderboardButton.onClick.AddListener(OnLeaderboardClicked);
            if (fallTryAgainButton      != null) fallTryAgainButton.onClick.AddListener(OnPlayAgainClicked);
            if (fallMainMenuButton      != null) fallMainMenuButton.onClick.AddListener(OnMainMenuClicked);

            // Subscribe to game events
            if (gameManager != null)
            {
                gameManager.OnVictory.AddListener(HandleVictory);
                gameManager.OnClimbStarted.AddListener(HandleClimbStarted);
            }

            if (fallTracker != null)
                fallTracker.OnFallCompleted.AddListener(HandleFallCompleted);
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.OnVictory.RemoveListener(HandleVictory);
                gameManager.OnClimbStarted.RemoveListener(HandleClimbStarted);
            }

            if (fallTracker != null)
                fallTracker.OnFallCompleted.RemoveListener(HandleFallCompleted);
        }

        // -----------------------------------------------------------------------
        // Event Handlers
        // -----------------------------------------------------------------------

        private void HandleClimbStarted()
        {
            sessionStartTime = Time.time;
            runLongestFall   = 0f;
            runTotalFalls    = 0;
            mostClimbedZoneName = "Unknown";

            if (gameManager != null && gameManager.GlobalStats != null)
                sessionBestHeightAtStart = gameManager.GlobalStats.bestHeight;
        }

        private void HandleFallCompleted(FallData data)
        {
            runTotalFalls++;
            if (data.distance > runLongestFall)
                runLongestFall = data.distance;

            if (data.severity >= FallSeverity.RunEnding)
                ShowFallSummary(data);
        }

        private void HandleVictory()
        {
            ShowVictorySummary();
        }

        // -----------------------------------------------------------------------
        // Show Victory
        // -----------------------------------------------------------------------

        private void ShowVictorySummary()
        {
            if (victoryPanel == null) return;

            float runTime    = gameManager != null ? gameManager.SessionTime : 0f;
            float height     = gameManager != null ? gameManager.CurrentHeight : 10000f;
            bool  isNewRecord = gameManager?.GlobalStats != null &&
                                height > sessionBestHeightAtStart;

            // Populate header
            if (victoryHeaderText    != null) victoryHeaderText.text    = "SUMMIT REACHED";
            if (victoryHeightText    != null) victoryHeightText.text    = $"{height:N0}m";
            if (victoryRunTimeText   != null) victoryRunTimeText.text   = FormatTime(runTime);
            if (victoryTotalFallsText!= null) victoryTotalFallsText.text= $"Falls: {runTotalFalls}";
            if (victoryLongestFallText!= null)victoryLongestFallText.text=$"Longest Fall: {runLongestFall:N0}m";

            if (newRecordBadge       != null) newRecordBadge.SetActive(isNewRecord);

            // Unlock notification
            string unlockedCosmetic = GetRecentUnlock();
            if (unlockNotificationText != null)
            {
                if (!string.IsNullOrEmpty(unlockedCosmetic))
                {
                    unlockNotificationText.text = $"Unlocked: {unlockedCosmetic}";
                    unlockNotificationText.gameObject.SetActive(true);
                }
                else
                {
                    unlockNotificationText.gameObject.SetActive(false);
                }
            }

            victoryPanel.SetActive(true);
            StartCoroutine(SlowMoThenNormal());
            StartCoroutine(StaggeredReveal(victoryPanel));
        }

        // -----------------------------------------------------------------------
        // Show Fall Summary
        // -----------------------------------------------------------------------

        private void ShowFallSummary(FallData data)
        {
            if (fallSummaryPanel == null) return;

            float runTime   = gameManager != null ? gameManager.SessionTime : 0f;
            float reachedH  = data.startHeight;
            float bestEver  = gameManager?.GlobalStats?.bestHeight ?? 0f;

            if (fallDistanceText     != null) fallDistanceText.text     = $"FELL {data.distance:N0}m";
            if (fallReachedHeightText!= null) fallReachedHeightText.text= $"Reached {reachedH:N0}m";
            if (fallBestEverText     != null) fallBestEverText.text     = $"Best ever: {bestEver:N0}m";
            if (fallTotalFallsText   != null) fallTotalFallsText.text   = $"Total Falls: {runTotalFalls}";
            if (fallRunTimeText      != null) fallRunTimeText.text      = FormatTime(runTime);
            if (fallMostClimbedZoneText!= null)fallMostClimbedZoneText.text=$"Zone: {mostClimbedZoneName}";

            fallSummaryPanel.SetActive(true);
            StartCoroutine(SlowMoThenNormal());
            StartCoroutine(StaggeredReveal(fallSummaryPanel));
        }

        // -----------------------------------------------------------------------
        // Animations
        // -----------------------------------------------------------------------

        private IEnumerator SlowMoThenNormal()
        {
            Time.timeScale = slowMoScale;
            float elapsed = 0f;
            while (elapsed < slowMoDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                Time.timeScale = Mathf.Lerp(slowMoScale, 1f, elapsed / slowMoDuration);
                yield return null;
            }
            Time.timeScale = 1f;
        }

        private IEnumerator StaggeredReveal(GameObject panel)
        {
            // Collect all direct children
            var children = new List<Transform>();
            foreach (Transform child in panel.transform)
                children.Add(child);

            // Hide all children initially
            foreach (Transform child in children)
            {
                CanvasGroup cg = child.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.alpha = 0f;
                }
                else
                {
                    // Add a temporary CanvasGroup
                    cg = child.gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = 0f;
                }
            }

            // Special handling for the victory header — scale up dramatically
            if (victoryPanel != null && panel == victoryPanel && victoryHeaderText != null)
            {
                victoryHeaderText.rectTransform.localScale = Vector3.zero;
            }

            // Reveal staggered
            int index = 0;
            foreach (Transform child in children)
            {
                yield return new WaitForSecondsRealtime(elementRevealDelay);

                CanvasGroup cg = child.GetComponent<CanvasGroup>();

                // Dramatic scale for the victory header
                if (victoryPanel != null && panel == victoryPanel &&
                    victoryHeaderText != null && child == victoryHeaderText.transform)
                {
                    yield return StartCoroutine(ScaleInElement(child, cg));
                }
                else
                {
                    yield return StartCoroutine(FadeInElement(cg));
                }

                index++;
            }
        }

        private IEnumerator ScaleInElement(Transform t, CanvasGroup cg)
        {
            float duration = 0.4f;
            float elapsed  = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                if (cg != null) cg.alpha = progress;
                t.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, progress);
                yield return null;
            }
            if (cg != null) cg.alpha = 1f;
            t.localScale = Vector3.one;
        }

        private IEnumerator FadeInElement(CanvasGroup cg)
        {
            if (cg == null) yield break;
            float duration = 0.25f;
            float elapsed  = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            cg.alpha = 1f;
        }

        // -----------------------------------------------------------------------
        // Button Handlers
        // -----------------------------------------------------------------------

        private void OnPlayAgainClicked()
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        private void OnMainMenuClicked()
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        private void OnLeaderboardClicked()
        {
            // Placeholder — leaderboard integration not yet implemented
            Debug.Log("[PostRunSummary] Leaderboard placeholder clicked.");
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static string FormatTime(float seconds)
        {
            int h = Mathf.FloorToInt(seconds / 3600f);
            int m = Mathf.FloorToInt((seconds % 3600f) / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);
            return h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m:00}:{s:00}";
        }

        private string GetRecentUnlock()
        {
            if (gameManager?.GlobalStats == null) return string.Empty;
            var cosmetics = gameManager.GlobalStats.unlockedCosmetics;
            if (cosmetics == null || cosmetics.Count == 0) return string.Empty;
            return cosmetics[cosmetics.Count - 1];
        }

        /// <summary>
        /// Called by ZoneManager (or similar) to track most-climbed zone name.
        /// </summary>
        public void SetMostClimbedZone(string zoneName)
        {
            mostClimbedZoneName = zoneName;
        }
    }
}
