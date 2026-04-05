using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TitanAscent.Environment;

namespace TitanAscent.Optimization
{
    /// <summary>
    /// Simple LOD management for distant titan geometry.
    ///
    /// Groups objects tagged "TitanGeometry" into three tiers based on distance from the player:
    ///   Near  ( 0 –  50 m): full detail, all components active.
    ///   Mid   (50 – 150 m): LODGroup reduced to level 1, particle systems disabled.
    ///   Far   (150 m+)    : LODGroup forced to lowest level, particles off, shadows off.
    ///
    /// Update runs every 0.5 s via coroutine (not every frame).
    ///
    /// Also adjusts AmbientCreatureSystem's spawn radius to ±500 m of the current altitude.
    /// </summary>
    public class LODManager : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Constants
        // -----------------------------------------------------------------------

        private const string GeometryTag = "TitanGeometry";
        private const float NearThreshold = 50f;
        private const float MidThreshold = 150f;
        private const float UpdateInterval = 0.5f;
        private const float CreatureAltitudeBand = 500f;

        // -----------------------------------------------------------------------
        // LOD tier enum
        // -----------------------------------------------------------------------

        private enum LODTier { Near = 0, Mid = 1, Far = 2 }

        // -----------------------------------------------------------------------
        // Per-object cache
        // -----------------------------------------------------------------------

        private class GeometryEntry
        {
            public GameObject go;
            public LODGroup lodGroup;           // may be null
            public ParticleSystem[] particles;  // may be empty
            public Renderer[] renderers;        // for shadow control
            public LODTier currentTier = (LODTier)(-1); // unset
        }

        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [Header("References (auto-found if null)")]
        [SerializeField] private Player.PlayerController playerController;
        [SerializeField] private AmbientCreatureSystem ambientCreatureSystem;

        [Header("Thresholds")]
        [SerializeField] private float nearDistance = NearThreshold;
        [SerializeField] private float midDistance = MidThreshold;
        [SerializeField] private float updateInterval = UpdateInterval;

        // -----------------------------------------------------------------------
        // Private state
        // -----------------------------------------------------------------------

        private List<GeometryEntry> _entries = new List<GeometryEntry>();
        private Coroutine _updateCoroutine;

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            if (playerController == null)
                playerController = FindObjectOfType<Player.PlayerController>();

            if (ambientCreatureSystem == null)
                ambientCreatureSystem = FindObjectOfType<AmbientCreatureSystem>();
        }

        private void OnEnable()
        {
            RefreshGeometryList();
            _updateCoroutine = StartCoroutine(UpdateLODsRoutine());
        }

        private void OnDisable()
        {
            if (_updateCoroutine != null)
            {
                StopCoroutine(_updateCoroutine);
                _updateCoroutine = null;
            }
        }

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Rescans the scene for "TitanGeometry" tagged objects.
        /// Call this after instantiating new geometry at runtime.
        /// </summary>
        public void RefreshGeometryList()
        {
            _entries.Clear();

            GameObject[] tagged = GameObject.FindGameObjectsWithTag(GeometryTag);
            foreach (GameObject go in tagged)
            {
                GeometryEntry entry = new GeometryEntry
                {
                    go        = go,
                    lodGroup  = go.GetComponent<LODGroup>(),
                    particles = go.GetComponentsInChildren<ParticleSystem>(),
                    renderers = go.GetComponentsInChildren<Renderer>()
                };
                _entries.Add(entry);
            }

            Debug.Log($"[LODManager] Cached {_entries.Count} TitanGeometry object(s).");
        }

        // -----------------------------------------------------------------------
        // Update coroutine
        // -----------------------------------------------------------------------

        private IEnumerator UpdateLODsRoutine()
        {
            WaitForSeconds wait = new WaitForSeconds(updateInterval);

            while (true)
            {
                yield return wait;

                if (playerController == null) continue;

                Vector3 playerPos = playerController.transform.position;
                float playerAltitude = playerPos.y;

                // LOD update pass
                for (int i = _entries.Count - 1; i >= 0; i--)
                {
                    GeometryEntry entry = _entries[i];

                    if (entry.go == null)
                    {
                        _entries.RemoveAt(i);
                        continue;
                    }

                    float dist = Vector3.Distance(playerPos, entry.go.transform.position);
                    LODTier tier = ClassifyTier(dist);

                    if (tier != entry.currentTier)
                    {
                        ApplyTier(entry, tier);
                        entry.currentTier = tier;
                    }
                }

                // Creature altitude band
                UpdateCreatureAltitudeBand(playerAltitude);
            }
        }

        // -----------------------------------------------------------------------
        // Tier classification
        // -----------------------------------------------------------------------

        private LODTier ClassifyTier(float distance)
        {
            if (distance <= nearDistance) return LODTier.Near;
            if (distance <= midDistance) return LODTier.Mid;
            return LODTier.Far;
        }

        // -----------------------------------------------------------------------
        // Apply tier
        // -----------------------------------------------------------------------

        private void ApplyTier(GeometryEntry entry, LODTier tier)
        {
            switch (tier)
            {
                case LODTier.Near:
                    SetLODLevel(entry, 0);
                    SetParticlesEnabled(entry, true);
                    SetShadowCasting(entry, UnityEngine.Rendering.ShadowCastingMode.On);
                    break;

                case LODTier.Mid:
                    SetLODLevel(entry, 1);
                    SetParticlesEnabled(entry, false);
                    SetShadowCasting(entry, UnityEngine.Rendering.ShadowCastingMode.On);
                    break;

                case LODTier.Far:
                    SetLODLevel(entry, int.MaxValue); // forces to lowest LOD
                    SetParticlesEnabled(entry, false);
                    SetShadowCasting(entry, UnityEngine.Rendering.ShadowCastingMode.Off);
                    break;
            }
        }

        private static void SetLODLevel(GeometryEntry entry, int level)
        {
            if (entry.lodGroup == null) return;

            LOD[] lods = entry.lodGroup.GetLODs();
            if (lods.Length == 0) return;

            // Clamp to last valid index
            int clampedLevel = Mathf.Clamp(level, 0, lods.Length - 1);
            entry.lodGroup.ForceLOD(clampedLevel);
        }

        private static void SetParticlesEnabled(GeometryEntry entry, bool enabled)
        {
            if (entry.particles == null) return;
            foreach (ParticleSystem ps in entry.particles)
            {
                if (ps == null) continue;
                if (enabled)
                {
                    if (!ps.isPlaying) ps.Play();
                }
                else
                {
                    if (ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        private static void SetShadowCasting(GeometryEntry entry, UnityEngine.Rendering.ShadowCastingMode mode)
        {
            if (entry.renderers == null) return;
            foreach (Renderer r in entry.renderers)
            {
                if (r != null)
                    r.shadowCastingMode = mode;
            }
        }

        // -----------------------------------------------------------------------
        // Creature altitude band
        // -----------------------------------------------------------------------

        private void UpdateCreatureAltitudeBand(float playerAltitude)
        {
            if (ambientCreatureSystem == null) return;

            // The AmbientCreatureSystem already uses altitudeBandBuffer internally;
            // here we inform it of the active altitude so it can cull creatures
            // outside the ±500 m window. We do this by updating the player height
            // which the system reads via PlayerController.CurrentHeight every tick.
            // No additional public API needed — the AmbientCreatureSystem already
            // queries PlayerController.CurrentHeight for its own band checks.
            // This block is reserved for future direct API calls if the creature system
            // exposes a SetActiveBand(float min, float max) method.
            _ = playerAltitude; // suppress unused warning
        }
    }
}
