using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TitanAscent.Systems;

namespace TitanAscent.Scene
{
    /// <summary>
    /// Spawns world-space LineRenderer rings at milestone heights:
    ///   - Every 1000m          : subtle glow ring (milestoneColor)
    ///   - Zone boundaries      : zone-tinted ring (zoneColor)
    ///   - Player best height   : gold ring, syncs with HeightMarker/FallTracker
    ///   - Last 3 death heights : faint red rings, oldest removed when >3
    ///
    /// Each ring uses 64 points and animates with a gentle alpha pulse (3s cycle).
    /// </summary>
    public class CheckpointMarkerSystem : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Inspector
        // ------------------------------------------------------------------

        [Header("Ring Material")]
        [SerializeField] private Material ringMaterial;

        [Header("Colors")]
        [SerializeField] private Color deathColor       = new Color(0.9f, 0.1f, 0.1f, 0.4f);
        [SerializeField] private Color bestHeightColor  = new Color(1f,   0.84f, 0f,   0.8f);
        [SerializeField] private Color zoneColor        = new Color(0.4f, 0.8f, 1f,   0.5f);
        [SerializeField] private Color milestoneColor   = new Color(1f,   1f,   1f,   0.25f);

        // ------------------------------------------------------------------
        // Private state
        // ------------------------------------------------------------------

        private const int   RingPoints       = 64;
        private const float PulsePeriod      = 3f;
        private const int   MaxDeathMarkers  = 3;

        // Zone boundary heights (metres) — matches ZoneManager zones 1–9
        private static readonly float[] ZoneBoundaries =
        {
              0f,    // Zone 1 start (ground)
           1000f,    // Zone 2
           2000f,    // Zone 3
           3500f,    // Zone 4
           5000f,    // Zone 5
           6500f,    // Zone 6
           7500f,    // Zone 7
           8200f,    // Zone 8
           9000f,    // Zone 9
           9800f     // Summit
        };

        private static readonly float[] MilestoneHeights =
        {
            1000f, 2000f, 3000f, 4000f, 5000f, 6000f, 7000f, 8000f, 9000f
        };

        // Approximate titan body radius per zone for correct ring sizing
        private static float TitanRadiusAtHeight(float h)
        {
            // Titan is roughly 300m wide at base, tapers to 60m near crown
            float t = Mathf.Clamp01(h / 10000f);
            return Mathf.Lerp(300f, 60f, t);
        }

        // Spawned ring GameObjects
        private readonly List<(GameObject go, Material mat, Color baseColor)> rings =
            new List<(GameObject, Material, Color)>();

        // Death-marker tracking
        private readonly List<(float height, GameObject go, Material mat)> deathMarkers =
            new List<(float, GameObject, Material)>();

        // Best-height ring
        private GameObject bestHeightRing;
        private Material   bestHeightMat;

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        private void Start()
        {
            SpawnStaticRings();
            StartCoroutine(PulseCoroutine());

            // Subscribe to events
            EventBus.Subscribe<FallEndedEvent>(OnFallCompleted);
            EventBus.Subscribe<NewHeightEvent>(OnNewHeightRecord);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<FallEndedEvent>(OnFallCompleted);
            EventBus.Unsubscribe<NewHeightEvent>(OnNewHeightRecord);
        }

        // ------------------------------------------------------------------
        // Static ring spawning
        // ------------------------------------------------------------------

        private void SpawnStaticRings()
        {
            // Milestone rings every 1000m
            foreach (float h in MilestoneHeights)
                SpawnRing(h, milestoneColor, $"Ring_Milestone_{h:F0}m");

            // Zone boundary rings
            foreach (float h in ZoneBoundaries)
                SpawnRing(h, zoneColor, $"Ring_Zone_{h:F0}m");

            // Best-height ring placeholder at 0 — updated when records arrive
            bestHeightRing = CreateRingGO(0f, bestHeightColor, "Ring_BestHeight");
            bestHeightMat  = bestHeightRing.GetComponent<LineRenderer>().material;
            bestHeightRing.SetActive(false);
        }

        private void SpawnRing(float height, Color color, string label)
        {
            GameObject go  = CreateRingGO(height, color, label);
            Material   mat = go.GetComponent<LineRenderer>().material;
            rings.Add((go, mat, color));
        }

        private GameObject CreateRingGO(float height, Color color, string label)
        {
            float radius = TitanRadiusAtHeight(height);

            GameObject go = new GameObject(label);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, height, 0f);

            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = RingPoints;

            if (ringMaterial != null)
                lr.material = new Material(ringMaterial);
            else
                lr.material = new Material(Shader.Find("Sprites/Default"));

            lr.material.color = color;
            lr.startWidth     = 2f;
            lr.endWidth       = 2f;

            // Generate ring points
            Vector3[] points = new Vector3[RingPoints];
            for (int i = 0; i < RingPoints; i++)
            {
                float angle = (i / (float)RingPoints) * Mathf.PI * 2f;
                points[i] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }

            lr.SetPositions(points);
            return go;
        }

        // ------------------------------------------------------------------
        // Event handlers
        // ------------------------------------------------------------------

        private void OnFallCompleted(FallEndedEvent evt)
        {
            float deathHeight = evt.data.startHeight;
            AddDeathMarker(deathHeight);
        }

        private void OnNewHeightRecord(NewHeightEvent evt)
        {
            if (!evt.isRecord) return;

            // Move the best-height ring to the new record
            bestHeightRing.SetActive(true);
            bestHeightRing.transform.position = new Vector3(0f, evt.height, 0f);

            // Update ring radius
            float radius = TitanRadiusAtHeight(evt.height);
            LineRenderer lr = bestHeightRing.GetComponent<LineRenderer>();
            Vector3[] points = new Vector3[RingPoints];
            for (int i = 0; i < RingPoints; i++)
            {
                float angle = (i / (float)RingPoints) * Mathf.PI * 2f;
                points[i] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }
            lr.SetPositions(points);
        }

        // ------------------------------------------------------------------
        // Death markers
        // ------------------------------------------------------------------

        private void AddDeathMarker(float height)
        {
            // Remove oldest if we already have MaxDeathMarkers
            if (deathMarkers.Count >= MaxDeathMarkers)
            {
                Destroy(deathMarkers[0].go);
                deathMarkers.RemoveAt(0);
            }

            GameObject go  = CreateRingGO(height, deathColor, $"Ring_Death_{height:F0}m");
            Material   mat = go.GetComponent<LineRenderer>().material;
            deathMarkers.Add((height, go, mat));
        }

        // ------------------------------------------------------------------
        // Alpha pulse coroutine
        // ------------------------------------------------------------------

        private IEnumerator PulseCoroutine()
        {
            while (true)
            {
                float t     = Time.time;
                float pulse = (Mathf.Sin(t * (Mathf.PI * 2f / PulsePeriod)) + 1f) * 0.5f; // 0–1

                // Pulse all static rings
                foreach (var (go, mat, baseColor) in rings)
                {
                    if (mat == null) continue;
                    Color c = baseColor;
                    c.a = Mathf.Lerp(baseColor.a * 0.4f, baseColor.a, pulse);
                    mat.color = c;
                }

                // Pulse best-height ring
                if (bestHeightMat != null)
                {
                    Color c = bestHeightColor;
                    c.a = Mathf.Lerp(bestHeightColor.a * 0.5f, bestHeightColor.a, pulse);
                    bestHeightMat.color = c;
                }

                // Pulse death markers
                foreach (var (height, go, mat) in deathMarkers)
                {
                    if (mat == null) continue;
                    Color c = deathColor;
                    c.a = Mathf.Lerp(deathColor.a * 0.3f, deathColor.a, pulse);
                    mat.color = c;
                }

                yield return null;
            }
        }
    }
}
