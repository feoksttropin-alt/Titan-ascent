using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace TitanAscent.UI
{
    public class HUDController : MonoBehaviour
    {
        [Header("Height Display")]
        [SerializeField] private TextMeshProUGUI currentHeightText;
        [SerializeField] private TextMeshProUGUI bestHeightText;
        [SerializeField] private float heightAnimationSpeed = 5f;

        [Header("Fall Statistics")]
        [SerializeField] private TextMeshProUGUI fallCountText;
        [SerializeField] private TextMeshProUGUI longestFallText;

        [Header("Timer")]
        [SerializeField] private TextMeshProUGUI runTimeText;

        [Header("Energy Bar")]
        [SerializeField] private Slider energyBar;
        [SerializeField] private Image energyBarFill;
        [SerializeField] private Color energyFullColor   = new Color(0.2f, 0.8f, 1f, 0.85f);
        [SerializeField] private Color energyLowColor    = new Color(1f, 0.3f, 0.1f, 0.85f);
        [SerializeField] private float lowEnergyThreshold = 0.25f;

        [Header("Flash Effects")]
        [SerializeField] private Image fallFlashOverlay;
        [SerializeField] private float fallFlashDuration = 0.4f;
        [SerializeField] private Color fallFlashColor = new Color(1f, 0.1f, 0.1f, 0.3f);

        [Header("Best Height Pulse")]
        [SerializeField] private float bestHeightPulseDuration = 0.6f;
        [SerializeField] private Color bestHeightPulseColor = new Color(1f, 0.9f, 0.2f);

        [Header("Progress Bar")]
        [SerializeField] private HeightProgressBar heightProgressBar;

        private Systems.GameManager gameManager;
        private Player.ThrusterSystem thrusterSystem;
        private Systems.FallTracker fallTracker;

        // Best-height pulse font sizes
        private const float BestHeightFontSizeNormal = 18f;
        private const float BestHeightFontSizePeak   = 22f;

        private float displayedHeight;
        private float displayedBestHeight;
        private Color defaultBestHeightColor;
        private Coroutine flashCoroutine;
        private Coroutine bestHeightPulseCoroutine;

        // Timer display cache — only rebuild the string when the value changes
        private int _lastTimerMinutes = -1;
        private int _lastTimerSeconds = -1;

        private void Awake()
        {
            gameManager      = Systems.GameManager.Instance ?? FindFirstObjectByType<Systems.GameManager>();
            thrusterSystem   = FindFirstObjectByType<Player.ThrusterSystem>();
            fallTracker      = FindFirstObjectByType<Systems.FallTracker>();

            if (heightProgressBar == null)
                heightProgressBar = FindFirstObjectByType<HeightProgressBar>();

            if (bestHeightText != null)
                defaultBestHeightColor = bestHeightText.color;

            // Start with overlay invisible
            if (fallFlashOverlay != null)
            {
                Color c = fallFlashColor;
                c.a = 0f;
                fallFlashOverlay.color = c;
            }
        }

        private void OnEnable()
        {
            if (gameManager != null)
            {
                gameManager.OnNewHeightRecord.AddListener(HandleNewRecord);
            }
            if (fallTracker != null)
            {
                fallTracker.OnFallCompleted.AddListener(HandleFallCompleted);
            }
        }

        private void OnDisable()
        {
            if (gameManager != null)
                gameManager.OnNewHeightRecord.RemoveListener(HandleNewRecord);
            if (fallTracker != null)
                fallTracker.OnFallCompleted.RemoveListener(HandleFallCompleted);
        }

        private void Update()
        {
            UpdateHeightDisplay();
            UpdateEnergyBar();
            UpdateFallStats();
            UpdateTimer();
        }

        private void UpdateHeightDisplay()
        {
            if (gameManager == null) return;

            float targetHeight = gameManager.CurrentHeight;
            float targetBest   = gameManager.BestHeightEver;

            // Animate height number
            displayedHeight = Mathf.Lerp(displayedHeight, targetHeight, Time.deltaTime * heightAnimationSpeed);
            displayedBestHeight = Mathf.Lerp(displayedBestHeight, targetBest, Time.deltaTime * heightAnimationSpeed);

            if (currentHeightText != null)
                currentHeightText.text = FormatHeight(displayedHeight);

            if (bestHeightText != null)
                bestHeightText.text = "BEST: " + FormatHeight(displayedBestHeight);
        }

        private void UpdateEnergyBar()
        {
            if (thrusterSystem == null || energyBar == null) return;

            float energy = thrusterSystem.EnergyPercent;
            energyBar.value = Mathf.Lerp(energyBar.value, energy, Time.deltaTime * 10f);

            if (energyBarFill != null)
            {
                Color targetColor = energy < lowEnergyThreshold ? energyLowColor : energyFullColor;
                energyBarFill.color = Color.Lerp(energyBarFill.color, targetColor, Time.deltaTime * 5f);
            }
        }

        private void UpdateFallStats()
        {
            if (gameManager == null) return;

            if (fallCountText != null)
                fallCountText.text = "FALLS: " + gameManager.TotalFalls.ToString();

            if (longestFallText != null)
                longestFallText.text = "LONGEST: " + FormatHeight(gameManager.LongestFall);
        }

        private void UpdateTimer()
        {
            if (gameManager == null || runTimeText == null) return;

            float elapsed  = gameManager.SessionTime;
            int minutes    = Mathf.FloorToInt(elapsed / 60f);
            int secs       = Mathf.FloorToInt(elapsed % 60f);

            // Only rebuild the string when the displayed value changes (once per second)
            if (minutes != _lastTimerMinutes || secs != _lastTimerSeconds)
            {
                _lastTimerMinutes  = minutes;
                _lastTimerSeconds  = secs;
                runTimeText.text   = $"{minutes:00}:{secs:00}";
            }
        }

        private void HandleFallCompleted(Systems.FallData data)
        {
            if (fallFlashCoroutine != null) StopCoroutine(fallFlashCoroutine);
            flashCoroutine = StartCoroutine(FlashFallOverlay());
        }

        private void HandleNewRecord(float height)
        {
            if (bestHeightPulseCoroutine != null) StopCoroutine(bestHeightPulseCoroutine);
            bestHeightPulseCoroutine = StartCoroutine(PulseBestHeightText());
        }

        private IEnumerator FlashFallOverlay()
        {
            if (fallFlashOverlay == null) yield break;

            // Fade in
            float t = 0f;
            while (t < fallFlashDuration * 0.3f)
            {
                float alpha = t / (fallFlashDuration * 0.3f) * fallFlashColor.a;
                Color c = fallFlashColor;
                c.a = alpha;
                fallFlashOverlay.color = c;
                t += Time.deltaTime;
                yield return null;
            }

            // Fade out
            t = 0f;
            while (t < fallFlashDuration * 0.7f)
            {
                float alpha = (1f - t / (fallFlashDuration * 0.7f)) * fallFlashColor.a;
                Color c = fallFlashColor;
                c.a = alpha;
                fallFlashOverlay.color = c;
                t += Time.deltaTime;
                yield return null;
            }

            Color clear = fallFlashColor;
            clear.a = 0f;
            fallFlashOverlay.color = clear;
        }

        private IEnumerator PulseBestHeightText()
        {
            if (bestHeightText == null) yield break;

            float elapsed = 0f;
            while (elapsed < bestHeightPulseDuration)
            {
                float t = elapsed / bestHeightPulseDuration;
                float pulse = Mathf.Sin(t * Mathf.PI);
                bestHeightText.color    = Color.Lerp(defaultBestHeightColor, bestHeightPulseColor, pulse);
                bestHeightText.fontSize = Mathf.Lerp(BestHeightFontSizeNormal, BestHeightFontSizePeak, pulse);
                elapsed += Time.deltaTime;
                yield return null;
            }

            bestHeightText.color    = defaultBestHeightColor;
            bestHeightText.fontSize = BestHeightFontSizeNormal;
        }

        private string FormatHeight(float height)
        {
            return Mathf.RoundToInt(height).ToString("N0") + "m";
        }
    }
}
