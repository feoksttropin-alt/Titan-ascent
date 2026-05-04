using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TitanAscent.Systems
{
    public class NarrationSystem : MonoBehaviour
    {
        [Header("Cooldown")]
        [SerializeField] private float cooldownBetweenLines = 8f;

        [Header("Audio")]
        [SerializeField] private AudioSource narratorAudioSource;

        private UI.NarrationUI narrationUI;
        private float lastNarrationTime = -999f;
        private Dictionary<string, int> lastPlayedIndex = new Dictionary<string, int>();

        // --- Narration Lines ---
        private static readonly string[] ClimbStartLines =
        {
            "The titan does not notice you. Yet.",
            "Another soul reaches for the crown.",
            "Gravity is patient.",
            "Thousands have tried. The titan remembers none of them.",
            "Begin."
        };

        private static readonly string[] SmallFallLines =
        {
            "A stumble. Nothing more.",
            "The titan shifts.",
            "Regain your footing.",
            "Small losses. Smaller lessons.",
            "Continue."
        };

        private static readonly string[] MediumFallLines =
        {
            "The mountain pushes back.",
            "You've lost ground. Not hope.",
            "Patience.",
            "The rope remembers where you failed.",
            "Higher. Again."
        };

        private static readonly string[] LargeFallLines =
        {
            "Progress is a matter of perspective.",
            "The titan exhales.",
            "You've been here before.",
            "The summit waits, indifferent.",
            "Those meters are not gone. They are practice.",
            "The fall teaches what the climb cannot."
        };

        private static readonly string[] CatastrophicFallLines =
        {
            "That was... considerable.",
            "The mountain remembers all of them.",
            "Breathe.",
            "Somewhere above, your previous self watches.",
            "The crown has seen worse. It will see you again.",
            "Distance is temporary. Resolve is not."
        };

        private static readonly string[] MajorRecoveryLines =
        {
            "Remarkable.",
            "The titan blinked.",
            "That should not have worked.",
            "Hold that feeling.",
            "The crown noticed.",
            "Impossible is a narrow word."
        };

        private static readonly string[] NewHeightRecordLines =
        {
            "Higher than before.",
            "The air is thinner here.",
            "Uncharted.",
            "Few have stood where you now hang.",
            "The titan's shadow grows longer below you."
        };

        private static readonly string[] NearSummitLines =
        {
            "The crown. You can see it.",
            "So close that the wind has changed.",
            "Do not look down.",
            "Every meter now costs more than those before.",
            "The titan knows you are here."
        };

        private static readonly string[] VictoryLines =
        {
            "You stand where none have stood.",
            "The crown remembers you now.",
            "It was always you.",
            "The titan is still.",
            "There are no more meters to climb."
        };

        private static readonly string[] RepeatedFailureSameAreaLines =
        {
            "The same stone refuses you. Find another path.",
            "This section has beaten others. It won't beat you.",
            "Change your approach. The surface hasn't changed — you can.",
            "Fall here enough times and you'll memorize it.",
            "The titan does not move. Your approach must."
        };

        private static readonly string[] GrappleMissStreakLines =
        {
            "Breathe. Then aim.",
            "The anchors are there. Your timing isn't.",
            "Slow down the throw.",
            "One hit is all you need.",
            "Patience is its own momentum."
        };

        private static readonly string[] LongStuckLines =
        {
            "You've been here long enough to know this section.",
            "Progress is sometimes invisible.",
            "You are not falling. That counts for something.",
            "The summit can wait. So can you."
        };

        // --- Zone Entry Lines ---
        private static readonly string[] Zone1Lines =
        {
            "The tail is where everyone begins.",
            "Your first steps. The titan does not react.",
            "Low ground. Easy to forget how far you have to go.",
            "Begin at the base. The crown will make itself known."
        };

        private static readonly string[] Zone2Lines =
        {
            "The spires are older than memory.",
            "Balance here. Every surface is a razor's edge.",
            "These formations took centuries. Respect them.",
            "Bone like iron. This is still the easy part."
        };

        private static readonly string[] Zone3Lines =
        {
            "The hind leg shifts beneath you.",
            "Muscle and sinew. Less forgiving than stone.",
            "The titan walks in its sleep. Hold tight.",
            "You are halfway to nowhere."
        };

        private static readonly string[] Zone4Lines =
        {
            "Wing root. The membrane shudders when it breathes.",
            "Do not look down yet. There is still too far to fall.",
            "The first moving terrain. Adjust.",
            "A wing that could blot out your sun. You are a fleck upon it."
        };

        private static readonly string[] Zone5Lines =
        {
            "The spine. The longest climb you have made.",
            "Every ridge a waypoint. Every gap a warning.",
            "Wind finds you here as if it was searching.",
            "This is where most stop. You have not stopped."
        };

        private static readonly string[] Zone6Lines =
        {
            "The Graveyard. Others have been here.",
            "Relics of the ambitious. You are not them.",
            "They fell. You are still rising.",
            "What remains of those before you is a map of where not to stand."
        };

        private static readonly string[] Zone7Lines =
        {
            "The storm does not care who you are.",
            "Visibility drops. Trust your grapple.",
            "This is the crucible. Most runs end here.",
            "The lightning is indifferent. So is the titan."
        };

        private static readonly string[] Zone8Lines =
        {
            "The neck. You can feel the titan breathing.",
            "Every contraction is a trap. Every expansion is a gift.",
            "Rhythm. Find the breathing and climb between pulses.",
            "So close the shadow of the crown falls on you."
        };

        private static readonly string[] Zone9Lines =
        {
            "The Crown. You are here.",
            "Every meter costs something now.",
            "The summit is not luck. It is repetition.",
            "Do not mistake proximity for certainty."
        };

        private void Awake()
        {
            narrationUI = FindFirstObjectByType<UI.NarrationUI>();
            if (narrationUI == null)
                Debug.LogWarning("[NarrationSystem] NarrationUI not found in scene. All narration will be silent.");
        }

        public void TriggerClimbStart() => TryNarrate("ClimbStart", ClimbStartLines, true);
        public void TriggerSmallFall() => TryNarrate("SmallFall", SmallFallLines);
        public void TriggerMediumFall() => TryNarrate("MediumFall", MediumFallLines);
        public void TriggerLargeFall() => TryNarrate("LargeFall", LargeFallLines);
        public void TriggerCatastrophicFall() => TryNarrate("CatastrophicFall", CatastrophicFallLines);
        public void TriggerMajorRecovery() => TryNarrate("MajorRecovery", MajorRecoveryLines);
        public void TriggerNewHeightRecord() => TryNarrate("NewHeightRecord", NewHeightRecordLines);
        public void TriggerNearSummit() => TryNarrate("NearSummit", NearSummitLines, true);
        public void TriggerVictory() => TryNarrate("Victory", VictoryLines, true);
        public void TriggerRepeatedFailureSameArea() => TryNarrate("RepeatedFailureSameArea", RepeatedFailureSameAreaLines);
        public void TriggerGrappleMissStreak() => TryNarrate("GrappleMissStreak", GrappleMissStreakLines);
        public void TriggerLongStuck() => TryNarrate("LongStuck", LongStuckLines);

        public void TriggerZoneEntry(int zoneIndex)
        {
            string[] lines = zoneIndex switch
            {
                0 => Zone1Lines,
                1 => Zone2Lines,
                2 => Zone3Lines,
                3 => Zone4Lines,
                4 => Zone5Lines,
                5 => Zone6Lines,
                6 => Zone7Lines,
                7 => Zone8Lines,
                8 => Zone9Lines,
                _ => null
            };

            if (lines != null)
                TryNarrate($"Zone{zoneIndex + 1}Entry", lines, ignoreCooldown: true);
        }

        public void TriggerForFall(FallData data)
        {
            switch (data.severity)
            {
                case FallSeverity.Small: TriggerSmallFall(); break;
                case FallSeverity.Medium: TriggerMediumFall(); break;
                case FallSeverity.Large: TriggerLargeFall(); break;
                case FallSeverity.Catastrophic: TriggerCatastrophicFall(); break;
                case FallSeverity.RunEnding: TriggerCatastrophicFall(); break;
            }
        }

        // ── Narration helpers (public so sub-systems such as NarrationExtended can call them) ──

        /// <summary>
        /// Picks a non-repeating line from <paramref name="lines"/> and narrates it.
        /// Respects the global cooldown unless <paramref name="ignoreCooldown"/> is true.
        /// </summary>
        public void TryNarrate(string key, string[] lines, bool ignoreCooldown = false)
        {
            if (!ignoreCooldown && Time.time - lastNarrationTime < cooldownBetweenLines)
                return;

            string line = PickLine(key, lines);
            Narrate(line);
        }

        /// <summary>Directly narrates <paramref name="line"/> without anti-repeat key tracking.</summary>
        public void NarrateRaw(string line) => Narrate(line);

        private string PickLine(string key, string[] lines)
        {
            if (lines.Length == 0) return string.Empty;
            if (lines.Length == 1) return lines[0];

            // Prevent unbounded dictionary growth across long sessions
            if (lastPlayedIndex.Count > 50)
                lastPlayedIndex.Clear();

            lastPlayedIndex.TryGetValue(key, out int lastIndex);
            int newIndex;
            do { newIndex = Random.Range(0, lines.Length); }
            while (newIndex == lastIndex && lines.Length > 1);

            lastPlayedIndex[key] = newIndex;
            return lines[newIndex];
        }

        private void Narrate(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            lastNarrationTime = Time.time;
            narrationUI?.ShowLine(line);
            // narratorAudioSource voice playback would be triggered here
        }
    }
}
