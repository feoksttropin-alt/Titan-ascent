using System.Collections;
using UnityEngine;
using TMPro;
using TitanAscent.Player;

namespace TitanAscent.UI
{
    public class RecoveryFeedback : MonoBehaviour
    {
        [Header("Text Elements")]
        [SerializeField] private TextMeshProUGUI recoveryText;
        [SerializeField] private TextMeshProUGUI comboText;

        [Header("Colors")]
        [SerializeField] private Color recoveryColor  = new Color(0.85f, 1f, 0.85f, 1f);
        [SerializeField] private Color comboColor     = new Color(1f, 0.84f, 0f, 1f);

        [Header("Combo Timing")]
        [SerializeField] private float comboWindowSeconds = 5f;

        private EmergencyRecovery emergencyRecovery;

        private Coroutine recoveryAnim;
        private Coroutine comboAnim;

        private float lastRecoveryTime = -999f;

        private void Awake()
        {
            emergencyRecovery = FindFirstObjectByType<EmergencyRecovery>();

            // Hide both texts at start
            SetTextVisible(recoveryText, false);
            SetTextVisible(comboText,    false);
        }

        private void OnEnable()
        {
            if (emergencyRecovery != null)
                emergencyRecovery.OnEmergencyUsed.AddListener(HandleEmergencyUsed);
        }

        private void OnDisable()
        {
            if (emergencyRecovery != null)
                emergencyRecovery.OnEmergencyUsed.RemoveListener(HandleEmergencyUsed);
        }

        // -----------------------------------------------------------------------
        // Event Handler
        // -----------------------------------------------------------------------

        private void HandleEmergencyUsed()
        {
            float now = Time.time;
            bool isCombo = (now - lastRecoveryTime) <= comboWindowSeconds;
            lastRecoveryTime = now;

            if (isCombo)
            {
                if (comboAnim != null) StopCoroutine(comboAnim);
                comboAnim = StartCoroutine(ShowComboText());
            }

            if (recoveryAnim != null) StopCoroutine(recoveryAnim);
            recoveryAnim = StartCoroutine(ShowRecoveryText());
        }

        // -----------------------------------------------------------------------
        // Animation Coroutines
        // -----------------------------------------------------------------------

        private IEnumerator ShowRecoveryText()
        {
            if (recoveryText == null) yield break;

            recoveryText.text  = "RECOVERY!";
            recoveryText.color = recoveryColor;
            recoveryText.gameObject.SetActive(true);

            // 1. Scale from 0 to 1.3x over 0.15s
            yield return ScaleText(recoveryText, 0f, 1.3f, 0.15f);

            // 2. Hold at 1.3x for 0.1s
            yield return new WaitForSeconds(0.1f);

            // 3. Scale back to 1.0x over 0.1s
            yield return ScaleText(recoveryText, 1.3f, 1.0f, 0.1f);

            // 4. Fade out over 0.4s
            yield return FadeOutText(recoveryText, 0.4f);

            SetTextVisible(recoveryText, false);
            recoveryAnim = null;
        }

        private IEnumerator ShowComboText()
        {
            if (comboText == null) yield break;

            comboText.text  = "DOUBLE RECOVERY!";
            comboText.color = comboColor;
            comboText.gameObject.SetActive(true);

            // Same animation sequence but slightly bigger for drama
            yield return ScaleText(comboText, 0f, 1.4f, 0.15f);
            yield return new WaitForSeconds(0.15f);
            yield return ScaleText(comboText, 1.4f, 1.0f, 0.1f);
            yield return FadeOutText(comboText, 0.5f);

            SetTextVisible(comboText, false);
            comboAnim = null;
        }

        // -----------------------------------------------------------------------
        // Animation Helpers
        // -----------------------------------------------------------------------

        private IEnumerator ScaleText(TextMeshProUGUI text, float fromScale, float toScale, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = Mathf.Lerp(fromScale, toScale, t);
                text.rectTransform.localScale = Vector3.one * scale;
                yield return null;
            }
            text.rectTransform.localScale = Vector3.one * toScale;
        }

        private IEnumerator FadeOutText(TextMeshProUGUI text, float duration)
        {
            float elapsed   = 0f;
            Color startColor = text.color;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(startColor.a, 0f, elapsed / duration);
                Color c = text.color;
                c.a = alpha;
                text.color = c;
                yield return null;
            }

            Color final = text.color;
            final.a = 0f;
            text.color = final;
        }

        private static void SetTextVisible(TextMeshProUGUI text, bool visible)
        {
            if (text != null)
                text.gameObject.SetActive(visible);
        }
    }
}
