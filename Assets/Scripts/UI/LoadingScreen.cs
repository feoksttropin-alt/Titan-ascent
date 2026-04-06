using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TitanAscent.UI
{
    /// <summary>
    /// Async loading screen singleton. Shows a progress bar, rotating tips,
    /// and an animated background while a scene loads asynchronously.
    /// Call LoadingScreen.Show("SceneName") to trigger; the screen hides
    /// itself once the scene is fully ready.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Singleton
        // ------------------------------------------------------------------

        private static LoadingScreen instance;

        // ------------------------------------------------------------------
        // Inspector
        // ------------------------------------------------------------------

        [Header("Progress")]
        [SerializeField] private Slider     progressBar;

        [Header("Tips")]
        [SerializeField] private Text       tipText;
        [SerializeField] private float      tipRotateInterval = 4f;

        [Header("Transitions")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float      fadeInDuration  = 0.3f;
        [SerializeField] private float      fadeOutDuration = 0.5f;

        [Header("Background")]
        [SerializeField] private RawImage   backgroundImage;
        [SerializeField] private float      scrollSpeed = 0.02f;

        // ------------------------------------------------------------------
        // Tips (15 entries)
        // ------------------------------------------------------------------

        private static readonly string[] Tips =
        {
            "Zone 7 has the most catastrophic fall rate of any section.",
            "The titan breathes once every 8 seconds. Time your climb.",
            "Releasing your grapple at the apex of a swing launches you further.",
            "Grip gloves are most effective on Bone Ridge surfaces.",
            "The emergency recovery window lasts longer on larger falls — use it.",
            "Daily challenges reset at midnight UTC.",
            "Your ghost is saved after every run. Race yourself.",
            "Crystal surfaces have the highest grip but the narrowest grapple zones.",
            "Wind in Zone 7 can exceed 30 m/s. Stay low.",
            "The narrator has over 100 unique lines. Some only appear rarely.",
            "Speedrun splits track each zone separately. Improve one zone at a time.",
            "Muscle skin on The Neck contracts every 8 seconds. Wait for the exhale.",
            "The crown has been reached. Not often.",
            "A catastrophic fall from Zone 9 is still survivable with emergency recovery.",
            "Your best height marker persists between runs."
        };

        // ------------------------------------------------------------------
        // Private state
        // ------------------------------------------------------------------

        private int    currentTipIndex;
        private float  tipTimer;
        private bool   isLoading;

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            SetAlpha(0f);
        }

        private void Start()
        {
            // Show a random tip immediately and start the rotation coroutine
            currentTipIndex = Random.Range(0, Tips.Length);
            ShowTip(currentTipIndex);
            StartCoroutine(RotateTipsCoroutine());
        }

        // ------------------------------------------------------------------
        // Static API
        // ------------------------------------------------------------------

        /// <summary>
        /// Shows the loading screen and begins loading the named scene.
        /// </summary>
        public static void Show(string sceneName)
        {
            if (instance == null)
            {
                // Create a default instance if not present in scene
                GameObject go = new GameObject("LoadingScreen");
                instance = go.AddComponent<LoadingScreen>();
                DontDestroyOnLoad(go);
            }

            if (!instance.isLoading)
                instance.StartCoroutine(instance.LoadRoutine(sceneName));
        }

        /// <summary>
        /// Forces the loading screen to hide (used if caller wants to control timing).
        /// </summary>
        public static void Hide()
        {
            if (instance != null && !instance.isLoading)
                instance.StartCoroutine(instance.FadeOut());
        }

        // ------------------------------------------------------------------
        // Core coroutine
        // ------------------------------------------------------------------

        private IEnumerator LoadRoutine(string sceneName)
        {
            isLoading = true;

            // Reset state
            currentTipIndex = 0;
            tipTimer        = 0f;
            ShowTip(currentTipIndex);
            SetProgress(0f);

            // Fade in
            yield return StartCoroutine(FadeIn());

            // Begin async load
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
            {
                SetProgress(op.progress / 0.9f);
                UpdateTipRotation();
                UpdateBackground();
                yield return null;
            }

            // Fill bar to 100 %
            SetProgress(1f);

            // Brief hold at 100 % so the player sees it
            float holdEnd = Time.unscaledTime + 0.5f;
            while (Time.unscaledTime < holdEnd)
            {
                UpdateTipRotation();
                UpdateBackground();
                yield return null;
            }

            // Activate the scene
            op.allowSceneActivation = true;

            // Wait one frame for scene to fully activate
            yield return null;

            // Fade out
            yield return StartCoroutine(FadeOut());

            isLoading = false;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private IEnumerator RotateTipsCoroutine()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(tipRotateInterval);
                currentTipIndex = (currentTipIndex + 1) % Tips.Length;
                ShowTip(currentTipIndex);
            }
        }

        private void UpdateTipRotation()
        {
            if (tipText == null) return;

            tipTimer += Time.unscaledDeltaTime;
            if (tipTimer >= tipRotateInterval)
            {
                tipTimer = 0f;
                currentTipIndex = (currentTipIndex + 1) % Tips.Length;
                ShowTip(currentTipIndex);
            }
        }

        private void ShowTip(int index)
        {
            if (tipText != null)
                tipText.text = Tips[Mathf.Clamp(index, 0, Tips.Length - 1)];
        }

        private void SetProgress(float t)
        {
            if (progressBar != null)
                progressBar.value = Mathf.Clamp01(t);
        }

        private void UpdateBackground()
        {
            if (backgroundImage == null) return;

            // Slow UV scroll for animated gradient background
            Vector2 offset = backgroundImage.uvRect.position;
            offset.y += scrollSpeed * Time.unscaledDeltaTime;
            backgroundImage.uvRect = new Rect(offset, backgroundImage.uvRect.size);
        }

        private IEnumerator FadeIn()
        {
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Clamp01(elapsed / fadeInDuration));
                yield return null;
            }

            SetAlpha(1f);
        }

        private IEnumerator FadeOut()
        {
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetAlpha(1f - Mathf.Clamp01(elapsed / fadeOutDuration));
                yield return null;
            }

            SetAlpha(0f);
        }

        private void SetAlpha(float alpha)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha          = alpha;
                canvasGroup.interactable   = alpha > 0.01f;
                canvasGroup.blocksRaycasts = alpha > 0.01f;
            }
        }
    }
}
