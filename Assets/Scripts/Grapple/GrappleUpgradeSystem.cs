using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TitanAscent.Systems;

namespace TitanAscent.Grapple
{
    // -------------------------------------------------------------------------
    // Data
    // -------------------------------------------------------------------------

    [Serializable]
    public struct GrappleUpgrade
    {
        public string id;
        public string displayName;
        public string description;
        public bool   isUnlocked;
        public string unlockCondition; // Human-readable description of the condition
        public bool   isApplied;
    }

    // -------------------------------------------------------------------------
    // GrappleUpgradeSystem
    // -------------------------------------------------------------------------

    /// <summary>
    /// Grapple mastery unlocks — purely cosmetic / feel, zero gameplay advantage.
    ///
    /// Six upgrades are defined as static readonly entries.  Call CheckUnlocks()
    /// at session end, supplying the current SessionStatsTracker, to evaluate
    /// each condition.  Fires OnUpgradeUnlocked for any newly-unlocked upgrade.
    /// Call ApplyUpgrade(id) to enable the corresponding component on this
    /// GameObject.
    /// </summary>
    public class GrappleUpgradeSystem : MonoBehaviour
    {
        // ── Upgrade identifiers ───────────────────────────────────────────────

        public const string ID_QUICK_RETRACT      = "QuickRetract";
        public const string ID_GHOST_ROPE         = "GhostRope";
        public const string ID_PRECISION_CROSSHAIR = "PrecisionCrosshair";
        public const string ID_MOMENTUM_TRAILS    = "MomentumTrails";
        public const string ID_ATTACH_RINGS       = "AttachRings";
        public const string ID_ROPE_COLOR_UNLOCK  = "RopeColorUnlock";

        // ── Static upgrade definitions ────────────────────────────────────────

        public static readonly IReadOnlyList<GrappleUpgrade> UpgradeDefinitions =
            new List<GrappleUpgrade>
            {
                new GrappleUpgrade
                {
                    id               = ID_QUICK_RETRACT,
                    displayName      = "Quick Retract",
                    description      = "Faster retract animation for a snappier feel.",
                    unlockCondition  = "Complete 3 or more climbs.",
                    isUnlocked       = false,
                    isApplied        = false
                },
                new GrappleUpgrade
                {
                    id               = ID_GHOST_ROPE,
                    displayName      = "Ghost Rope",
                    description      = "The rope becomes semi-transparent when slack.",
                    unlockCondition  = "Achieve a single swing of 25 m or longer.",
                    isUnlocked       = false,
                    isApplied        = false
                },
                new GrappleUpgrade
                {
                    id               = ID_PRECISION_CROSSHAIR,
                    displayName      = "Precision Crosshair",
                    description      = "Enhanced reticle with tighter accuracy indicators.",
                    unlockCondition  = "Reach 70% grapple accuracy over 50 shots.",
                    isUnlocked       = false,
                    isApplied        = false
                },
                new GrappleUpgrade
                {
                    id               = ID_MOMENTUM_TRAILS,
                    displayName      = "Momentum Trails",
                    description      = "Brief motion trails appear after a high-speed swing.",
                    unlockCondition  = "Achieve a max swing speed of 18 m/s or more.",
                    isUnlocked       = false,
                    isApplied        = false
                },
                new GrappleUpgrade
                {
                    id               = ID_ATTACH_RINGS,
                    displayName      = "Attach Rings",
                    description      = "Concentric rings animate at the grapple attach point.",
                    unlockCondition  = "Accumulate 200 grapple attachments across all sessions.",
                    isUnlocked       = false,
                    isApplied        = false
                },
                new GrappleUpgrade
                {
                    id               = ID_ROPE_COLOR_UNLOCK,
                    displayName      = "Rope Color Unlock",
                    description      = "Unlocks custom rope color selection in the cosmetics menu.",
                    unlockCondition  = "Reach the summit at least once.",
                    isUnlocked       = false,
                    isApplied        = false
                }
            };

        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Events")]
        public UnityEvent<GrappleUpgrade> OnUpgradeUnlocked;

        [Header("Components activated per upgrade (assign in Inspector)")]
        [SerializeField] private MonoBehaviour quickRetractComponent;
        [SerializeField] private MonoBehaviour ghostRopeComponent;
        [SerializeField] private MonoBehaviour precisionCrosshairComponent;
        [SerializeField] private MonoBehaviour momentumTrailsComponent;
        [SerializeField] private MonoBehaviour attachRingsComponent;
        [SerializeField] private MonoBehaviour ropeColorUnlockComponent;

        // ── Runtime state ─────────────────────────────────────────────────────

        /// <summary>Mutable copy of upgrade states, keyed by upgrade id.</summary>
        private Dictionary<string, GrappleUpgrade> _upgrades;

        // Persistent cross-session counters (stored via PlayerPrefs)
        private const string PrefKeyTotalClimbs      = "TitanAscent_TotalClimbs";
        private const string PrefKeyTotalAttachments = "TitanAscent_TotalAttachments";
        private const string PrefKeyLongestSwing     = "TitanAscent_LongestSwing";
        private const string PrefKeyMaxSwingSpeed    = "TitanAscent_MaxSwingSpeed";
        private const string PrefKeySummitReached    = "TitanAscent_SummitReached";
        private const string PrefKeyUnlocked         = "TitanAscent_Upgrade_Unlocked_";

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _upgrades = new Dictionary<string, GrappleUpgrade>(UpgradeDefinitions.Count);
            foreach (GrappleUpgrade def in UpgradeDefinitions)
            {
                GrappleUpgrade entry = def;
                entry.isUnlocked = PlayerPrefs.GetInt(PrefKeyUnlocked + def.id, 0) == 1;
                _upgrades[def.id] = entry;
            }

            // Re-apply any previously unlocked upgrades
            foreach (KeyValuePair<string, GrappleUpgrade> kvp in _upgrades)
            {
                if (kvp.Value.isUnlocked)
                    ApplyUpgrade(kvp.Key);
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Evaluates unlock conditions using the supplied session stats and
        /// persisted cross-session counters.  Call once at session end.
        /// </summary>
        /// <param name="stats">Stats tracker for the just-completed session.</param>
        /// <param name="summitReachedThisSession">True if the player reached the summit.</param>
        public void CheckUnlocks(SessionStatsTracker stats, bool summitReachedThisSession)
        {
            if (stats == null)
            {
                Debug.LogWarning("[GrappleUpgradeSystem] CheckUnlocks called with null SessionStatsTracker.");
                return;
            }

            // Update cross-session counters
            int   totalClimbs      = PlayerPrefs.GetInt(PrefKeyTotalClimbs, 0) + 1;
            int   totalAttachments = PlayerPrefs.GetInt(PrefKeyTotalAttachments, 0) + stats.GrapplesAttached;
            float longestSwing     = Mathf.Max(PlayerPrefs.GetFloat(PrefKeyLongestSwing, 0f), stats.LongestSingleSwing);
            float maxSwingSpeed    = Mathf.Max(PlayerPrefs.GetFloat(PrefKeyMaxSwingSpeed, 0f), stats.HighestFallSpeed);
            bool  summitEverReached = PlayerPrefs.GetInt(PrefKeySummitReached, 0) == 1 || summitReachedThisSession;

            PlayerPrefs.SetInt(PrefKeyTotalClimbs, totalClimbs);
            PlayerPrefs.SetInt(PrefKeyTotalAttachments, totalAttachments);
            PlayerPrefs.SetFloat(PrefKeyLongestSwing, longestSwing);
            PlayerPrefs.SetFloat(PrefKeyMaxSwingSpeed, maxSwingSpeed);
            PlayerPrefs.SetInt(PrefKeySummitReached, summitEverReached ? 1 : 0);

            // Accuracy check (requires at least 50 shots in this session for fair evaluation)
            bool accuracyMet = stats.GrapplesFired >= 50 && stats.GetAccuracyPercent() >= 70f;

            TryUnlock(ID_QUICK_RETRACT,       totalClimbs      >= 3);
            TryUnlock(ID_GHOST_ROPE,          longestSwing     >= 25f);
            TryUnlock(ID_PRECISION_CROSSHAIR, accuracyMet);
            TryUnlock(ID_MOMENTUM_TRAILS,     maxSwingSpeed    >= 18f);
            TryUnlock(ID_ATTACH_RINGS,        totalAttachments >= 200);
            TryUnlock(ID_ROPE_COLOR_UNLOCK,   summitEverReached);

            PlayerPrefs.Save();
        }

        /// <summary>
        /// Enables the component associated with the given upgrade id.
        /// Safe to call on already-applied upgrades.
        /// </summary>
        public void ApplyUpgrade(string id)
        {
            MonoBehaviour target = ComponentForId(id);
            if (target == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[GrappleUpgradeSystem] ApplyUpgrade '{id}': no component assigned — skipping.");
#endif
                return;
            }

            target.enabled = true;

            if (_upgrades.TryGetValue(id, out GrappleUpgrade upgrade))
            {
                upgrade.isApplied = true;
                _upgrades[id] = upgrade;
            }
        }

        /// <returns>A copy of the current upgrade state for the given id, or default if unknown.</returns>
        public GrappleUpgrade GetUpgrade(string id)
        {
            return _upgrades.TryGetValue(id, out GrappleUpgrade u) ? u : default;
        }

        /// <returns>Read-only snapshot of all current upgrade states.</returns>
        public IReadOnlyDictionary<string, GrappleUpgrade> GetAllUpgrades() => _upgrades;

        // ── Helpers ───────────────────────────────────────────────────────────

        private void TryUnlock(string id, bool conditionMet)
        {
            if (!_upgrades.TryGetValue(id, out GrappleUpgrade upgrade)) return;
            if (upgrade.isUnlocked) return; // Already unlocked — nothing to do.
            if (!conditionMet) return;

            upgrade.isUnlocked = true;
            _upgrades[id] = upgrade;

            PlayerPrefs.SetInt(PrefKeyUnlocked + id, 1);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[GrappleUpgradeSystem] Upgrade unlocked: {upgrade.displayName}");
#endif

            ApplyUpgrade(id);
            OnUpgradeUnlocked?.Invoke(upgrade);
        }

        private MonoBehaviour ComponentForId(string id)
        {
            switch (id)
            {
                case ID_QUICK_RETRACT:       return quickRetractComponent;
                case ID_GHOST_ROPE:          return ghostRopeComponent;
                case ID_PRECISION_CROSSHAIR: return precisionCrosshairComponent;
                case ID_MOMENTUM_TRAILS:     return momentumTrailsComponent;
                case ID_ATTACH_RINGS:        return attachRingsComponent;
                case ID_ROPE_COLOR_UNLOCK:   return ropeColorUnlockComponent;
                default:                     return null;
            }
        }
    }
}
