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

        private void Awake()
        {
            narrationUI = FindObjectOfType<UI.NarrationUI>();
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
