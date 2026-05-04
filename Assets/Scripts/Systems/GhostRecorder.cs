using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    /// Lightweight per-run ghost recorder.
    /// Subscribes to GameManager.OnClimbStarted (OnEnable) to start recording,
    /// and to GameManager.OnVictory / FallTracker.OnFallCompleted (RunEnding)
    /// to stop recording and flush a binary ghost file.
    ///
    /// Binary save format per frame:
    ///   float    timestamp
    ///   Vector3  position  (3 × float)
    ///   Quaternion rotation (4 × float)
    ///   bool     isGrappling
    ///   Vector3  grapplePoint (3 × float, written even when not grappling)
    ///
    /// Save path: Application.persistentDataPath + "/ghost_last.dat"
    /// </summary>
    public class GhostRecorder : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [Header("Recording")]
        [SerializeField] private float recordFps = 20f;  // samples per second

        [Header("References (auto-found if null)")]
        [SerializeField] private GrappleController       grappleController;
        [SerializeField] private Player.PlayerController playerController;
        [SerializeField] private FallTracker             fallTracker;

        // -----------------------------------------------------------------------
        // Constants
        // -----------------------------------------------------------------------

        private const int MaxFrames = 72000;  // 1 h at 20 fps

        // -----------------------------------------------------------------------
        // State
        // -----------------------------------------------------------------------

        private bool                _isRecording     = false;
        private bool                _gmBound         = false;
        private List<RecorderFrame> _frames          = new List<RecorderFrame>();
        private Rigidbody           _rb;
        private float               _interval;
        private float               _timer;

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
            _isRecording = true;
            Debug.Log("[GhostRecorder] Recording started.");
        }

        public void StopRecording()
        {
            if (!_isRecording) return;
            _isRecording = false;

            Debug.Log($"[GhostRecorder] Recording stopped. Frames: {_frames.Count}");

            if (_frames.Count > 0)
                SaveBinary(_frames, GhostSystem.LastGhostPath);
        }

        public void StopAndSave()
        {
            if (_isRecording)
            {
                _isRecording = false;
                Debug.Log($"[GhostRecorder] StopAndSave called. Frames: {_frames.Count}");
            }

            if (_frames.Count > 0)
                SaveBinary(_frames, GhostSystem.LastGhostPath);
        }

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            if (playerController == null)
                playerController = FindFirstObjectByType<Player.PlayerController>();

            if (grappleController == null)
            {
                grappleController = (playerController != null)
                    ? playerController.GetComponentInChildren<GrappleController>()
                    : null;

                if (grappleController == null)
                    grappleController = FindFirstObjectByType<GrappleController>();
            }

            if (fallTracker == null)
                fallTracker = FindFirstObjectByType<FallTracker>();

            _interval = recordFps > 0f ? 1f / recordFps : 0.05f;
        }

        private void OnEnable()
        {
            if (GameManager.Instance != null && !_gmBound)
            {
                GameManager.Instance.OnClimbStarted.AddListener(OnClimbStarted);
                GameManager.Instance.OnVictory.AddListener(OnVictory);
                _gmBound = true;
            }
            else if (GameManager.Instance == null)
            {
                Debug.LogWarning("[GhostRecorder] GameManager.Instance not available in OnEnable; will retry in Start.");
            }

            BindFallTracker();
        }

        private void Start()
        {
            // Bind GameManager events only if OnEnable couldn't (e.g. singleton not yet alive)
            if (!_gmBound && GameManager.Instance != null)
            {
                GameManager.Instance.OnClimbStarted.AddListener(OnClimbStarted);
                GameManager.Instance.OnVictory.AddListener(OnVictory);
                _gmBound = true;
            }

            if (fallTracker == null)
                fallTracker = FindFirstObjectByType<FallTracker>();

            BindFallTracker();
        }

        private void OnDisable()
        {
            if (_gmBound && GameManager.Instance != null)
            {
                GameManager.Instance.OnClimbStarted.RemoveListener(OnClimbStarted);
                GameManager.Instance.OnVictory.RemoveListener(OnVictory);
            }
            _gmBound = false;

            UnbindFallTracker();
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
        // Event handlers
        // -----------------------------------------------------------------------

        private void OnClimbStarted()
        {
            StartRecording();
        }

        private void OnVictory()
        {
            StopRecording();
        }

        private void OnFallCompleted(FallData data)
        {
            if (data.severity == FallSeverity.RunEnding)
                StopRecording();
        }

        // -----------------------------------------------------------------------
        // FallTracker binding helpers
        // -----------------------------------------------------------------------

        private void BindFallTracker()
        {
            if (fallTracker == null) return;
            fallTracker.OnFallCompleted.RemoveListener(OnFallCompleted);
            fallTracker.OnFallCompleted.AddListener(OnFallCompleted);
        }

        private void UnbindFallTracker()
        {
            if (fallTracker == null) return;
            fallTracker.OnFallCompleted.RemoveListener(OnFallCompleted);
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

            // Use the PlayerController transform if available, else fall back to this transform
            Transform t = (playerController != null) ? playerController.transform : transform;

            GhostPlayerState state = DeterminePlayerState(grappleActive);

            _frames.Add(new RecorderFrame
            {
                time          = Time.time,
                position      = t.position,
                rotation      = t.rotation,
                grappleActive = grappleActive,
                grappleAnchor = grappleAnchor,
                playerState   = state
            });
        }

        private GhostPlayerState DeterminePlayerState(bool grappleActive)
        {
            if (grappleActive)
                return GhostPlayerState.Swinging;

            Rigidbody rb = _rb;
            if (rb == null && playerController != null)
                rb = playerController.GetComponent<Rigidbody>();

            if (rb == null)
                return GhostPlayerState.Airborne;

            float vy = rb.linearVelocity.y;

            if (vy < -5f)
                return GhostPlayerState.Falling;

            if (Mathf.Abs(vy) < 0.5f && rb.linearVelocity.magnitude < 1f)
                return GhostPlayerState.Grounded;

            return GhostPlayerState.Airborne;
        }

        // -----------------------------------------------------------------------
        // Binary serialisation
        // -----------------------------------------------------------------------

        /// <summary>
        /// Writes the frame list to a compact binary file.
        /// Format per frame:
        ///   float timestamp, float px, float py, float pz,
        ///   float rx, float ry, float rz, float rw,
        ///   bool isGrappling, float gx, float gy, float gz
        /// </summary>
        private static void SaveBinary(List<RecorderFrame> frames, string filePath)
        {
            try
            {
                string dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                using BinaryWriter bw = new BinaryWriter(fs);

                bw.Write(frames.Count);

                foreach (RecorderFrame f in frames)
                {
                    bw.Write(f.time);

                    bw.Write(f.position.x);
                    bw.Write(f.position.y);
                    bw.Write(f.position.z);

                    bw.Write(f.rotation.x);
                    bw.Write(f.rotation.y);
                    bw.Write(f.rotation.z);
                    bw.Write(f.rotation.w);

                    bw.Write(f.grappleActive);

                    bw.Write(f.grappleAnchor.x);
                    bw.Write(f.grappleAnchor.y);
                    bw.Write(f.grappleAnchor.z);
                }

                Debug.Log($"[GhostRecorder] Binary ghost saved to '{filePath}' ({frames.Count} frames).");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GhostRecorder] Failed to save binary ghost: {e.Message}");
            }
        }
    }
}
