using System.Collections;
using UnityEngine;
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
        [SerializeField] private CanvasGroup runHistoryPanel;
        [SerializeField] private CanvasGroup creditsPanel;

        [Header("Credits")]
        [SerializeField] private TextMeshProUGUI creditsText;

        [Header("Tutorial Dialog")]
        [SerializeField] private GameObject tutorialPromptDialog;

        [Header("Transition")]
        [SerializeField] private float fadeDuration = 0.3f;

        // -----------------------------------------------------------------------
        // Private state
        // -----------------------------------------------------------------------

        private CanvasGroup _currentPanel;
        private Coroutine   _fadeCoroutine;
        private const float FIRST_RUN_DELAY = 0.5f;

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            // Hide all panels immediately
            SetInstant(mainPanel,      true);
            SetInstant(settingsPanel,  false);
            SetInstant(cosmeticsPanel, false);
            SetInstant(runHistoryPanel,false);
            SetInstant(creditsPanel,   false);

            _currentPanel = mainPanel;

            if (tutorialPromptDialog != null)
                tutorialPromptDialog.SetActive(false);
        }

        private void Start()
        {
            PopulateCredits();

            // First-time flow: show tutorial prompt after short delay
            SaveManager sm = FindFirstObjectByType<SaveManager>();
            if (sm != null && sm.CurrentData.totalClimbs == 0)
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

        public void ShowMainPanel()     => TransitionTo(mainPanel);
        public void ShowSettings()      => TransitionTo(settingsPanel);
        public void ShowCosmetics()     => TransitionTo(cosmeticsPanel);
        public void ShowRunHistory()    => TransitionTo(runHistoryPanel);
        public void ShowCredits()       => TransitionTo(creditsPanel);

        public void NavigateBack()
        {
            if (_currentPanel != mainPanel)
                TransitionTo(mainPanel);
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
