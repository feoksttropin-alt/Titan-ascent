using System.Collections.Generic;
using UnityEngine;
using TitanAscent.Physics;
using TitanAscent.Scene;
using TitanAscent.Environment;

namespace TitanAscent.Systems
{
    /// <summary>
    /// Extends NarrationSystem with context-aware lines for triggers not
    /// handled by the base system:
    ///   - NearZoneTransition   — within 50 m of a zone boundary
    ///   - LandmarkDiscovered   — player enters a LandmarkObject's range
    ///   - SlingshotDetected    — SwingAnalyzer fires OnSlingshotDetected
    ///   - PerfectRelease       — SwingAnalyzer fires OnPerfectRelease
    ///   - StormEntered         — player enters Zone 7
    ///   - CrownApproach        — player is 9500 m+ (escalating per 100 m)
    ///   - FirstFallEver        — player's very first fall in their first run
    /// </summary>
    [RequireComponent(typeof(NarrationSystem))]
    public class NarrationExtended : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("References")]
        [SerializeField] private SwingAnalyzer swingAnalyzer;
        [SerializeField] private ZoneManager   zoneManager;
        [SerializeField] private FallTracker   fallTracker;

        [Header("Zone Boundary Proximity")]
        [Tooltip("How close (in meters) to a zone boundary before the transition line fires.")]
        [SerializeField] private float zoneBoundaryProximity = 50f;

        // ── Narration lines ───────────────────────────────────────────────────

        private static readonly string[] NearTransitionLines =
        {
            "The titan changes here.",
            "Something shifts ahead.",
            "The air is different.",
            "You can feel it changing.",
        };

        private static readonly string[] LandmarkDiscoveredLines =
        {
            // Full lore text is filled in dynamically; these are prefix lines
            "Something here holds a story.",
            "Others have marked this place.",
            "The titan remembers this too.",
        };

        private static readonly string[] SlingshotLines =
        {
            "Well used.",
            "Clean.",
            "Nice form.",
            "Good leverage.",
        };

        private static readonly string[] PerfectReleaseLines =
        {
            "Perfect.",
            "Excellent.",
            "Yes.",
        };

        private static readonly string[] StormEnteredLines =
        {
            "The storm does not sleep.",
            "Hold. Whatever you can find.",
            "The wind here remembers nothing.",
            "This is where most stop.",
            "The storm is older than your fear of it.",
        };

        // Crown approach lines keyed by height tier (9500, 9600, ..., 9900)
        private static readonly Dictionary<int, string[]> CrownApproachLines = new Dictionary<int, string[]>
        {
            {
                9500, new[]
                {
                    "The crown. It is real.",
                    "You have made it this far.",
                    "The titan breathes differently here.",
                }
            },
            {
                9600, new[]
                {
                    "500 meters. The air is almost gone.",
                    "Keep moving.",
                    "You can see the top.",
                }
            },
            {
                9700, new[]
                {
                    "300 meters.",
                    "There is no precedent for what comes next.",
                    "The crown waits.",
                }
            },
            {
                9800, new[]
                {
                    "200 meters. Almost nothing stands between you.",
                    "Don't stop.",
                    "Every grip matters now.",
                }
            },
            {
                9900, new[]
                {
                    "100 meters. You are about to do something impossible.",
                    "The titan knows.",
                    "One grip at a time.",
                }
            },
        };

        private static readonly string[] FirstFallEverLines =
        {
            "The first of many.",
            "Now you know what it feels like.",
            "The titan is still here. So are you.",
        };

        // ── Runtime state ─────────────────────────────────────────────────────

        private NarrationSystem _narration;

        private TitanZone   _lastZone;
        private bool        _stormNarratedThisRun;
        private bool        _firstFallNarrated;

        // Crown approach: track which 100-m tiers have already fired
        private readonly HashSet<int> _crownTiersFired = new HashSet<int>();

        // Zone-transition proximity: track last fired state
        private bool _wasNearBoundary;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _narration = GetComponent<NarrationSystem>();
        }

        private void Start()
        {
            // Resolve missing references
            if (swingAnalyzer == null)
                swingAnalyzer = FindFirstObjectByType<SwingAnalyzer>();
            if (zoneManager == null)
                zoneManager = FindFirstObjectByType<ZoneManager>();
            if (fallTracker == null)
                fallTracker = FindFirstObjectByType<FallTracker>();

            // Subscribe to events
            if (swingAnalyzer != null)
            {
                swingAnalyzer.OnSlingshotDetected.AddListener(OnSlingshot);
                swingAnalyzer.OnPerfectRelease.AddListener(OnPerfectRelease);
            }

            if (zoneManager != null)
                zoneManager.OnZoneChanged.AddListener(OnZoneChanged);

            if (fallTracker != null)
                fallTracker.OnFallCompleted.AddListener(OnFallCompleted);

            LandmarkObject.OnPlayerNearLandmark += OnLandmarkDiscovered;
        }

        private void OnDestroy()
        {
            if (swingAnalyzer != null)
            {
                swingAnalyzer.OnSlingshotDetected.RemoveListener(OnSlingshot);
                swingAnalyzer.OnPerfectRelease.RemoveListener(OnPerfectRelease);
            }

            if (zoneManager != null)
                zoneManager.OnZoneChanged.RemoveListener(OnZoneChanged);

            if (fallTracker != null)
                fallTracker.OnFallCompleted.RemoveListener(OnFallCompleted);

            LandmarkObject.OnPlayerNearLandmark -= OnLandmarkDiscovered;
        }

        private void Update()
        {
            TrackZoneBoundaryProximity();
            TrackCrownApproach();
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void OnSlingshot()
        {
            _narration.TryNarrate("Slingshot", SlingshotLines);
        }

        private void OnPerfectRelease(float heightGain)
        {
            _narration.TryNarrate("PerfectRelease", PerfectReleaseLines, ignoreCooldown: true);
        }

        private void OnZoneChanged(TitanZone previous, TitanZone newZone)
        {
            _lastZone = newZone;

            // Zone 7 = UpperBackStorm (index 6, 0-based)
            if (newZone != null && newZone.name == "UpperBackStorm" && !_stormNarratedThisRun)
            {
                _stormNarratedThisRun = true;
                _narration.TryNarrate("StormEntered", StormEnteredLines, ignoreCooldown: true);
            }
        }

        private void OnLandmarkDiscovered(LandmarkObject landmark)
        {
            if (landmark == null) return;

            // Show lore in shortened form via narration (truncate to first sentence)
            string lore = landmark.Lore;
            if (!string.IsNullOrEmpty(lore))
            {
                int dotIdx = lore.IndexOf('.');
                string shortLore = dotIdx >= 0 ? lore.Substring(0, dotIdx + 1) : lore;
                _narration.NarrateRaw(shortLore);
            }
            else
            {
                _narration.TryNarrate("LandmarkDiscovered", LandmarkDiscoveredLines);
            }
        }

        private void OnFallCompleted(FallData data)
        {
            if (!_firstFallNarrated && fallTracker != null && fallTracker.TotalFalls == 1)
            {
                _firstFallNarrated = true;
                _narration.TryNarrate("FirstFallEver", FirstFallEverLines, ignoreCooldown: true);
            }
        }

        // ── Per-frame tracking ────────────────────────────────────────────────

        private void TrackZoneBoundaryProximity()
        {
            if (zoneManager == null) return;

            Player.PlayerController player = FindFirstObjectByType<Player.PlayerController>();
            if (player == null) return;

            float h = player.CurrentHeight;

            // Get current and next zone; check distance to boundary
            TitanZone current = zoneManager.GetZoneForHeight(h);
            if (current == null) return;

            float distToUpper = current.maxHeight - h;
            bool isNear = distToUpper <= zoneBoundaryProximity && distToUpper > 0f;

            if (isNear && !_wasNearBoundary)
                _narration.TryNarrate("NearZoneTransition", NearTransitionLines);

            _wasNearBoundary = isNear;
        }

        private void TrackCrownApproach()
        {
            Player.PlayerController player = FindFirstObjectByType<Player.PlayerController>();
            if (player == null) return;

            float h = player.CurrentHeight;
            if (h < 9500f) return;

            // Determine which 100-m tier the player is currently in
            int tier = Mathf.FloorToInt(h / 100f) * 100; // e.g. 9500, 9600, etc.

            if (!_crownTiersFired.Contains(tier) && CrownApproachLines.TryGetValue(tier, out string[] lines))
            {
                _crownTiersFired.Add(tier);
                _narration.TryNarrate($"CrownApproach_{tier}", lines, ignoreCooldown: true);
            }
        }
    }
}
