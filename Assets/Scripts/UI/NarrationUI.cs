using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace TitanAscent.UI
{
    public class NarrationUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI subtitleText;
        [SerializeField] private Image backgroundPanel;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Style")]
        [SerializeField] private float fontSize = 18f;
        [SerializeField] private float holdDuration = 3f;
        [SerializeField] private float fadeInDuration = 0.4f;
        [SerializeField] private float fadeOutDuration = 0.8f;
        [SerializeField] private Color textColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.4f);

        private Queue<string> messageQueue = new Queue<string>();
        private Coroutine displayCoroutine;
        private bool isDisplaying = false;

        private void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            if (subtitleText != null)
            {
                subtitleText.fontSize = fontSize;
                subtitleText.color = textColor;
                subtitleText.alignment = TextAlignmentOptions.Center;
            }

            if (backgroundPanel != null)
                backgroundPanel.color = panelColor;

            // Start hidden
            if (canvasGroup != null)
                canvasGroup.alpha = 0f;
        }

        public void ShowLine(string line)
        {
            messageQueue.Enqueue(line);

            if (!isDisplaying)
            {
                displayCoroutine = StartCoroutine(DisplayQueue());
            }
        }

        public void ClearQueue()
        {
            messageQueue.Clear();
            if (displayCoroutine != null)
            {
                StopCoroutine(displayCoroutine);
                displayCoroutine = null;
            }
            StartCoroutine(FadeOut());
            isDisplaying = false;
        }

        private IEnumerator DisplayQueue()
        {
            isDisplaying = true;

            while (messageQueue.Count > 0)
            {
                string line = messageQueue.Dequeue();
                yield return StartCoroutine(ShowSingleLine(line));
            }

            isDisplaying = false;
        }

        private IEnumerator ShowSingleLine(string line)
        {
            if (subtitleText != null)
                subtitleText.text = line;

            // Fade in
            yield return StartCoroutine(FadeCanvas(0f, 1f, fadeInDuration));

            // Hold
            yield return new WaitForSeconds(holdDuration);

            // Fade out
            yield return StartCoroutine(FadeCanvas(1f, 0f, fadeOutDuration));
        }

        private IEnumerator FadeCanvas(float from, float to, float duration)
        {
            if (canvasGroup == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            canvasGroup.alpha = to;
        }

        private IEnumerator FadeOut()
        {
            if (canvasGroup == null) yield break;
            yield return StartCoroutine(FadeCanvas(canvasGroup.alpha, 0f, fadeOutDuration));
        }
    }
}
