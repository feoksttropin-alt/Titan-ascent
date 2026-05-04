using UnityEngine;

namespace TitanAscent.Physics
{
    /// <summary>
    /// Dedicated constraint solver for rope physics.
    /// Extracted from RopeSimulator for clarity and unit-testability.
    ///
    /// Usage
    /// ─────
    /// 1. Call Initialize(segmentCount, segmentLength) once (or when segment count changes).
    /// 2. Each FixedUpdate: set positions from outside, call SolveConstraints().
    /// 3. Read back positions via GetSegmentPositions().
    /// 4. Apply the resulting tension force to the player's Rigidbody via ApplyTensionToPlayer().
    ///
    /// The first segment is pinned to GrappleOrigin.position each solve step.
    /// The last segment's constraint displacement is converted to a force on the player Rigidbody.
    /// </summary>
    public class RopeConstraintSolver : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Constraint Settings")]
        [Tooltip("Number of constraint passes per FixedUpdate. Higher = stiffer rope.")]
        [SerializeField] private int constraintPasses = 8;

        [Tooltip("Tension force multiplier applied to the player Rigidbody.")]
        [SerializeField] private float tensionForceMultiplier = 150f;

        [Header("References")]
        [SerializeField] private Transform  grappleOrigin;
        [SerializeField] private Rigidbody  playerRigidbody;

        // ── Public Properties ─────────────────────────────────────────────────

        /// <summary>Current number of rope segments. Setting this triggers reallocation.</summary>
        public int SegmentCount
        {
            get => _segmentCount;
            set
            {
                if (value == _segmentCount) return;
                _segmentCount = Mathf.Max(2, value);
                Reallocate();
            }
        }

        // ── Internal state ─────────────────────────────────────────────────────

        private int     _segmentCount  = 20;
        private float   _segmentLength = 0.5f;

        private Vector3[] _positions;

        // Last measured tension (0–1)
        private float _tension;

        // Whether the solver has been initialized
        private bool _initialized;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (playerRigidbody == null)
            {
                Player.PlayerController pc = FindFirstObjectByType<Player.PlayerController>();
                if (pc != null)
                    playerRigidbody = pc.GetComponent<Rigidbody>();
            }

            Reallocate();
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Initializes (or re-initializes) the solver with the given segment count and length.
        /// Positions are distributed from the grapple origin downward as a straight line.
        /// </summary>
        public void Initialize(int segmentCount, float segmentLength)
        {
            _segmentCount  = Mathf.Max(2, segmentCount);
            _segmentLength = Mathf.Max(0.001f, segmentLength);
            Reallocate();
        }

        /// <summary>
        /// Performs N constraint passes on the current positions array.
        /// Call every FixedUpdate, after updating positions from physics simulation.
        /// Pins the first segment to GrappleOrigin and applies tension force to the player.
        /// </summary>
        public void SolveConstraints()
        {
            if (!_initialized || _positions == null) return;

            // Pin anchor to grapple origin
            if (grappleOrigin != null)
                _positions[0] = grappleOrigin.position;

            // Pin last segment to player position
            if (playerRigidbody != null)
                _positions[_segmentCount - 1] = playerRigidbody.position;

            Vector3 lastSegmentBefore = _positions[_segmentCount - 1];

            for (int pass = 0; pass < constraintPasses; pass++)
            {
                for (int i = 0; i < _segmentCount - 1; i++)
                {
                    Vector3 p1 = _positions[i];
                    Vector3 p2 = _positions[i + 1];

                    float currentDist = Vector3.Distance(p1, p2);
                    if (currentDist < 0.0001f) continue;

                    float error = (currentDist - _segmentLength) / currentDist;

                    // Both segments move proportionally — anchored ends are re-pinned below
                    Vector3 correction = (p2 - p1) * (error * 0.5f);

                    // First segment is pinned — only move p2
                    if (i == 0)
                        _positions[i + 1] -= correction * 2f;
                    // Last free segment: only move p1 (p2 is the player end, handled separately)
                    else if (i == _segmentCount - 2)
                        _positions[i] += correction * 2f;
                    else
                    {
                        _positions[i]     += correction;
                        _positions[i + 1] -= correction;
                    }
                }

                // Re-pin fixed endpoints after each pass
                if (grappleOrigin != null)
                    _positions[0] = grappleOrigin.position;
                if (playerRigidbody != null)
                    _positions[_segmentCount - 1] = playerRigidbody.position;
            }

            // Compute tension from how much the last segment was displaced
            Vector3 displacement = _positions[_segmentCount - 1] - lastSegmentBefore;

            // Measure stretch ratio across entire rope
            float totalLength = 0f;
            for (int i = 0; i < _segmentCount - 1; i++)
                totalLength += Vector3.Distance(_positions[i], _positions[i + 1]);

            float restLength  = _segmentLength * (_segmentCount - 1);
            float stretchRatio = totalLength / Mathf.Max(0.001f, restLength);
            _tension = Mathf.Clamp01((stretchRatio - 1f) / 0.2f);

            // Apply tension force to player
            ApplyTensionToPlayer(displacement);
        }

        /// <summary>Directly overwrite segment positions (e.g. from Verlet simulation).</summary>
        public void SetSegmentPositions(Vector3[] positions)
        {
            if (positions == null || positions.Length != _segmentCount) return;
            System.Array.Copy(positions, _positions, _segmentCount);
        }

        /// <summary>Returns a copy of the current segment positions.</summary>
        public Vector3[] GetSegmentPositions()
        {
            Vector3[] copy = new Vector3[_segmentCount];
            System.Array.Copy(_positions, copy, _segmentCount);
            return copy;
        }

        /// <summary>Returns normalized tension (0 = slack, 1 = fully taut).</summary>
        public float GetTension() => _tension;

        // ── Private helpers ───────────────────────────────────────────────────

        private void Reallocate()
        {
            _positions = new Vector3[_segmentCount];

            // Distribute segments in a straight downward line from origin
            Vector3 origin = grappleOrigin != null ? grappleOrigin.position : transform.position;
            for (int i = 0; i < _segmentCount; i++)
                _positions[i] = origin + Vector3.down * i * _segmentLength;

            _tension     = 0f;
            _initialized = true;
        }

        private void ApplyTensionToPlayer(Vector3 displacement)
        {
            if (playerRigidbody == null || displacement.sqrMagnitude < 0.0001f) return;

            // Convert displacement to force (F = m * a; use velocity-change for stability)
            Vector3 force = displacement * tensionForceMultiplier;
            playerRigidbody.AddForce(force, ForceMode.Force);
        }

        // ── Gizmos ────────────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            if (_positions == null || _positions.Length < 2) return;

            Gizmos.color = Color.Lerp(Color.white, Color.red, _tension);
            for (int i = 0; i < _positions.Length - 1; i++)
                Gizmos.DrawLine(_positions[i], _positions[i + 1]);

            // Mark anchor
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_positions[0], 0.15f);

            // Mark player end
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_positions[_positions.Length - 1], 0.15f);
        }
    }
}
