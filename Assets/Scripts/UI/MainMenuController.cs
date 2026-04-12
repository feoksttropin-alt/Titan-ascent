using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using TitanAscent.Systems;

namespace TitanAscent.UI
{
    /// <summary>
    /// Top-level main menu controller. Manages panel navigation with CanvasGroup fades.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector — panels
        // -----------------------------------------------------------------------

        [Header("Panels")]
        [SerializeField] private CanvasGroup mainPanel;
        [SerializeField] private CanvasGroup settingsPanel;
        [SerializeField] private CanvasGroup cosmeticsPanel;
        [SerializeField] private CanvasGroup leaderboardPanel;
        [SerializeField] private CanvasGroup runHistoryPanel;
        [SerializeField] private CanvasGroup creditsPanel;

        [Header("Credits")]
        [SerializeField] private TextMeshProUGUI creditsText;

        [Header("Tutorial Dialog")]
        [SerializeField] private GameObject tutorialPromptDialog;

        [Header("Transition")]
        [SerializeField] private float fadeDuration = 0.3f;

        [Header("UI Components")]
        [SerializeField] private RunHistoryUI  runHistoryUI;
        [SerializeField] private CosmeticMenuUI cosmeticMenuUI;

        [Header("Main Panel — Best Run Stats")]
        [SerializeField] private TextMeshProUGUI bestHeightText;
        [SerializeField] private TextMeshProUGUI totalClimbsText;
        [SerializeField] private TextMeshProUGUI speedrunPBText;

        [Header("Leaderboard Panel")]
        [SerializeField] private Transform leaderboardEntryContainer;
        [SerializeField] private TextMeshProUGUI leaderboardEntryPrefab;

        [Header("Ghost Playback")]
        [SerializeField] private GhostSystem ghostSystem;

        // -----------------------------------------------------------------------
        // Private state
        // -----------------------------------------------------------------------

        private SaveManager       _saveManager;
        private LeaderboardManager _leaderboardManager;

        private CanvasGroup _currentPanel;
        private Coroutine   _fadeCoroutine;
        private const float FIRST_RUN_DELAY = 0.5f;

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            _saveManager        = FindFirstObjectByType<SaveManager>();
            _leaderboardManager = FindFirstObjectByType<LeaderboardManager>();

            if (ghostSystem == null)
                ghostSystem = FindFirstObjectByType<GhostSystem>();

            // Hide all panels immediately
            SetInstant(mainPanel,        true);
            SetInstant(settingsPanel,    false);
            SetInstant(cosmeticsPanel,   false);
            SetInstant(leaderboardPanel, false);
            SetInstant(runHistoryPanel,  false);
            SetInstant(creditsPanel,     false);

            _currentPanel = mainPanel;

            if (tutorialPromptDialog != null)
                tutorialPromptDialog.SetActive(false);

            if (runHistoryUI == null && runHistoryPanel != null)
                runHistoryUI = runHistoryPanel.GetComponentInChildren<RunHistoryUI>(true);

            if (cosmeticMenuUI == null && cosmeticsPanel != null)
                cosmeticMenuUI = cosmeticsPanel.GetComponentInChildren<CosmeticMenuUI>(true);
        }

        private void Start()
        {
            PopulateCredits();
            RefreshMainPanelStats();

            // First-time flow: show tutorial prompt after short delay
            if (_saveManager != null && _saveManager.CurrentData.totalClimbs == 0)
                StartCoroutine(ShowTutorialPromptDelayed());
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                NavigateBack();
        }

        // -----------------------------------------------------------------------
        // Public navigation methods (wire to buttons in Inspector)
        // -----------------------------------------------------------------------

        public void ShowMainPanel()  => TransitionTo(mainPanel);
        public void ShowSettings()   => TransitionTo(settingsPanel);
        public void ShowCredits()    => TransitionTo(creditsPanel);

        public void ShowCosmetics()
        {
            cosmeticMenuUI?.RefreshDisplay();
            TransitionTo(cosmeticsPanel);
        }

        public void ShowRunHistory()
        {
            runHistoryUI?.Refresh();
            TransitionTo(runHistoryPanel);
        }

        public void ShowLeaderboard()
        {
            PopulateLeaderboard();
            TransitionTo(leaderboardPanel);
        }

        /// <summary>Generic panel switcher by name, usable from UnityEvents.</summary>
        public void ShowPanel(string panelName)
        {
            switch (panelName)
            {
                case "Main":        ShowMainPanel();  break;
                case "Settings":    ShowSettings();   break;
                case "Cosmetics":   ShowCosmetics();  break;
                case "Leaderboard": ShowLeaderboard(); break;
                case "RunHistory":  ShowRunHistory();  break;
                case "Credits":     ShowCredits();     break;
                default:
                    Debug.LogWarning($"[MainMenuController] Unknown panel name: '{panelName}'");
                    break;
            }
        }

        public void NavigateBack()
        {
            if (_currentPanel != mainPanel)
                TransitionTo(mainPanel);
        }

        // -----------------------------------------------------------------------
        // Game flow
        // -----------------------------------------------------------------------

        /// <summary>Loads the main game scene. Wire to the Play button.</summary>
        public void StartGame()
        {
            SceneManager.LoadScene(SceneNames.MainGame);
        }

        /// <summary>Quits the application. Wire to the Quit button.</summary>
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // -----------------------------------------------------------------------
        // Ghost playback
        // -----------------------------------------------------------------------

        /// <summary>
        /// Loads and starts playback of the last recorded ghost run.
        /// Called from the Run History UI or any button wired to this method.
        /// </summary>
        public void PlayLastGhost()
        {
            if (ghostSystem == null)
            {
                Debug.LogWarning("[MainMenuController] GhostSystem reference is null; cannot play ghost.");
                return;
            }

            string path = GhostSystem.LastGhostPath;
            bool loaded = ghostSystem.LoadGhost(path);
            if (!loaded)
            {
                Debug.LogWarning("[MainMenuController] No ghost file found at: " + path);
                return;
            }

            ghostSystem.StartPlayback();
        }

        /// <summary>
        /// Loads a ghost from a specific file path (e.g., a session-specific ghost
        /// surfaced from run history) and starts playback.
        /// </summary>
        public void PlayGhostFromHistory(string filePath)
        {
            if (ghostSystem == null)
            {
                Debug.LogWarning("[MainMenuController] GhostSystem reference is null; cannot play ghost.");
                return;
            }

            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogWarning("[MainMenuController] PlayGhostFromHistory called with empty path.");
                return;
            }

            bool loaded = ghostSystem.LoadGhost(filePath);
            if (!loaded)
            {
                Debug.LogWarning("[MainMenuController] Could not load ghost from: " + filePath);
                return;
            }

            ghostSystem.StartPlayback();
        }

        /// <summary>Stops an active ghost playback.</summary>
        public void StopGhostPlayback()
        {
            ghostSystem?.StopPlayback();
        }

        // -----------------------------------------------------------------------
        // Main panel stats
        // -----------------------------------------------------------------------

        private void RefreshMainPanelStats()
        {
            if (_saveManager == null) return;

            SaveData data = _saveManager.CurrentData;

            if (bestHeightText != null)
                bestHeightText.text = $"{data.bestHeight:F1} m";

            if (totalClimbsText != null)
                totalClimbsText.text = data.totalClimbs.ToString();

            if (speedrunPBText != null)
            {
                speedrunPBText.text = data.speedrunPB > 0f
                    ? LeaderboardManager.FormatTime(data.speedrunPB)
                    : "--";
            }
        }

        // -----------------------------------------------------------------------
        // Leaderboard
        // -----------------------------------------------------------------------

        private void PopulateLeaderboard()
        {
            if (_leaderboardManager == null) return;
            if (leaderboardEntryContainer == null) return;

            // Clear existing entries
            foreach (Transform child in leaderboardEntryContainer)
                Destroy(child.gameObject);

            List<LeaderboardEntry> entries = _leaderboardManager.GetTopEntries(10);

            for (int i = 0; i < entries.Count; i++)
            {
                LeaderboardEntry entry = entries[i];

                string line = $"{i + 1}. {entry.playerName}   " +
                              $"{entry.heightReached:F1} m   " +
                              $"{LeaderboardManager.FormatTime(entry.timeSeconds)}";

                if (leaderboardEntryPrefab != null)
                {
                    TextMeshProUGUI label =
                        Instantiate(leaderboardEntryPrefab, leaderboardEntryContainer);

                    if (entry.isLocalPlayer)
                    {
                        // Bold and tinted gold to distinguish the local player's entry
                        label.text      = $"<b>{line}</b>";
                        label.color     = new Color(1f, 0.84f, 0f); // gold
                    }
                    else
                    {
                        label.text  = line;
                        label.color = Color.white;
                    }
                }
                else
                {
                    // Fallback: log if no prefab is assigned
                    Debug.LogWarning($"[MainMenuController] leaderboardEntryPrefab is not assigned. Entry {i + 1}: {line}");
                }
            }

            if (entries.Count == 0)
            {
                if (leaderboardEntryPrefab != null)
                {
                    TextMeshProUGUI empty =
                        Instantiate(leaderboardEntryPrefab, leaderboardEntryContainer);
                    empty.text  = "No runs recorded yet.";
                    empty.color = Color.gray;
                }
            }
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        private void TransitionTo(CanvasGroup target)
        {
            if (target == null || target == _currentPanel) return;

            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);

            _fadeCoroutine = StartCoroutine(FadeTransition(_currentPanel, target));
        }

        private IEnumerator FadeTransition(CanvasGroup from, CanvasGroup to)
        {
            // Fade out current
            if (from != null)
            {
                float elapsed = 0f;
                float startAlpha = from.alpha;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    from.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeDuration);
                    yield return null;
                }
                SetInstant(from, false);
            }

            _currentPanel = to;

            // Fade in new
            if (to != null)
            {
                SetInstant(to, true);
                to.alpha = 0f;
                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    to.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
                    yield return null;
                }
                to.alpha = 1f;
            }
        }

        private static void SetInstant(CanvasGroup cg, bool visible)
        {
            if (cg == null) return;
            cg.alpha          = visible ? 1f : 0f;
            cg.interactable   = visible;
            cg.blocksRaycasts = visible;
            cg.gameObject.SetActive(visible);
        }

        private void PopulateCredits()
        {
            if (creditsText == null) return;

            string version = BuildVersionManager.VersionString;
            creditsText.text =
                "<b>TITAN ASCENT</b>\n\n" +
                $"Version {version}\n" +
                "Built with Unity 2022.3\n\n" +
                "Thank you for playing.\n" +
                "Every fall is a lesson.\n" +
                "Every climb a story.";
        }

        private IEnumerator ShowTutorialPromptDelayed()
        {
            yield return new WaitForSecondsRealtime(FIRST_RUN_DELAY);

            if (tutorialPromptDialog != null)
                tutorialPromptDialog.SetActive(true);
        }
    }
}
