using System;
using UnityEngine;
using UnityEngine.Events;

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

        [Header("Events")]
        public UnityEvent<GameState> OnGameStateChanged;
        public UnityEvent OnClimbStarted;
        public UnityEvent OnVictory;

        private GameState currentState = GameState.MainMenu;
        private float sessionStartTime;
        private float currentHeight;
        private bool nearSummitNotified = false;

        public GameState CurrentState => currentState;
        public float SessionTime => Time.time - sessionStartTime;
        public float CurrentHeight => currentHeight;
        public SaveData GlobalStats => saveManager?.CurrentData;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
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
            saveManager?.Save();
            OnVictory?.Invoke();
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
                stats.totalClimbs = stats.totalClimbs; // preserved
            }
        }

        private void HandleNewHeightRecord(float height)
        {
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
