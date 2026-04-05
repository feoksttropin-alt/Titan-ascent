#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using UnityEngine;
using TitanAscent.Environment;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TitanAscent.Scene
{
    /// <summary>
    /// Runtime procedural anchor placement helper for prototyping.
    /// NOT intended for final shipped builds — wrapped in UNITY_EDITOR || DEVELOPMENT_BUILD.
    ///
    /// Scans a height range for GameObjects tagged "TitanSurface", raycasts their
    /// meshes to find flat-ish areas (normal angle &lt;60° from up), and places
    /// SurfaceAnchorPoint prefab instances at valid positions.
    /// </summary>
    public class ProceduralAnchorPlacer : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Inspector
        // ------------------------------------------------------------------

        [Header("Placement Settings")]
        [SerializeField] private float minimumSpacing      = 4f;
        [SerializeField] private float maxNormalAngle      = 60f;    // degrees from up
        [SerializeField] private bool  preferConcave       = true;
        [SerializeField] private Vector2 randomRotationRange = new Vector2(-15f, 15f);

        [Header("Scan Grid")]
        [SerializeField] private float scanGridSpacing     = 2f;     // horizontal step between ray origins
        [SerializeField] private float scanHorizontalExtent = 30f;   // ±X/Z extent of grid
        [SerializeField] private float raycastDownDistance = 5f;

        [Header("Layer Filtering")]
        [SerializeField] private LayerMask scanLayerMask   = ~0;
        [SerializeField] private string    titanSurfaceTag = "TitanSurface";

        // ------------------------------------------------------------------
        // Generated anchor tracking
        // ------------------------------------------------------------------

        private const string GeneratedTag = "generated";
        private readonly List<GameObject> _generatedAnchors = new List<GameObject>();

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>
        /// Places SurfaceAnchorPoint prefab instances across all "TitanSurface"-tagged
        /// surfaces within the specified height range.
        /// </summary>
        public void PlaceAnchorsInRange(float minHeight, float maxHeight, GameObject anchorPrefab)
        {
            if (anchorPrefab == null)
            {
                Debug.LogWarning("[ProceduralAnchorPlacer] anchorPrefab is null.");
                return;
            }

            List<Vector3> validPositions = GatherValidPositions(minHeight, maxHeight);
            List<Vector3> spacedPositions = ApplyMinimumSpacing(validPositions);

            // Determine zone index from midpoint altitude
            float midAltitude = (minHeight + maxHeight) * 0.5f;
            ZoneManager zm    = FindFirstObjectByType<ZoneManager>();
            int zoneIndex     = zm != null ? zm.CurrentZoneIndex : 0;

            foreach (Vector3 pos in spacedPositions)
            {
                GameObject instance = Instantiate(anchorPrefab, pos, Quaternion.identity);
                instance.tag = GeneratedTag;

                // Random rotation around up axis
                float yRot = Random.Range(randomRotationRange.x, randomRotationRange.y);
                instance.transform.Rotate(Vector3.up, yRot, Space.World);

                // Configure surface type based on zone
                SurfaceAnchorPoint anchor = instance.GetComponent<SurfaceAnchorPoint>();
                if (anchor != null)
                {
                    // Alternate surface types by zone — assign via SurfaceProperties override
                    SurfaceProperties sp = instance.GetComponent<SurfaceProperties>();
                    if (sp == null) sp = instance.AddComponent<SurfaceProperties>();
                }

                _generatedAnchors.Add(instance);

#if UNITY_EDITOR
                Undo.RegisterCreatedObjectUndo(instance, "Place Procedural Anchor");
#endif
            }

            Debug.Log($"[ProceduralAnchorPlacer] Placed {spacedPositions.Count} anchors " +
                      $"between {minHeight:F0}m–{maxHeight:F0}m.");
        }

        /// <summary>Destroys all GameObjects tagged "generated" created by this placer.</summary>
        public void ClearGeneratedAnchors()
        {
            for (int i = _generatedAnchors.Count - 1; i >= 0; i--)
            {
                if (_generatedAnchors[i] == null) continue;
#if UNITY_EDITOR
                Undo.DestroyObjectImmediate(_generatedAnchors[i]);
#else
                Destroy(_generatedAnchors[i]);
#endif
            }
            _generatedAnchors.Clear();

            // Also sweep the scene for any orphaned "generated"-tagged anchors
            GameObject[] orphans = GameObject.FindGameObjectsWithTag(GeneratedTag);
            foreach (GameObject o in orphans)
            {
                if (o.GetComponent<SurfaceAnchorPoint>() != null)
                {
#if UNITY_EDITOR
                    Undo.DestroyObjectImmediate(o);
#else
                    Destroy(o);
#endif
                }
            }

            Debug.Log("[ProceduralAnchorPlacer] Cleared all generated anchors.");
        }

        /// <summary>
        /// Editor-only preview — draws gizmos for potential placement positions without
        /// instantiating any objects.  Safe to call from OnDrawGizmos.
        /// </summary>
        public void PreviewPlacement(float minHeight, float maxHeight)
        {
#if UNITY_EDITOR
            List<Vector3> valid  = GatherValidPositions(minHeight, maxHeight);
            List<Vector3> spaced = ApplyMinimumSpacing(valid);

            Gizmos.color = new Color(0f, 1f, 0.5f, 0.7f);
            foreach (Vector3 pos in spaced)
            {
                Gizmos.DrawSphere(pos, 0.4f);
                Gizmos.DrawRay(pos, Vector3.up * 0.8f);
            }

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            foreach (Vector3 pos in valid)
            {
                if (!spaced.Contains(pos))
                    Gizmos.DrawSphere(pos, 0.2f);
            }
#endif
        }

        // ------------------------------------------------------------------
        // Internal helpers
        // ------------------------------------------------------------------

        private List<Vector3> GatherValidPositions(float minHeight, float maxHeight)
        {
            var result = new List<Vector3>();

            // Cast a grid of downward rays within the height band
            for (float x = -scanHorizontalExtent; x <= scanHorizontalExtent; x += scanGridSpacing)
            {
                for (float z = -scanHorizontalExtent; z <= scanHorizontalExtent; z += scanGridSpacing)
                {
                    // Sample several heights in the band
                    float heightStep = (maxHeight - minHeight) / 5f;
                    for (float h = minHeight; h <= maxHeight; h += heightStep)
                    {
                        Vector3 rayOrigin = new Vector3(
                            transform.position.x + x,
                            h + raycastDownDistance,
                            transform.position.z + z);

                        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                            raycastDownDistance * 2f, scanLayerMask))
                            continue;

                        // Tag filter
                        if (!string.IsNullOrEmpty(titanSurfaceTag) &&
                            !hit.collider.CompareTag(titanSurfaceTag))
                            continue;

                        // Normal angle check
                        float angle = Vector3.Angle(hit.normal, Vector3.up);
                        if (angle > maxNormalAngle) continue;

                        // Concavity preference (cast a secondary ray slightly offset and compare normals)
                        if (preferConcave)
                        {
                            if (!IsApproximatelyConcave(hit)) continue;
                        }

                        result.Add(hit.point);
                    }
                }
            }

            return result;
        }

        private bool IsApproximatelyConcave(RaycastHit center)
        {
            // Sample 4 neighbour normals; concave if average normal diverges outward
            Vector3[] offsets = { Vector3.right, -Vector3.right, Vector3.forward, -Vector3.forward };
            int       count   = 0;
            Vector3   sum     = Vector3.zero;

            foreach (Vector3 offset in offsets)
            {
                Vector3 origin = center.point + offset * 0.5f + Vector3.up * 0.3f;
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit nh, 1f, scanLayerMask))
                {
                    sum += nh.normal;
                    count++;
                }
            }

            if (count == 0) return true; // can't determine — accept

            Vector3 avgNormal = (sum / count).normalized;
            float   dotWithUp = Vector3.Dot(avgNormal, center.normal);

            // Concave: neighbour normals lean away from centre normal (dot < 0.95)
            return dotWithUp < 0.95f;
        }

        private List<Vector3> ApplyMinimumSpacing(List<Vector3> candidates)
        {
            var result  = new List<Vector3>();
            float sqrMin = minimumSpacing * minimumSpacing;

            foreach (Vector3 candidate in candidates)
            {
                bool tooClose = false;
                foreach (Vector3 accepted in result)
                {
                    if ((candidate - accepted).sqrMagnitude < sqrMin)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (!tooClose)
                    result.Add(candidate);
            }

            return result;
        }

        // ------------------------------------------------------------------
        // Gizmos (editor preview)
        // ------------------------------------------------------------------

        private void OnDrawGizmosSelected()
        {
#if UNITY_EDITOR
            // Draw a bounding box showing the scan extents for reference
            Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.2f);
            Gizmos.DrawWireCube(transform.position, new Vector3(
                scanHorizontalExtent * 2f, 1f, scanHorizontalExtent * 2f));
#endif
        }
    }
}

#endif
