using System;
using UnityEngine;
using UnityEngine.Events;
using TitanAscent.UI;

namespace TitanAscent.Systems
{
    public enum GameState
    {
        MainMenu,
        Climbing,
        Paused,
        Falling,
        Victory
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Systems")]
        [SerializeField] private FallTracker fallTracker;
        [SerializeField] private NarrationSystem narration;
        [SerializeField] private SaveManager saveManager;
        [SerializeField] private PostRunSummary postRunSummary;
        [SerializeField] private SessionStatsTracker sessionStatsTracker;

        [Header("Events")]
        public UnityEvent<GameState> OnGameStateChanged;
        public UnityEvent OnClimbStarted;
        public UnityEvent OnVictory;
        public UnityEvent<float> OnNewHeightRecord;

        private GameState currentState = GameState.MainMenu;
        private float sessionStartTime;
        private float currentHeight;
        private bool nearSummitNotified = false;

        public GameState CurrentState => currentState;
        public float SessionTime => Time.time - sessionStartTime;
        public float CurrentHeight => currentHeight;
        public float BestHeightEver => fallTracker != null ? fallTracker.BestHeightEver : 0f;
        public int TotalFalls => fallTracker != null ? fallTracker.TotalFalls : 0;
        public float LongestFall => fallTracker != null ? fallTracker.LongestFall : 0f;
        public SaveData GlobalStats => saveManager?.CurrentData;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (postRunSummary == null)
                postRunSummary = FindFirstObjectByType<TitanAscent.UI.PostRunSummary>();

            if (sessionStatsTracker == null)
                sessionStatsTracker = FindFirstObjectByType<SessionStatsTracker>();

            saveManager?.Load();
            BindEvents();
        }

        private void BindEvents()
        {
            if (fallTracker != null)
            {
                fallTracker.OnFallCompleted.AddListener(HandleFallCompleted);
                fallTracker.OnNewHeightRecord.AddListener(HandleNewHeightRecord);
            }
        }

        private void Update()
        {
            if (currentState != GameState.Climbing && currentState != GameState.Falling) return;

            if (fallTracker != null)
                currentHeight = fallTracker.transform.position.y;

            // Near summit trigger (within 500m of 10000m)
            if (!nearSummitNotified && currentHeight >= 9500f)
            {
                nearSummitNotified = true;
                narration?.TriggerNearSummit();
            }

            // Victory check
            if (currentHeight >= 10000f)
                TriggerVictory();
        }

        public void StartClimb()
        {
            SetState(GameState.Climbing);
            sessionStartTime = Time.time;
            nearSummitNotified = false;
            SessionManager.Instance?.StartSession();
            sessionStatsTracker?.StartSession();
            narration?.TriggerClimbStart();
            OnClimbStarted?.Invoke();
        }

        public void PauseGame()
        {
            if (currentState == GameState.Climbing || currentState == GameState.Falling)
            {
                SetState(GameState.Paused);
                Time.timeScale = 0f;
            }
        }

        public void ResumeGame()
        {
            if (currentState == GameState.Paused)
            {
                SetState(GameState.Climbing);
                Time.timeScale = 1f;
            }
        }

        public void TriggerVictory()
        {
            if (currentState == GameState.Victory) return;
            SetState(GameState.Victory);
            narration?.TriggerVictory();
            SessionManager.Instance?.EndSession(true);
            RecordCurrentRun();
            saveManager?.Save();
            OnVictory?.Invoke();
            postRunSummary?.ShowSummary(
                height:      currentHeight,
                time:        SessionTime,
                falls:       TotalFalls,
                longestFall: LongestFall);
        }

        private void HandleFallCompleted(FallData data)
        {
            SetState(data.severity >= FallSeverity.Large ? GameState.Falling : GameState.Climbing);
            narration?.TriggerForFall(data);

            if (saveManager != null)
            {
                var stats = saveManager.CurrentData;
                if (data.distance > stats.longestFall)
                {
                    stats.longestFall = data.distance;
                    saveManager.Save();
                }
                stats.totalFalls++;
                saveManager.Save();
            }

            // A run-ending fall concludes the current session — record it
            if (data.severity == FallSeverity.RunEnding)
            {
                RecordCurrentRun();
            }
        }

        /// <summary>Writes a RunRecord for the current session to SaveManager.</summary>
        private void RecordCurrentRun()
        {
            if (saveManager == null) return;
            float height = fallTracker != null ? fallTracker.BestHeightEver : currentHeight;
            int   falls  = fallTracker != null ? fallTracker.TotalFalls      : 0;
            saveManager.AddRunRecord(height, SessionTime, falls);
        }

        private void HandleNewHeightRecord(float height)
        {
            OnNewHeightRecord?.Invoke(height);
            narration?.TriggerNewHeightRecord();
            if (saveManager != null)
            {
                var stats = saveManager.CurrentData;
                if (height > stats.bestHeight)
                {
                    stats.bestHeight = height;
                    saveManager.Save();
                }
            }
        }

        private void SetState(GameState newState)
        {
            if (currentState == newState) return;
            currentState = newState;
            OnGameStateChanged?.Invoke(newState);
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause) saveManager?.Save();
        }

        private void OnApplicationQuit()
        {
            saveManager?.Save();
        }
    }
}
