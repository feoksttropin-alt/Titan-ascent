using System;
using System.Collections.Generic;
using UnityEngine;

namespace TitanAscent.Systems
{
    /// <summary>
    /// Generates a deterministic daily challenge from a date-derived seed.
    /// Seed = (year*10000 + month*100 + day) XOR 0x54495441 ("TITA")
    /// </summary>
    public class DailyChallengeSeed : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Singleton
        // ------------------------------------------------------------------

        public static DailyChallengeSeed Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            GenerateForToday();
        }

        // ------------------------------------------------------------------
        // Modifiers pool
        // ------------------------------------------------------------------

        private static readonly string[] ModifierPool =
        {
            "NoBounce",          // Landing impacts are not reduced
            "GlassRope",         // Grapple rope breaks after 3 uses
            "SlipperyTitan",     // All surface friction halved
            "HeavyGravity",      // Gravity 1.5×
            "LightGravity",      // Gravity 0.6×
            "WindStorm",         // Constant strong wind
            "NightClimb",        // Heavily reduced visibility
            "TurboThrusters",    // Thruster force doubled, recharge time doubled
            "SingleShot",        // Only one grapple shot at a time (no multi-grapple)
            "FragileGrip",       // Grip depletes 3× faster
            "SpeedDemon",        // Player movement speed +50%
            "BoulderFall",       // Increased debris rain
            "MiniGrapple",       // Max rope length halved
            "LongGrapple",       // Max rope length doubled
            "SilentTitan",       // No narrator
            "BriefWindow",       // Emergency recovery window −50% duration
            "BonusAnchor",       // +20% extra anchor points visible
            "ColdFog",           // Occasional fog obscures anchors
            "EchoingGroan",      // Titan tremors every 30s regardless of zone
            "SteelNerves"        // No fall-distance overlay or distance feedback
        };

        // ------------------------------------------------------------------
        // Bonus objectives pool (20 entries)
        // ------------------------------------------------------------------

        private static readonly string[] ObjectivePool =
        {
            "Reach 3000m without falling more than 50m total",
            "Complete a single unbroken swing chain of 10+ grapples",
            "Reach the summit without using thrusters",
            "Survive a fall of 500m or greater",
            "Attach to 50 unique anchor points in one run",
            "Reach Zone 5 within 10 minutes of starting",
            "Complete a 200m vertical gain in under 60 seconds",
            "Land on a BoneRidge surface 5 times in one run",
            "Use grip braking to stop a slide within 2 seconds 3 times",
            "Reach 1000m, 2000m, and 3000m all in the same run",
            "Fire and successfully attach to 20 grapples in 2 minutes",
            "Complete the run using the MultiGrapple only after Zone 4",
            "Survive a catastrophic fall without using emergency recovery",
            "Reach Zone 7 with 0 total falls",
            "Achieve a swing velocity of 40 m/s or greater",
            "Use the thruster system fewer than 5 times in a full run",
            "Reel in at least 500m of rope across the entire run",
            "Reach 5000m height without passing through Zone 6 anchors",
            "Complete the run in under 25 minutes",
            "Score a New Height Record on the daily challenge seed"
        };

        // ------------------------------------------------------------------
        // Flavor text pool (30 entries)
        // ------------------------------------------------------------------

        private static readonly string[] FlavorPool =
        {
            "The titan stirs in its ancient sleep. Today the climb is different.",
            "Winds from the east. The beast breathes slow. Your window is narrow.",
            "Ash falls from the crown. The path changes with every storm.",
            "Today's ascent was foretold in the glyph-scars on the left shoulder.",
            "The creature exhales for the first time in forty years. Choose your route carefully.",
            "A memory of yesterday's climber still clings to the upper ridges.",
            "The giant's heartbeat is irregular. Trust nothing that moves.",
            "Something sleeps in the hollow behind the seventh rib. Do not wake it.",
            "Lightning carved new grooves last night. The old paths are gone.",
            "Every climber sees something different at the summit. If they reach it.",
            "The titan shifted three centimetres to the left. That's enough to ruin you.",
            "Cold fog hugs the mid-section. The anchors are there. Find them.",
            "Muscle memory is both your greatest ally and the titan's favourite trap.",
            "Other challengers have started today. None of them have returned.",
            "The upper zones are quieter than usual. That is not a good sign.",
            "Today the titan sheds. Old anchors fall away. New ones form.",
            "There is a climber etched into the first ridge wall. They got very high.",
            "The wind carries something that sounds almost like encouragement.",
            "The titan does not notice you. It doesn't need to.",
            "You are very small. That is the only reason you might succeed.",
            "The surface remembers impact. It will not forget yours.",
            "Climb as though the titan is waking. It might be.",
            "Every metre is a gamble. Every swing a negotiation.",
            "The crown is so high the clouds form between you and it.",
            "People have mapped this path. The titan has unmapped it since.",
            "Begin at the foot. End at the crown. Do not linger anywhere between.",
            "The body of the titan is not stone — it breathes, bleeds, and shifts.",
            "Today's configuration favours the patient. Rush and the giant wins.",
            "When you fall — and you will — fall gracefully.",
            "The record is held by someone who has not climbed in three years. Prove why."
        };

        // ------------------------------------------------------------------
        // Properties
        // ------------------------------------------------------------------

        public int TodaySeed { get; private set; }

        public IReadOnlyList<string> TodayModifiers { get; private set; }

        public string TodayBonusObjective { get; private set; }

        public string TodayFlavorText { get; private set; }

        public bool HasCompletedToday
        {
            get
            {
                string key = DailyKey(DateTime.Today);
                SaveManager sm = FindFirstObjectByType<SaveManager>();
                if (sm == null) return false;
                return sm.CurrentData.completedChallenges.Contains(key);
            }
        }

        // ------------------------------------------------------------------
        // Seeded generation
        // ------------------------------------------------------------------

        private void GenerateForToday()
        {
            DateTime today = DateTime.Today;
            TodaySeed = ComputeSeed(today);

            System.Random rng = new System.Random(TodaySeed);

            // Modifier count: weighted toward 1 (single most common)
            int modCount = WeightedModifierCount(rng);
            List<string> mods = new List<string>(modCount);
            List<string> pool = new List<string>(ModifierPool);

            for (int i = 0; i < modCount && pool.Count > 0; i++)
            {
                int idx = rng.Next(pool.Count);
                mods.Add(pool[idx]);
                pool.RemoveAt(idx);
            }

            TodayModifiers   = mods.AsReadOnly();
            TodayBonusObjective = ObjectivePool[rng.Next(ObjectivePool.Length)];
            TodayFlavorText  = FlavorPool[rng.Next(FlavorPool.Length)];
        }

        private static int ComputeSeed(DateTime date)
        {
            int datePart = (date.Year * 10000) + (date.Month * 100) + date.Day;
            return datePart ^ 0x54495441;
        }

        private static int WeightedModifierCount(System.Random rng)
        {
            // 60% chance of 1, 30% chance of 2, 10% chance of 3
            float roll = (float)rng.NextDouble();
            if (roll < 0.60f) return 1;
            if (roll < 0.90f) return 2;
            return 3;
        }

        // ------------------------------------------------------------------
        // Completion
        // ------------------------------------------------------------------

        /// <summary>
        /// Records today's challenge completion with height and time.
        /// </summary>
        public void CompleteDailyChallenge(float height, float time)
        {
            string key = DailyKey(DateTime.Today);
            SaveManager sm = FindFirstObjectByType<SaveManager>();

            if (sm == null)
            {
                Debug.LogWarning("[DailyChallengeSeed] No SaveManager found.");
                return;
            }

            if (!sm.CurrentData.completedChallenges.Contains(key))
            {
                sm.CurrentData.completedChallenges.Add(key);
                sm.Save();
                Debug.Log($"[DailyChallengeSeed] Daily challenge completed: height={height:F0}m, time={time:F1}s");
            }
        }

        private static string DailyKey(DateTime date) => $"daily_{date:yyyy-MM-dd}";
    }
}
