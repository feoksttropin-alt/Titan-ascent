using System;
using System.Collections.Generic;
using UnityEngine;

namespace TitanAscent.Systems
{
    [Serializable]
    public class SaveData
    {
        public int version = 2;
        public float bestHeight = 0f;
        public float longestFall = 0f;
        public int totalFalls = 0;
        public int totalClimbs = 0;
        public float speedrunPB = 0f;
        public List<string> unlockedCosmetics = new List<string>();
        public List<string> completedChallenges = new List<string>();
        // Run history — populated by RunHistoryUI
        public List<UI.RunRecord> runHistory = new List<UI.RunRecord>();
        // Checkpoint state
        public float checkpointHeight = 0f;
        public float checkpointHealth = 100f;
    }

    public class SaveManager : MonoBehaviour
    {
        private const string SaveKey = "TitanAscent_SaveData";
        private const int CurrentVersion = 2;

        private SaveData currentData = new SaveData();

        public SaveData CurrentData => currentData;

        public void Load()
        {
            if (!PlayerPrefs.HasKey(SaveKey))
            {
                currentData = new SaveData();
                return;
            }

            try
            {
                string json = PlayerPrefs.GetString(SaveKey);
                currentData = JsonUtility.FromJson<SaveData>(json);
                MigrateIfNeeded();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] Failed to load save: {e.Message}. Starting fresh.");
                currentData = new SaveData();
            }
        }

        public void Save()
        {
            try
            {
                currentData.version = CurrentVersion;
                string json = JsonUtility.ToJson(currentData, false);
                PlayerPrefs.SetString(SaveKey, json);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Failed to save: {e.Message}");
            }
        }

        public void Reset()
        {
            currentData = new SaveData();
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        public void UnlockCosmetic(string cosmeticId)
        {
            if (!currentData.unlockedCosmetics.Contains(cosmeticId))
            {
                currentData.unlockedCosmetics.Add(cosmeticId);
                Save();
            }
        }

        public void CompleteChallenge(string challengeId)
        {
            if (!currentData.completedChallenges.Contains(challengeId))
            {
                currentData.completedChallenges.Add(challengeId);
                Save();
            }
        }

        public bool IsCosmeticUnlocked(string cosmeticId) =>
            currentData.unlockedCosmetics.Contains(cosmeticId);

        public bool IsChallengeCompleted(string challengeId) =>
            currentData.completedChallenges.Contains(challengeId);

        /// <summary>
        /// Records a completed run to the run history list and persists it.
        /// Keeps a maximum of 20 entries (newest first).
        /// </summary>
        public void AddRunRecord(float height, float time, int falls)
        {
            if (currentData.runHistory == null)
                currentData.runHistory = new System.Collections.Generic.List<UI.RunRecord>();

            UI.RunRecord record = new UI.RunRecord
            {
                runDate         = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                maxHeight       = height,
                totalFalls      = falls,
                durationSeconds = time,
                modeType        = "Normal",
                reached         = height >= 10000f,
            };

            currentData.runHistory.Insert(0, record);

            const int maxRecords = 20;
            if (currentData.runHistory.Count > maxRecords)
                currentData.runHistory.RemoveRange(maxRecords, currentData.runHistory.Count - maxRecords);

            Save();
        }

        public void UpdateSpeedrunPB(float time)
        {
            if (currentData.speedrunPB <= 0f || time < currentData.speedrunPB)
            {
                currentData.speedrunPB = time;
                Save();
            }
        }

        /// <summary>Clears the persisted checkpoint so the next run starts from scratch.</summary>
        public void ClearCheckpoint()
        {
            currentData.checkpointHeight = 0f;
            currentData.checkpointHealth = 100f;
            Save();
        }

        private void MigrateIfNeeded()
        {
            if (currentData.version < CurrentVersion)
            {
                // v1 → v2: add checkpoint fields (defaults are fine)
                currentData.version = CurrentVersion;
                Save();
            }
        }
    }
}
