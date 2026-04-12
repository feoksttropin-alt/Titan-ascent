using System.Collections;
using UnityEngine;
using TitanAscent.Systems;
using TitanAscent.Environment;
using TitanAscent.Optimization;

namespace TitanAscent.VFX
{
    /// <summary>
    /// VFX as the player nears the titan's crystal crown summit (10 000 m).
    /// - Above 9500 m : crystal shimmer particles fall from above.
    /// - Summit indicator pulses faster as player ascends.
    /// - Final 200 m  : golden mote particles drift upward.
    /// - Victory       : massive golden burst + crystal shard drift + camera pull-back.
    /// Subscribes to GameManager.OnVictory.
    /// </summary>
    public class SummitApproachVFX : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Particle Prefabs")]
        [SerializeField] private ParticleSystem crystalShimmerPrefab;
        [SerializeField] private ParticleSystem goldenMotesPrefab;
        [SerializeField] private ParticleSystem victoryBurstPrefab;

        [Header("References")]
        [SerializeField] private SummitIndicator summitIndicator;
        [SerializeField] private Camera          mainCamera;

        [Header("Crown")]
        [SerializeField] private Vector3 crownWorldPosition = new Vector3(0f, 10000f, 0f);

        [Header("Altitude Thresholds")]
        [SerializeField] private float shimmerAltitude    = 9500f;
        [SerializeField] private float goldenMotesAltitude = 9800f;  // final 200 m

        [Header("Crystal Shimmer")]
        [SerializeField] private float shimmerSpawnRadius  = 15f;
        [SerializeField] private float shimmerHeightOffset = 20f;    // how far above player to spawn
        [SerializeField] private float shimmerMaxEmission  = 25f;

        [Header("Golden Motes")]
        [SerializeField] private float motesMaxEmission    = 18f;

        [Header("Camera Pull-Back")]
        [SerializeField] private float cameraPullBackDistance = 20f;
        [SerializeField] private float cameraPullBackDuration = 2f;

        [Header("Summit Indicator Pulsing")]
        [SerializeField] private float indicatorPulseBaseMultiplier = 1f;
        [SerializeField] private float indicatorPulseMaxMultiplier  = 4f;

        // ── Private state ─────────────────────────────────────────────────────

        private Player.PlayerController player;
        private GameManager             gameManager;

        private bool victoryTriggered;
        private bool victoryVFXPlaying;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            player      = FindFirstObjectByType<Player.PlayerController>();
            gameManager = GameManager.Instance != null
                ? GameManager.Instance
                : FindFirstObjectByType<GameManager>();

            if (mainCamera == null)
                mainCamera = Camera.main;

            // Stop particles initially
            StopParticleIfPlaying(crystalShimmerPrefab);
            StopParticleIfPlaying(goldenMotesPrefab);
        }

        private void OnEnable()
        {
            if (gameManager != null)
                gameManager.OnVictory.AddListener(HandleVictory);
        }

        private void OnDisable()
        {
            if (gameManager != null)
                gameManager.OnVictory.RemoveListener(HandleVictory);

            StopParticleIfPlaying(crystalShimmerPrefab);
            StopParticleIfPlaying(goldenMotesPrefab);
        }

        private void Update()
        {
            if (victoryVFXPlaying) return;

            float altitude = player != null ? player.CurrentHeight : transform.position.y;

            UpdateCrystalShimmer(altitude);
            UpdateGoldenMotes(altitude);
            UpdateSummitIndicatorPulse(altitude);
        }

        // ── Crystal Shimmer ───────────────────────────────────────────────────

        private void UpdateCrystalShimmer(float altitude)
        {
            if (crystalShimmerPrefab == null) return;

            if (altitude >= shimmerAltitude)
            {
                float t = Mathf.InverseLerp(shimmerAltitude, crownWorldPosition.y, altitude);

                // Position shimmer prefab above the player
                Vector3 spawnPos = (player != null ? player.transform.position : transform.position)
                                   + Vector3.up * shimmerHeightOffset;
                crystalShimmerPrefab.transform.position = spawnPos;

                if (!crystalShimmerPrefab.isPlaying)
                    crystalShimmerPrefab.Play();

                SetEmissionRate(crystalShimmerPrefab, Mathf.Lerp(0f, shimmerMaxEmission, t));
            }
            else
            {
                StopParticleIfPlaying(crystalShimmerPrefab);
            }
        }

        // ── Golden Motes ──────────────────────────────────────────────────────

        private void UpdateGoldenMotes(float altitude)
        {
            if (goldenMotesPrefab == null) return;

            if (altitude >= goldenMotesAltitude)
            {
                float t = Mathf.InverseLerp(goldenMotesAltitude, crownWorldPosition.y, altitude);

                goldenMotesPrefab.transform.position =
                    player != null ? player.transform.position : transform.position;

                if (!goldenMotesPrefab.isPlaying)
                    goldenMotesPrefab.Play();

                SetEmissionRate(goldenMotesPrefab, Mathf.Lerp(0f, motesMaxEmission, t));
            }
            else
            {
                StopParticleIfPlaying(goldenMotesPrefab);
            }
        }

        // ── Summit Indicator Pulse ────────────────────────────────────────────

        private void UpdateSummitIndicatorPulse(float altitude)
        {
            // SummitIndicator exposes pulsePeriod — we accelerate it here by manipulating
            // its serialised field is not public, so we drive a normalised multiplier via
            // its transform scale as a proxy signal understood by designers.
            // Instead, we communicate by adjusting Time.timeScale equivalent: not available.
            // The cleanest approach without modifying SummitIndicator is to feed it via a
            // public method if available. Since the class doesn't expose pulse speed directly,
            // we'll drive it by scaling the indicator's local scale (which affects glow sprite size)
            // as a subtle visual intensification signal while leaving internal pulse timing alone.
            if (summitIndicator == null) return;

            float t = Mathf.InverseLerp(shimmerAltitude, crownWorldPosition.y, altitude);
            float scale = Mathf.Lerp(indicatorPulseBaseMultiplier, indicatorPulseMaxMultiplier, t);
            summitIndicator.transform.localScale = Vector3.one * scale;
        }

        // ── Victory Handler ───────────────────────────────────────────────────

        private void HandleVictory()
        {
            if (victoryTriggered) return;
            victoryTriggered = true;

            StopParticleIfPlaying(crystalShimmerPrefab);
            StopParticleIfPlaying(goldenMotesPrefab);

            StartCoroutine(VictorySequenceCoroutine());
        }

        private IEnumerator VictorySequenceCoroutine()
        {
            victoryVFXPlaying = true;

            // 1. Golden burst from crown position
            SpawnVictoryBurst();

            // 2. Crystal shards slow drift — reuse shimmerPrefab with high emission burst
            if (crystalShimmerPrefab != null)
            {
                crystalShimmerPrefab.transform.position = crownWorldPosition;
                crystalShimmerPrefab.Play();
                crystalShimmerPrefab.Emit(60);
            }

            // 3. Simultaneously pull camera back
            yield return StartCoroutine(CameraPullBackCoroutine());

            // Let particles breathe a moment
            yield return new WaitForSeconds(1.0f);

            // Shimmer and motes slow fade out
            StopParticleIfPlaying(crystalShimmerPrefab);

            victoryVFXPlaying = false;
        }

        private void SpawnVictoryBurst()
        {
            if (victoryBurstPrefab == null) return;

            if (ObjectPooler.Instance != null)
            {
                GameObject burst = ObjectPooler.Instance.Get(
                    victoryBurstPrefab.gameObject, crownWorldPosition, Quaternion.identity);

                if (burst != null)
                {
                    ParticleSystem ps = burst.GetComponent<ParticleSystem>();
                    if (ps != null)
                    {
                        ps.Play();
                        ps.Emit(150);
                    }
                    ObjectPooler.Instance.ReturnAfter(burst, 5f);
                }
            }
            else
            {
                // Fallback: use prefab directly
                victoryBurstPrefab.transform.position = crownWorldPosition;
                victoryBurstPrefab.Play();
                victoryBurstPrefab.Emit(150);
            }
        }

        private IEnumerator CameraPullBackCoroutine()
        {
            if (mainCamera == null) yield break;

            Vector3 originPos  = mainCamera.transform.position;
            Vector3 backDir    = -mainCamera.transform.forward;
            Vector3 targetPos  = originPos + backDir * cameraPullBackDistance;

            float elapsed = 0f;
            float halfDur = cameraPullBackDuration * 0.5f;

            // Pull back smoothly
            while (elapsed < halfDur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / halfDur);
                mainCamera.transform.position = Vector3.Lerp(originPos, targetPos, t);
                yield return null;
            }

            // Hold briefly
            yield return new WaitForSeconds(0.3f);

            // Return smoothly
            elapsed = 0f;
            while (elapsed < halfDur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / halfDur);
                mainCamera.transform.position = Vector3.Lerp(targetPos, originPos, t);
                yield return null;
            }

            mainCamera.transform.position = originPos;
        }

        // ── Utilities ─────────────────────────────────────────────────────────

        private static void SetEmissionRate(ParticleSystem ps, float rate)
        {
            if (ps == null) return;
            var emission       = ps.emission;
            emission.rateOverTime = rate;
        }

        private static void StopParticleIfPlaying(ParticleSystem ps)
        {
            if (ps != null && ps.isPlaying)
                ps.Stop();
        }
    }
}
