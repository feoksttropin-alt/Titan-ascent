using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TitanAscent.Systems;

namespace TitanAscent.UI
{
    public class TitleScreen : MonoBehaviour
    {
        [Header("Text Elements")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI subtitleText;
        [SerializeField] private TextMeshProUGUI bestHeightDisplay;
        [SerializeField] private TextMeshProUGUI totalFallsDisplay;
        [SerializeField] private TextMeshProUGUI versionText;

        [Header("Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button dailyButton;
        [SerializeField] private Button speedrunButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Panels")]
        [SerializeField] private CanvasGroup rootCanvasGroup;
        [SerializeField] private GameObject settingsPanel;

        [Header("Title Animation")]
        [SerializeField] private float titleFloatAmplitude = 4f;
        [SerializeField] private float titleFloatPeriod = 3f;
        [SerializeField] private float fadeInDuration = 1.5f;

        private GameManager gameManager;
        private ChallengeManager challengeManager;
        private SpeedrunManager speedrunManager;
        private SaveData saveData;
        private Vector3 titleTextBasePosition;

        private void Awake()
        {
            gameManager = GameManager.Instance != null
                ? GameManager.Instance
                : FindFirstObjectByType<GameManager>();

            challengeManager = FindFirstObjectByType<ChallengeManager>();
            speedrunManager  = FindFirstObjectByType<SpeedrunManager>();
        }

        private void Start()
        {
            // Capture base position before animating
            if (titleText != null)
                titleTextBasePosition = titleText.rectTransform.anchoredPosition3D;

            // Load save data
            LoadAndDisplaySave();

            // Wire buttons
            if (startButton    != null) startButton.onClick.AddListener(OnStartClicked);
            if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
            if (dailyButton    != null) dailyButton.onClick.AddListener(OnDailyClicked);
            if (speedrunButton != null) speedrunButton.onClick.AddListener(OnSpeedrunClicked);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
            if (quitButton     != null) quitButton.onClick.AddListener(OnQuitClicked);

            // Version display
            if (versionText != null)
                versionText.text = Application.version;

            // Fade in from black
            StartCoroutine(FadeIn());
        }

        private void Update()
        {
            AnimateTitleFloat();
        }

        // -----------------------------------------------------------------------
        // Save Display
        // -----------------------------------------------------------------------

        private void LoadAndDisplaySave()
        {
            if (gameManager == null)
            {
                SetSaveDisplayVisible(false);
                return;
            }

            saveData = gameManager.GlobalStats;

            bool hasSave = saveData != null && saveData.bestHeight > 0f;

            // Show/hide continue button
            if (continueButton != null)
                continueButton.gameObject.SetActive(hasSave);

            // Show/hide stat displays
            SetSaveDisplayVisible(hasSave);

            if (hasSave)
            {
                if (bestHeightDisplay != null)
                    bestHeightDisplay.text = $"Personal Best: {saveData.bestHeight:N0}m";

                if (totalFallsDisplay != null)
                    totalFallsDisplay.text = $"Total Falls: {saveData.totalFalls:N0}";
            }
        }

        private void SetSaveDisplayVisible(bool visible)
        {
            if (bestHeightDisplay != null) bestHeightDisplay.gameObject.SetActive(visible);
            if (totalFallsDisplay != null) totalFallsDisplay.gameObject.SetActive(visible);
        }

        // -----------------------------------------------------------------------
        // Animations
        // -----------------------------------------------------------------------

        private IEnumerator FadeIn()
        {
            if (rootCanvasGroup == null) yield break;

            rootCanvasGroup.alpha = 0f;
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                rootCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
                yield return null;
            }
            rootCanvasGroup.alpha = 1f;
        }

        private IEnumerator FadeOut(float duration, System.Action onComplete)
        {
            if (rootCanvasGroup == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            float elapsed = 0f;
            float startAlpha = rootCanvasGroup.alpha;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                rootCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
                yield return null;
            }
            rootCanvasGroup.alpha = 0f;
            onComplete?.Invoke();
        }

        private void AnimateTitleFloat()
        {
            if (titleText == null) return;

            float offset = Mathf.Sin(Time.time * (2f * Mathf.PI / titleFloatPeriod)) * titleFloatAmplitude;
            Vector3 pos = titleTextBasePosition;
            pos.y += offset;
            titleText.rectTransform.anchoredPosition3D = pos;
        }

        // -----------------------------------------------------------------------
        // Button Handlers
        // -----------------------------------------------------------------------

        private void OnStartClicked()
        {
            StartCoroutine(FadeOut(0.5f, () =>
            {
                gameManager?.StartClimb();
            }));
        }

        private void OnContinueClicked()
        {
            StartCoroutine(FadeOut(0.5f, () =>
            {
                PlaytestLogger.Instance?.StartSession();
                gameManager?.StartClimb();
            }));
        }

        private void OnDailyClicked()
        {
            StartCoroutine(FadeOut(0.5f, () =>
            {
                challengeManager?.StartDailyChallenge();
                gameManager?.StartClimb();
            }));
        }

        private void OnSpeedrunClicked()
        {
            StartCoroutine(FadeOut(0.5f, () =>
            {
                speedrunManager?.StartSpeedrun();
                gameManager?.StartClimb();
            }));
        }

        private void OnSettingsClicked()
        {
            if (settingsPanel != null)
                settingsPanel.SetActive(true);
        }

        private void OnQuitClicked()
        {
            Application.Quit();
        }
    }
}
