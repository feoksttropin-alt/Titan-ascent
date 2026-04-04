using UnityEngine;

namespace TitanAscent.Data
{
    public enum CosmeticType
    {
        Suit,
        GrappleSkin,
        RopeColor,
        ParticleTrail
    }

    [CreateAssetMenu(fileName = "CosmeticItem", menuName = "TitanAscent/Cosmetic Item")]
    public class CosmeticItem : ScriptableObject
    {
        [Header("Identification")]
        public string itemName;
        public string itemDescription;
        public CosmeticType itemType;

        [Header("Unlock")]
        public string unlockedByAchievement;
        public bool isUnlockedByDefault = false;

        [Header("Visuals")]
        public Sprite previewSprite;
        public Material materialOverride;
        public Color primaryColor = Color.white;
        public Color secondaryColor = Color.grey;

        [Header("Particle Trail")]
        public ParticleSystem trailParticleSystemPrefab;
        public Gradient trailColorGradient;

        /// <summary>
        /// Returns the unique identifier for save/load operations.
        /// Uses the asset name by default.
        /// </summary>
        public string GetId()
        {
            return name;
        }

        /// <summary>
        /// Applies this cosmetic to a given renderer. No gameplay effects.
        /// </summary>
        public void ApplyToRenderer(Renderer renderer)
        {
            if (renderer == null || materialOverride == null) return;

            Material[] mats = renderer.sharedMaterials;
            if (mats.Length > 0)
            {
                mats[0] = materialOverride;
                renderer.sharedMaterials = mats;
            }
        }
    }
}
