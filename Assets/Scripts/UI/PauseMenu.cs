using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using TitanAscent.Input;
using TitanAscent.Systems;

namespace TitanAscent.UI
{
    public class PauseMenu : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private CanvasGroup pausePanel;

        [Header("Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitToMenuButton;

        [Header("Stats Display")]
        [SerializeField] private TextMeshProUGUI currentStatsText;
        [SerializeField] private TextMeshProUGUI bestHeightText;

        [Header("Settings Panel")]
        [SerializeField] private GameObject settingsPanel;

        [Header("Slide Animation")]
        [SerializeField] private float slideInDuration = 0.3f;
        [SerializeField] private float panelSlideOffsetY = 200f;

        private GameManager gameManager;
        private RectTransform panelRect;
        private Vector2 panelOnScreenPos;
        private Vector2 panelOffScreenPos;
        private Coroutine slideCoroutine;

        // Confirm dialog state
        private bool showingRestartConfirm = false;
        private bool showingQuitConfirm = false;

        private bool _isPaused = false;

        private void Awake()
        {
            gameManager = GameManager.Instance != null
                ? GameManager.Instance
                : FindFirstObjectByType<GameManager>();

            if (pausePanel != null)
            {
                panelRect = pausePanel.GetComponent<RectTransform>();
                if (panelRect != null)
                {
                    panelOnScreenPos = panelRect.anchoredPosition;
                    panelOffScreenPos = panelOnScreenPos + new Vector2(0f, panelSlideOffsetY);
                }

                // Start hidden
                pausePanel.alpha = 0f;
                pausePanel.interactable = false;
                pausePanel.blocksRaycasts = false;
                if (panelRect != null)
                    panelRect.anchoredPosition = panelOffScreenPos;
            }
        }

        private void Start()
        {
            if (resumeButton    != null) resumeButton.onClick.AddListener(OnResumeClicked);
            if (restartButton   != null) restartButton.onClick.AddListener(OnRestartClicked);
            if (settingsButton  != null) settingsButton.onClick.AddListener(OnSettingsClicked);
            if (quitToMenuButton!= null) quitToMenuButton.onClick.AddListener(OnQuitToMenuClicked);

            if (gameManager != null)
                gameManager.OnGameStateChanged.AddListener(HandleGameStateChanged);
        }

        private void Update()
        {
            if (InputHandler.Instance != null && InputHandler.Instance.Pause)
            {
                if (_isPaused)
                    Hide();
                else
                    Show();
            }
        }

        private void OnDestroy()
        {
            if (gameManager != null)
                gameManager.OnGameStateChanged.RemoveListener(HandleGameStateChanged);
        }

        // -----------------------------------------------------------------------
        // Public Show / Hide API
        // -----------------------------------------------------------------------

        /// <summary>Pause the game and display the pause panel.</summary>
        public void Show()
        {
            if (_isPaused) return;
            _isPaused = true;
            gameManager?.PauseGame();
            ShowPauseMenu();
        }

        /// <summary>Resume the game and hide the pause panel.</summary>
        public void Hide()
        {
            if (!_isPaused) return;
            _isPaused = false;
            // Ensure timeScale is restored even if GameManager is absent
            Time.timeScale = 1f;
            gameManager?.ResumeGame();
            HidePauseMenu();
        }

        // -----------------------------------------------------------------------
        // State Changes
        // -----------------------------------------------------------------------

        private void HandleGameStateChanged(GameState newState)
        {
            if (newState == GameState.Paused)
            {
                _isPaused = true;
                ShowPauseMenu();
            }
            else if (newState == GameState.Climbing || newState == GameState.MainMenu)
            {
                _isPaused = false;
                HidePauseMenu();
            }
        }

        public void ShowPauseMenu()
        {
            UpdateStatsDisplay();

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (slideCoroutine != null) StopCoroutine(slideCoroutine);
            slideCoroutine = StartCoroutine(SlideIn());
        }

        public void HidePauseMenu()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            if (slideCoroutine != null) StopCoroutine(slideCoroutine);
            slideCoroutine = StartCoroutine(SlideOut());
        }

        // -----------------------------------------------------------------------
        // Slide Animations (unscaled time so they work with timeScale == 0)
        // -----------------------------------------------------------------------

        private IEnumerator SlideIn()
        {
            if (pausePanel == null) yield break;

            pausePanel.interactable = true;
            pausePanel.blocksRaycasts = true;

            float elapsed = 0f;
            Vector2 startPos = panelRect != null ? panelRect.anchoredPosition : panelOffScreenPos;

            while (elapsed < slideInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / slideInDuration);
                pausePanel.alpha = t;
                if (panelRect != null)
                    panelRect.anchoredPosition = Vector2.Lerp(startPos, panelOnScreenPos, t);
                yield return null;
            }

            pausePanel.alpha = 1f;
            if (panelRect != null)
                panelRect.anchoredPosition = panelOnScreenPos;
        }

        private IEnumerator SlideOut()
        {
            if (pausePanel == null) yield break;

            pausePanel.interactable = false;
            pausePanel.blocksRaycasts = false;

            float elapsed = 0f;
            Vector2 startPos = panelRect != null ? panelRect.anchoredPosition : panelOnScreenPos;

            while (elapsed < slideInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / slideInDuration);
                pausePanel.alpha = 1f - t;
                if (panelRect != null)
                    panelRect.anchoredPosition = Vector2.Lerp(startPos, panelOffScreenPos, t);
                yield return null;
            }

            pausePanel.alpha = 0f;
            if (panelRect != null)
                panelRect.anchoredPosition = panelOffScreenPos;
        }

        // -----------------------------------------------------------------------
        // Stats
        // -----------------------------------------------------------------------

        private void UpdateStatsDisplay()
        {
            if (gameManager == null) return;

            if (currentStatsText != null)
            {
                float seconds = gameManager.SessionTime;
                int minutes = Mathf.FloorToInt(seconds / 60f);
                int secs    = Mathf.FloorToInt(seconds % 60f);
                currentStatsText.text =
                    $"Height: {gameManager.CurrentHeight:N0}m\n" +
                    $"Falls: {gameManager.GlobalStats?.totalFalls ?? 0}\n" +
                    $"Run Time: {minutes:00}:{secs:00}";
            }

            if (bestHeightText != null && gameManager.GlobalStats != null)
                bestHeightText.text = $"Best: {gameManager.GlobalStats.bestHeight:N0}m";
        }

        // -----------------------------------------------------------------------
        // Button Handlers
        // -----------------------------------------------------------------------

        private void OnResumeClicked()
        {
            Hide();
        }

        private void OnRestartClicked()
        {
            showingRestartConfirm = true;
        }

        private void OnSettingsClicked()
        {
            if (settingsPanel != null)
                settingsPanel.SetActive(true);
        }

        private void OnQuitToMenuClicked()
        {
            showingQuitConfirm = true;
        }

        // -----------------------------------------------------------------------
        // IMGUI Confirm Dialogs
        // -----------------------------------------------------------------------

        private void OnGUI()
        {
            if (showingRestartConfirm)
                DrawConfirmDialog(
                    "Restart?",
                    "Your best height will be preserved.",
                    () =>
                    {
                        showingRestartConfirm = false;
                        Time.timeScale = 1f;
                        SceneManager.LoadScene(SceneNames.MainGame);
                    },
                    () => showingRestartConfirm = false
                );

            if (showingQuitConfirm)
                DrawConfirmDialog(
                    "Return to Main Menu?",
                    "Your best height will be preserved.",
                    () =>
                    {
                        showingQuitConfirm = false;
                        Time.timeScale = 1f;
                        SceneManager.LoadScene(SceneNames.MainMenu);
                    },
                    () => showingQuitConfirm = false
                );
        }

        private void DrawConfirmDialog(string title, string message, System.Action onConfirm, System.Action onCancel)
        {
            float dialogWidth  = 340f;
            float dialogHeight = 150f;
            float x = (Screen.width  - dialogWidth)  / 2f;
            float y = (Screen.height - dialogHeight) / 2f;

            Rect windowRect = new Rect(x, y, dialogWidth, dialogHeight);
            GUI.Box(windowRect, title);

            GUI.Label(new Rect(x + 10f, y + 30f, dialogWidth - 20f, 50f), message);

            float buttonY = y + dialogHeight - 45f;
            if (GUI.Button(new Rect(x + 20f, buttonY, 120f, 35f), "Confirm"))
                onConfirm?.Invoke();

            if (GUI.Button(new Rect(x + dialogWidth - 140f, buttonY, 120f, 35f), "Cancel"))
                onCancel?.Invoke();
        }
    }
}
