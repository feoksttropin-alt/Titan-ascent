using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TitanAscent.UI;

namespace TitanAscent.Systems
{
    /// <summary>
    /// Handles all scene transitions with a black canvas-group fade.
    /// All public methods are safe to call from UI buttons.
    /// </summary>
    public class SceneTransitionManager : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Constants
        // -----------------------------------------------------------------------

        public const string MAIN_MENU = "MainMenu";
        public const string GAMEPLAY  = "Gameplay";

        private const float FADE_DURATION = 0.3f;

        // -----------------------------------------------------------------------
        // Singleton
        // -----------------------------------------------------------------------

        public static SceneTransitionManager Instance { get; private set; }

        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [Header("Fade Overlay")]
        [SerializeField] private CanvasGroup fadeOverlay;

        // -----------------------------------------------------------------------
        // Private state
        // -----------------------------------------------------------------------

        private bool _isTransitioning;

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
            DontDestroyOnLoad(gameObject);

            EnsureFadeOverlay();
        }

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>Fades to black, loads the Gameplay scene, fades in.</summary>
        public void LoadGameplay()
        {
            if (_isTransitioning) return;
            StartCoroutine(TransitionTo(GAMEPLAY, incrementClimbs: false));
        }

        /// <summary>Fades to black, loads the MainMenu scene, fades in.</summary>
        public void LoadMainMenu()
        {
            if (_isTransitioning) return;
            StartCoroutine(TransitionTo(MAIN_MENU, incrementClimbs: false));
        }

        /// <summary>
        /// Same as LoadGameplay but also increments SaveManager.totalClimbs
        /// before the transition begins.
        /// </summary>
        public void ReloadGameplay()
        {
            if (_isTransitioning) return;
            StartCoroutine(TransitionTo(GAMEPLAY, incrementClimbs: true));
        }

        /// <summary>Saves and quits the application.</summary>
        public void QuitGame()
        {
            if (_isTransitioning) return;
            _isTransitioning = true;
            StartCoroutine(QuitRoutine());
        }

        // -----------------------------------------------------------------------
        // Coroutines
        // -----------------------------------------------------------------------

        private IEnumerator TransitionTo(string sceneName, bool incrementClimbs)
        {
            _isTransitioning = true;

            if (incrementClimbs)
            {
                SaveManager sm = FindFirstObjectByType<SaveManager>();
                if (sm != null)
                {
                    sm.CurrentData.totalClimbs++;
                    sm.Save();
                }
            }

            // Fade to black
            yield return StartCoroutine(Fade(0f, 1f, FADE_DURATION));

            // Async scene load via LoadingScreen
            LoadingScreen.Show(sceneName);

            // Wait for scene to be loaded (LoadingScreen drives the async op)
            // We wait until the active scene changes to the target
            float timeout = 30f;
            float elapsed = 0f;
            while (SceneManager.GetActiveScene().name != sceneName && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // Fade from black
            yield return StartCoroutine(Fade(1f, 0f, FADE_DURATION));

            _isTransitioning = false;
        }

        private IEnumerator QuitRoutine()
        {
            SaveManager sm = FindFirstObjectByType<SaveManager>();
            sm?.Save();

            yield return StartCoroutine(Fade(0f, 1f, FADE_DURATION));

            Application.Quit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            EnsureFadeOverlay();
            if (fadeOverlay == null) yield break;

            fadeOverlay.gameObject.SetActive(true);
            fadeOverlay.blocksRaycasts = true;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                fadeOverlay.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }

            fadeOverlay.alpha = to;
            if (to <= 0f)
            {
                fadeOverlay.blocksRaycasts = false;
                fadeOverlay.gameObject.SetActive(false);
            }
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private void EnsureFadeOverlay()
        {
            if (fadeOverlay != null) return;

            // Build a simple full-screen black panel at runtime if not assigned
            GameObject canvasGO = new GameObject("TransitionFadeCanvas");
            canvasGO.transform.SetParent(transform, false);
            DontDestroyOnLoad(canvasGO);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9998;

            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            GameObject panelGO = new GameObject("FadePanel");
            panelGO.transform.SetParent(canvasGO.transform, false);

            UnityEngine.UI.Image img = panelGO.AddComponent<UnityEngine.UI.Image>();
            img.color = Color.black;

            RectTransform rt = panelGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            fadeOverlay = panelGO.AddComponent<CanvasGroup>();
            fadeOverlay.alpha = 0f;
            fadeOverlay.blocksRaycasts = false;
            panelGO.SetActive(false);
        }
    }
}
