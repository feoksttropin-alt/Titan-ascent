using System;
using System.Collections.Generic;
using UnityEngine;
using TitanAscent.Grapple;

namespace TitanAscent.Systems
{
    // -----------------------------------------------------------------------
    // Player state enum for ghost frames
    // -----------------------------------------------------------------------

    public enum GhostPlayerState
    {
        Grounded,
        Airborne,
        Swinging,
        Sliding,
        Falling
    }

    // -----------------------------------------------------------------------
    // Extended GhostFrame with player state
    // (GhostSystem.GhostFrame already exists; we extend via composition)
    // -----------------------------------------------------------------------

    [Serializable]
    public class RecorderFrame
    {
        public float           time;
        public Vector3         position;
        public Quaternion      rotation;
        public bool            grappleActive;
        public Vector3         grappleAnchor;
        public GhostPlayerState playerState;
    }

    // -----------------------------------------------------------------------
    // GhostRecorder — lightweight component-level recorder
    // -----------------------------------------------------------------------

    /// <summary>
    /// Lightweight per-run ghost recorder attached to the player.
    /// Samples at a configurable rate (default 20 fps).
    /// On StopRecording(), automatically hands the data to GhostSystem.SaveGhost().
    /// Works alongside GhostSystem.cs (which handles replay/persistence).
    /// </summary>
    public class GhostRecorder : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [Header("Recording")]
        [SerializeField] private float recordFps = 20f;  // samples per second

        [Header("References (auto-found if null)")]
        [SerializeField] private GrappleController grappleController;
        [SerializeField] private GhostSystem       ghostSystem;

        // -----------------------------------------------------------------------
        // Constants
        // -----------------------------------------------------------------------

        private const int MaxFrames = 72000;  // 1 h at 20 fps

        // -----------------------------------------------------------------------
        // State
        // -----------------------------------------------------------------------

        private bool                _isRecording  = false;
        private float               _timer        = 0f;
        private float               _interval;
        private List<RecorderFrame> _frames       = new List<RecorderFrame>();
        private Rigidbody           _rb;

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        public bool IsRecording => _isRecording;

        /// <summary>Returns a copy of the recorded frames after stopping.</summary>
        public List<RecorderFrame> GetRecording() => new List<RecorderFrame>(_frames);

        public void StartRecording()
        {
            _frames.Clear();
            _timer       = 0f;
            _interval    = recordFps > 0f ? 1f / recordFps : 0.05f;
            _isRecording = true;
            Debug.Log("[GhostRecorder] Recording started.");
        }

        public void StopRecording()
        {
            if (!_isRecording) return;
            _isRecording = false;
            Debug.Log($"[GhostRecorder] Recording stopped. Frames: {_frames.Count}");

            // Hand off to GhostSystem for persistence
            if (ghostSystem != null && _frames.Count > 0)
                ghostSystem.SaveGhost("run");
        }

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            if (grappleController == null)
                grappleController = GetComponentInChildren<GrappleController>()
                    ?? FindFirstObjectByType<GrappleController>();

            if (ghostSystem == null)
                ghostSystem = FindFirstObjectByType<GhostSystem>();

            _interval = recordFps > 0f ? 1f / recordFps : 0.05f;
        }

        private void Update()
        {
            if (!_isRecording) return;

            _timer += Time.deltaTime;
            if (_timer < _interval) return;
            _timer -= _interval;

            CaptureFrame();
        }

        // -----------------------------------------------------------------------
        // Frame capture
        // -----------------------------------------------------------------------

        private void CaptureFrame()
        {
            if (_frames.Count >= MaxFrames)
            {
                Debug.LogWarning("[GhostRecorder] Max frame cap (72 000) reached. Recording stopped.");
                StopRecording();
                return;
            }

            bool    grappleActive = false;
            Vector3 grappleAnchor = Vector3.zero;

            if (grappleController != null && grappleController.IsAttached)
            {
                grappleActive = true;
                grappleAnchor = grappleController.AttachPoint;
            }

            GhostPlayerState state = DeterminePlayerState(grappleActive);

            _frames.Add(new RecorderFrame
            {
                time          = Time.time,
                position      = transform.position,
                rotation      = transform.rotation,
                grappleActive = grappleActive,
                grappleAnchor = grappleAnchor,
                playerState   = state
            });
        }

        private GhostPlayerState DeterminePlayerState(bool grappleActive)
        {
            if (grappleActive)
                return GhostPlayerState.Swinging;

            if (_rb == null)
                return GhostPlayerState.Airborne;

            float vy = _rb.linearVelocity.y;

            // Simple heuristic based on vertical velocity
            if (vy < -5f)
                return GhostPlayerState.Falling;

            // Ground check via velocity — if very slow horizontally and vertical ~0
            if (Mathf.Abs(vy) < 0.5f && _rb.linearVelocity.magnitude < 1f)
                return GhostPlayerState.Grounded;

            return GhostPlayerState.Airborne;
        }
    }
}
