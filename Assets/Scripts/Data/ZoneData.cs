using UnityEngine;
using TitanAscent.Environment;

namespace TitanAscent.Data
{
    [CreateAssetMenu(fileName = "ZoneData", menuName = "TitanAscent/Zone Data")]
    public class ZoneData : ScriptableObject
    {
        [Header("Identification")]
        public string zoneName;
        [TextArea(2, 4)]
        public string description;

        [Header("Height Range")]
        public float minHeight;
        public float maxHeight;

        [Header("Environment")]
        public SurfaceType dominantSurface = SurfaceType.ScaleArmor;
        [Range(0f, 3f)] public float windMultiplier = 1f;
        public Color ambientLightColor = Color.white;
        [Range(0f, 1f)] public float fogDensity = 0.02f;

        [Header("Gameplay")]
        public bool hasMovingElements = false;
        public bool narrationUnlocked = true;
        [TextArea(1, 3)]
        public string[] zoneNarrationLines;

        [Header("Debug")]
        public Color zoneDebugColor = Color.cyan;

        public bool ContainsHeight(float height)
        {
            return height >= minHeight && height < maxHeight;
        }

        public float GetHeightProgress(float height)
        {
            float range = maxHeight - minHeight;
            if (range <= 0f) return 0f;
            return Mathf.Clamp01((height - minHeight) / range);
        }
    }
}
