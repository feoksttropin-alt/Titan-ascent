using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TitanAscent.Scene
{
    // -----------------------------------------------------------------------
    // Landmark Type Enum
    // -----------------------------------------------------------------------

    public enum LandmarkType
    {
        AncientWeapon,
        SkeletonOfClimber,
        RuinedStructure,
        BrokenGear,
        CrystalFormation,
        TitanScar
    }

    // -----------------------------------------------------------------------
    // LandmarkObject
    // -----------------------------------------------------------------------

    /// <summary>
    /// Component for named world landmarks embedded in the titan.
    /// Handles proximity detection, fires events when the player enters or
    /// leaves the visible range, and provides a static registry of all landmarks.
    /// </summary>
    public class LandmarkObject : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Static Registry
        // -----------------------------------------------------------------------

        /// <summary>All active <see cref="LandmarkObject"/> instances in the scene.</summary>
        public static List<LandmarkObject> AllLandmarks { get; } = new List<LandmarkObject>();

        // -----------------------------------------------------------------------
        // Inspector Fields
        // -----------------------------------------------------------------------

        [Header("Identity")]
        [SerializeField] private string landmarkName = "Unnamed Landmark";

        [Header("Lore")]
        [TextArea(3, 8)]
        [SerializeField] private string lore = "";

        [Header("Classification")]
        [SerializeField] private LandmarkType landmarkType = LandmarkType.RuinedStructure;

        [Header("Proximity")]
        [Tooltip("Radius at which this landmark appears on map/indicators and fires events.")]
        [SerializeField] private float visibleFromDistance = 50f;

        [Header("Anchor")]
        [Tooltip("Whether this landmark also acts as a grapple anchor point.")]
        [SerializeField] private bool hasAnchorPoint = false;

        // -----------------------------------------------------------------------
        // Public Properties
        // -----------------------------------------------------------------------

        public string       LandmarkName         => landmarkName;
        public string       Lore                 => lore;
        public LandmarkType Type                 => landmarkType;
        public float        VisibleFromDistance  => visibleFromDistance;
        public bool         HasAnchorPoint       => hasAnchorPoint;

        // -----------------------------------------------------------------------
        // Events
        // -----------------------------------------------------------------------

        /// <summary>Fired when the player enters <see cref="visibleFromDistance"/>.</summary>
        public static event System.Action<LandmarkObject> OnPlayerNearLandmark;

        /// <summary>Fired when the player exits <see cref="visibleFromDistance"/>.</summary>
        public static event System.Action<LandmarkObject> OnPlayerLeftLandmark;

        // -----------------------------------------------------------------------
        // Private State
        // -----------------------------------------------------------------------

        private Transform _playerTransform;
        private bool      _playerIsNear;

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            AllLandmarks.Add(this);
        }

        private void OnDestroy()
        {
            AllLandmarks.Remove(this);

            // Fire exit event if player was inside range when this object is destroyed
            if (_playerIsNear)
                OnPlayerLeftLandmark?.Invoke(this);
        }

        private void Start()
        {
            // Resolve player transform
            var playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null)
                _playerTransform = playerGO.transform;
        }

        private void Update()
        {
            if (_playerTransform == null)
            {
                var playerGO = GameObject.FindWithTag("Player");
                if (playerGO != null)
                    _playerTransform = playerGO.transform;
                return;
            }

            float sqrDist = (transform.position - _playerTransform.position).sqrMagnitude;
            float sqrRange = visibleFromDistance * visibleFromDistance;

            if (!_playerIsNear && sqrDist <= sqrRange)
            {
                _playerIsNear = true;
                OnPlayerNearLandmark?.Invoke(this);
            }
            else if (_playerIsNear && sqrDist > sqrRange)
            {
                _playerIsNear = false;
                OnPlayerLeftLandmark?.Invoke(this);
            }
        }

        // -----------------------------------------------------------------------
        // Static Query
        // -----------------------------------------------------------------------

        /// <summary>
        /// Returns the closest <see cref="LandmarkObject"/> within 50 m of
        /// <paramref name="position"/>, or null if none are within range.
        /// </summary>
        public static LandmarkObject GetNearestLandmark(Vector3 position)
        {
            const float searchRadius = 50f;
            float sqrSearch = searchRadius * searchRadius;

            LandmarkObject nearest     = null;
            float          nearestSqr  = float.MaxValue;

            foreach (var landmark in AllLandmarks)
            {
                if (landmark == null) continue;
                float sqr = (landmark.transform.position - position).sqrMagnitude;
                if (sqr <= sqrSearch && sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest    = landmark;
                }
            }

            return nearest;
        }

        // -----------------------------------------------------------------------
        // Gizmos
        // -----------------------------------------------------------------------

        private void OnDrawGizmosSelected()
        {
            // Visibility / detection radius
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, visibleFromDistance);

            // Landmark icon at position
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.8f);
            Gizmos.DrawSphere(transform.position, 0.5f);
        }

        private void OnDrawGizmos()
        {
            // Always-visible small indicator
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.25f);
            Gizmos.DrawSphere(transform.position, 0.3f);
        }
    }
}
