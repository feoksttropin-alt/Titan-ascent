using System.Collections;
using UnityEngine;

namespace TitanAscent.Systems
{
    public class CheckpointlessStats : MonoBehaviour
    {
        [SerializeField] private SaveManager saveManager;
        [SerializeField] private FallTracker fallTracker;
        [SerializeField] private float validationInterval = 30f;

        private float sessionBestFall = 0f;

        private void Start()
        {
            if (fallTracker != null)
                fallTracker.OnFallCompleted.AddListener(OnFallCompleted);

            StartCoroutine(PeriodicValidation());
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus) ValidateAndSave();
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause) ValidateAndSave();
        }

        private void OnFallCompleted(FallData data)
        {
            if (data.distance > sessionBestFall)
            {
                sessionBestFall = data.distance;
                if (saveManager != null)
                {
                    var d = saveManager.CurrentData;
                    if (data.distance > d.longestFall)
                    {
                        d.longestFall = data.distance;
                        d.totalFalls++;
                        saveManager.Save();
                    }
                    else
                    {
                        d.totalFalls++;
                        saveManager.Save();
                    }
                }
            }
        }

        private IEnumerator PeriodicValidation()
        {
            while (true)
            {
                yield return new WaitForSeconds(validationInterval);
                ValidateAndSave();
            }
        }

        private void ValidateAndSave()
        {
            if (saveManager == null) return;

            var d = saveManager.CurrentData;
            bool dirty = false;

            if (d.bestHeight < 0f || d.bestHeight > 15000f) { d.bestHeight = Mathf.Clamp(d.bestHeight, 0f, 15000f); dirty = true; }
            if (d.longestFall < 0f || d.longestFall > 15000f) { d.longestFall = Mathf.Clamp(d.longestFall, 0f, 15000f); dirty = true; }
            if (d.totalFalls < 0) { d.totalFalls = 0; dirty = true; }
            if (d.totalClimbs < 0) { d.totalClimbs = 0; dirty = true; }
            if (d.speedrunPB < 0f) { d.speedrunPB = 0f; dirty = true; }
            if (d.unlockedCosmetics == null) { d.unlockedCosmetics = new System.Collections.Generic.List<string>(); dirty = true; }
            if (d.completedChallenges == null) { d.completedChallenges = new System.Collections.Generic.List<string>(); dirty = true; }

            if (dirty)
            {
                UnityEngine.Debug.LogWarning("[CheckpointlessStats] Corrected corrupt save data.");
                saveManager.Save();
            }
        }
    }
}
