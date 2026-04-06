using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace TitanAscent.Systems
{
    // -----------------------------------------------------------------------
    // Data
    // -----------------------------------------------------------------------

    [Serializable]
    public struct GhostFrame
    {
        public float    time;
        public Vector3  position;
        public Quaternion rotation;
        public bool     grappleActive;
        public Vector3  grappleAnchorPoint;
    }

    [Serializable]
    public class GhostRecording
    {
        public string label;
        public string sessionId;
        public List<GhostFrame> frames = new List<GhostFrame>();
    }

    // -----------------------------------------------------------------------
    // GhostSystem
    // -----------------------------------------------------------------------

    public class GhostSystem : MonoBehaviour
    {
        private const int   MaxFrames        = 72000;   // 1 hour at 20 fps
        private const float RecordInterval   = 0.05f;   // seconds (20 fps)

        /// <summary>Fixed path used for the most-recent ghost file.</summary>
        public static string LastGhostPath =>
            Path.Combine(Application.persistentDataPath, "ghost_last.dat");

        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Grapple.GrappleController grappleController;

        [Header("Ghost Visual")]
        [SerializeField] private Material ghostMaterial;
        [SerializeField] private Material ghostRopeMaterial;
        [SerializeField] private GameObject ghostAvatarPrefab;

        private GhostRecording currentRecording;
        private bool isRecording  = false;
        private float recordTimer = 0f;

        private GhostRecording loadedRecording;
        private bool isReplaying = false;
        private float replayStartTime = 0f;
        private int   replayFrameIndex = 0;
        private GameObject ghostInstance;

        private string currentSessionId;

        // -----------------------------------------------------------------------
        // Public properties
        // -----------------------------------------------------------------------

        /// <summary>True while a ghost replay is currently running.</summary>
        public bool IsPlaybackActive => isReplaying;

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            currentSessionId = Guid.NewGuid().ToString("N");

            if (playerTransform == null)
            {
                Player.PlayerController pc = FindFirstObjectByType<Player.PlayerController>();
                if (pc != null) playerTransform = pc.transform;
            }

            if (grappleController == null)
                grappleController = FindFirstObjectByType<Grapple.GrappleController>();
        }

        private void Update()
        {
            if (isRecording)
                TickRecording();

            if (isReplaying)
                TickReplay();
        }

        // -----------------------------------------------------------------------
        // Recording
        // -----------------------------------------------------------------------

        public void StartRecording()
        {
            currentRecording = new GhostRecording
            {
                label     = "run",
                sessionId = currentSessionId
            };
            isRecording = true;
            recordTimer = 0f;
            Debug.Log("[GhostSystem] Recording started.");
        }

        public void StopRecording()
        {
            isRecording = false;
            Debug.Log($"[GhostSystem] Recording stopped. Frames: {currentRecording?.frames.Count ?? 0}");
        }

        private void TickRecording()
        {
            if (currentRecording == null || playerTransform == null) return;

            recordTimer += Time.deltaTime;
            if (recordTimer < RecordInterval) return;
            recordTimer -= RecordInterval;

            if (currentRecording.frames.Count >= MaxFrames)
            {
                Debug.LogWarning("[GhostSystem] Max recording length (1 hour) reached. Recording stopped.");
                StopRecording();
                return;
            }

            bool grappleActive   = false;
            Vector3 anchorPoint  = Vector3.zero;

            if (grappleController != null)
            {
                grappleActive = grappleController.IsAttached;
                if (grappleActive)
                    anchorPoint = grappleController.AttachPoint;
            }

            currentRecording.frames.Add(new GhostFrame
            {
                time              = Time.time,
                position          = playerTransform.position,
                rotation          = playerTransform.rotation,
                grappleActive     = grappleActive,
                grappleAnchorPoint= anchorPoint
            });
        }

        // -----------------------------------------------------------------------
        // Save / Load
        // -----------------------------------------------------------------------

        public void SaveGhost(string label)
        {
            if (currentRecording == null || currentRecording.frames.Count == 0)
            {
                Debug.LogWarning("[GhostSystem] No recording data to save.");
                return;
            }

            currentRecording.label = label;

            // Always overwrite the fixed "last run" ghost file for quick replay access.
            WriteRecordingToFile(currentRecording, LastGhostPath);

            // Also persist a session-specific copy for run-history lookup.
            string sessionFileName = $"ghost_{currentSessionId}.dat";
            string sessionFilePath = Path.Combine(Application.persistentDataPath, sessionFileName);
            WriteRecordingToFile(currentRecording, sessionFilePath);
        }

        private void WriteRecordingToFile(GhostRecording recording, string filePath)
        {
            try
            {
                using FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                BinaryFormatter bf  = new BinaryFormatter();
                bf.Serialize(fs, recording);
                Debug.Log($"[GhostSystem] Ghost saved: {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GhostSystem] Failed to save ghost to '{filePath}': {e.Message}");
            }
        }

        public bool LoadGhost(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[GhostSystem] Ghost file not found: {filePath}");
                return false;
            }

            try
            {
                using FileStream fs  = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                BinaryFormatter bf   = new BinaryFormatter();
                loadedRecording = (GhostRecording)bf.Deserialize(fs);
                Debug.Log($"[GhostSystem] Ghost loaded: {loadedRecording.frames.Count} frames, label={loadedRecording.label}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GhostSystem] Failed to load ghost: {e.Message}");
                return false;
            }
        }

        // -----------------------------------------------------------------------
        // Playback
        // -----------------------------------------------------------------------

        public void StartPlayback()
        {
            if (loadedRecording == null || loadedRecording.frames.Count == 0)
            {
                Debug.LogWarning("[GhostSystem] No ghost loaded for playback.");
                return;
            }

            SpawnGhostVisual();
            isReplaying      = true;
            replayStartTime  = Time.time;
            replayFrameIndex = 0;
            Debug.Log("[GhostSystem] Playback started.");
        }

        public void StopPlayback()
        {
            isReplaying = false;
            DestroyGhostVisual();
            Debug.Log("[GhostSystem] Playback stopped.");
        }

        private void TickReplay()
        {
            if (loadedRecording == null || ghostInstance == null) return;

            List<GhostFrame> frames = loadedRecording.frames;
            if (frames.Count == 0) return;

            float elapsed = Time.time - replayStartTime;

            // Find the two frames to interpolate between
            while (replayFrameIndex < frames.Count - 1 &&
                   frames[replayFrameIndex + 1].time - frames[0].time <= elapsed)
            {
                replayFrameIndex++;
            }

            if (replayFrameIndex >= frames.Count - 1)
            {
                // Reached end of ghost recording
                StopPlayback();
                return;
            }

            GhostFrame a = frames[replayFrameIndex];
            GhostFrame b = frames[replayFrameIndex + 1];

            float aTime   = a.time - frames[0].time;
            float bTime   = b.time - frames[0].time;
            float span    = bTime - aTime;
            float t       = span > 0f ? Mathf.Clamp01((elapsed - aTime) / span) : 1f;

            ghostInstance.transform.position = Vector3.Lerp(a.position, b.position, t);
            ghostInstance.transform.rotation = Quaternion.Slerp(a.rotation, b.rotation, t);
        }

        // -----------------------------------------------------------------------
        // Ghost Visual Helpers
        // -----------------------------------------------------------------------

        private void SpawnGhostVisual()
        {
            DestroyGhostVisual();

            if (ghostAvatarPrefab != null)
            {
                ghostInstance = Instantiate(ghostAvatarPrefab);
            }
            else
            {
                // Fallback: primitive capsule
                ghostInstance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Destroy(ghostInstance.GetComponent<Collider>());
            }

            // Apply ghost material (semi-transparent blue)
            if (ghostMaterial != null)
            {
                Renderer[] renderers = ghostInstance.GetComponentsInChildren<Renderer>();
                foreach (Renderer r in renderers)
                    r.material = ghostMaterial;
            }

            ghostInstance.name = "GhostReplay";
        }

        private void DestroyGhostVisual()
        {
            if (ghostInstance != null)
            {
                Destroy(ghostInstance);
                ghostInstance = null;
            }
        }
    }
}
