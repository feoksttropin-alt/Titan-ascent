using System;
using UnityEngine;
using TitanAscent.Environment;

namespace TitanAscent.Systems
{
    [Flags]
    public enum ChallengeModifier
    {
        None         = 0,
        LowFuel      = 1 << 0,
        ExtremeWind  = 1 << 1,
        UltraSlippery = 1 << 2,
        Combined     = LowFuel | ExtremeWind | UltraSlippery
    }

    public class ChallengeManager : MonoBehaviour
    {
        [Header("Modifier Multipliers")]
        [SerializeField] private float lowFuelEnergyMultiplier = 0.35f;
        [SerializeField] private float extremeWindMultiplier = 2.5f;
        [SerializeField] private float ultraSlipperyFrictionMultiplier = 0.2f;

        [Header("References")]
        [SerializeField] private Player.ThrusterSystem thrusterSystem;
        [SerializeField] private Environment.WindSystem windSystem;

        private ChallengeModifier activeModifiers = ChallengeModifier.None;

        public ChallengeModifier ActiveModifiers => activeModifiers;

        public bool IsDaily { get; private set; }
        public int DailySeed { get; private set; }

        private void Start()
        {
            // Prefer DailyChallengeSeed singleton for a consistent, XOR-mixed seed;
            // fall back to the raw date integer if the singleton is not yet present.
            if (DailyChallengeSeed.Instance != null)
            {
                DailySeed = DailyChallengeSeed.Instance.TodaySeed;
            }
            else
            {
                DateTime today = DateTime.UtcNow.Date;
                DailySeed = (today.Year * 10000 + today.Month * 100 + today.Day) ^ 0x54495441;
            }
        }

        public void ApplyModifiers(ChallengeModifier modifiers)
        {
            activeModifiers = modifiers;

            if (thrusterSystem != null)
            {
                float energyScale = modifiers.HasFlag(ChallengeModifier.LowFuel)
                    ? lowFuelEnergyMultiplier
                    : 1f;
                thrusterSystem.SetEnergyMultiplier(energyScale);
            }

            if (windSystem != null)
            {
                float windScale = modifiers.HasFlag(ChallengeModifier.ExtremeWind)
                    ? extremeWindMultiplier
                    : 1f;
                windSystem.SetGlobalWindMultiplier(windScale);
            }

            SurfaceProperties.SetGlobalFrictionMultiplier(
                modifiers.HasFlag(ChallengeModifier.UltraSlippery)
                    ? ultraSlipperyFrictionMultiplier
                    : 1f
            );
        }

        public void ClearModifiers()
        {
            ApplyModifiers(ChallengeModifier.None);
        }

        public ChallengeModifier GenerateDailyModifiers()
        {
            UnityEngine.Random.InitState(DailySeed);
            int roll = UnityEngine.Random.Range(0, 7); // 0-6 maps to flag combinations
            IsDaily = true;
            return (ChallengeModifier)roll;
        }

        public void StartDailyChallenge()
        {
            ChallengeModifier daily = GenerateDailyModifiers();
            ApplyModifiers(daily);
        }
    }
}
