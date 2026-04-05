using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TitanAscent.UI
{
    public class HeightProgressBar : MonoBehaviour
    {
        [Header("Bar References")]
        [SerializeField] private RectTransform barRect;
        [SerializeField] private RectTransform playerDot;
        [SerializeField] private RectTransform bestHeightDot;
        [SerializeField] private Image[] zoneBands; // 9 bands, colored per zone

        [Header("Skull Icons")]
        [SerializeField] private GameObject skullPrefab;
        [SerializeField] private int maxSkulls = 3;

        [Header("Colors")]
        [SerializeField] private Color[] zoneColors = new Color[]
        {
            new Color(0.4f, 0.7f, 0.3f),   // Zone 1 – green
            new Color(0.6f, 0.7f, 0.4f),   // Zone 2
            new Color(0.7f, 0.5f, 0.3f),   // Zone 3 – orange-brown
            new Color(0.5f, 0.5f, 0.8f),   // Zone 4 – blue
            new Color(0.8f, 0.8f, 0.5f),   // Zone 5 – yellow
            new Color(0.5f, 0.3f, 0.5f),   // Zone 6 – purple
            new Color(0.6f, 0.4f, 0.2f),   // Zone 7 – dark orange
            new Color(0.3f, 0.5f, 0.7f),   // Zone 8 – deep blue
            new Color(0.9f, 0.85f, 1.0f),  // Zone 9 – white-purple crown
        };

        private static readonly float[] ZoneBoundaries = { 0f, 800f, 1800f, 3000f, 4200f, 5500f, 6500f, 7800f, 9000f, 10000f };
        private const float TitanHeight = 10000f;

        [Header("References")]
        [SerializeField] private Systems.FallTracker fallTracker;

        private List<float> worstFalls = new List<float>();
        private List<RectTransform> skullObjects = new List<RectTransform>();
        private bool pulsing = false;

        private void Start()
        {
            SetupZoneBands();
            SetupSkulls();

            if (fallTracker != null)
            {
                fallTracker.OnFallCompleted.AddListener(OnFallRecorded);
                fallTracker.OnNewHeightRecord.AddListener(_ => StartCoroutine(PulseDot(playerDot)));
            }
        }

        private void Update()
        {
            if (fallTracker == null || barRect == null) return;

            float barH = barRect.rect.height;

            if (playerDot != null)
                SetDotHeight(playerDot, fallTracker.transform.position.y, barH);

            if (bestHeightDot != null)
                SetDotHeight(bestHeightDot, fallTracker.BestHeightEver, barH);
        }

        private void SetDotHeight(RectTransform dot, float height, float barHeight)
        {
            float ratio = Mathf.Clamp01(height / TitanHeight);
            dot.anchoredPosition = new Vector2(dot.anchoredPosition.x, ratio * barHeight);
        }

        private void SetupZoneBands()
        {
            if (zoneBands == null || barRect == null) return;
            float barH = barRect.rect.height;
            for (int i = 0; i < zoneBands.Length && i < zoneColors.Length; i++)
            {
                if (zoneBands[i] == null) continue;
                float bot = ZoneBoundaries[i] / TitanHeight;
                float top = ZoneBoundaries[i + 1] / TitanHeight;
                RectTransform rt = zoneBands[i].rectTransform;
                rt.anchorMin = new Vector2(0f, bot);
                rt.anchorMax = new Vector2(1f, top);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                zoneBands[i].color = new Color(zoneColors[i].r, zoneColors[i].g, zoneColors[i].b, 0.35f);
            }
        }

        private void SetupSkulls()
        {
            if (skullPrefab == null || barRect == null) return;
            for (int i = 0; i < maxSkulls; i++)
            {
                GameObject s = Instantiate(skullPrefab, barRect);
                s.SetActive(false);
                skullObjects.Add(s.GetComponent<RectTransform>());
            }
        }

        private void OnFallRecorded(Systems.FallData data)
        {
            if (data.distance < 50f) return;
            worstFalls.Add(data.startHeight);
            worstFalls.Sort((a, b) => b.CompareTo(a)); // descending by height = more dramatic
            if (worstFalls.Count > maxSkulls) worstFalls.RemoveAt(worstFalls.Count - 1);
            UpdateSkullPositions();
        }

        private void UpdateSkullPositions()
        {
            float barH = barRect != null ? barRect.rect.height : 600f;
            for (int i = 0; i < skullObjects.Count; i++)
            {
                if (i < worstFalls.Count)
                {
                    skullObjects[i].gameObject.SetActive(true);
                    float ratio = Mathf.Clamp01(worstFalls[i] / TitanHeight);
                    skullObjects[i].anchoredPosition = new Vector2(skullObjects[i].anchoredPosition.x, ratio * barH);
                }
                else
                {
                    skullObjects[i].gameObject.SetActive(false);
                }
            }
        }

        private IEnumerator PulseDot(RectTransform dot)
        {
            if (dot == null || pulsing) yield break;
            pulsing = true;
            Vector3 original = dot.localScale;
            float dur = 0.4f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float scale = 1f + Mathf.Sin(t / dur * Mathf.PI) * 0.5f;
                dot.localScale = original * scale;
                yield return null;
            }
            dot.localScale = original;
            pulsing = false;
        }
    }
}
