using System;
using UnityEngine;
using TitanAscent.Data;
using TitanAscent.Grapple;

namespace TitanAscent.Systems
{
    [Serializable]
    public class CosmeticLoadout
    {
        public string suitId          = string.Empty;
        public string grappleSkinId   = string.Empty;
        public string ropeColorId     = string.Empty;
        public string particleTrailId = string.Empty;
    }

    public class CosmeticSystem : MonoBehaviour
    {
        private const string SaveKey = "TitanAscent_CosmeticLoadout";

        [Header("Player Visual Components")]
        [SerializeField] private Renderer playerRenderer;
        [SerializeField] private Renderer grappleRenderer;
        [SerializeField] private RopeSimulator ropeSimulator;
        [SerializeField] private Transform playerTransform;

        // ── Active state ─────────────────────────────────────────────────────────
        private Material activeSuitMaterial;
        private Material activeGrappleSkinMaterial;
        private Color activeRopeColor = Color.white;
        private GameObject activeParticleTrailInstance;

        private CosmeticLoadout currentLoadout = new CosmeticLoadout();

        // ── Public apply methods ─────────────────────────────────────────────────

        /// <summary>Sets the player renderer's first material slot to item.materialOverride.</summary>
        public void ApplySuit(CosmeticItem item)
        {
            if (item == null) return;
            if (item.materialOverride == null) return;

            activeSuitMaterial = item.materialOverride;
            SetFirstMaterial(playerRenderer, activeSuitMaterial);
        }

        /// <summary>Sets the grapple head renderer's first material slot to item.materialOverride.</summary>
        public void ApplyGrappleSkin(CosmeticItem item)
        {
            if (item == null) return;
            if (item.materialOverride == null) return;

            activeGrappleSkinMaterial = item.materialOverride;
            SetFirstMaterial(grappleRenderer, activeGrappleSkinMaterial);
        }

        /// <summary>Sets the RopeSimulator LineRenderer gradient colors to item.primaryColor.</summary>
        public void ApplyRopeColor(CosmeticItem item)
        {
            if (item == null) return;

            activeRopeColor = item.primaryColor;

            if (ropeSimulator == null) return;

            LineRenderer lr = ropeSimulator.GetComponent<LineRenderer>();
            if (lr == null) return;

            lr.startColor = activeRopeColor;
            lr.endColor   = activeRopeColor;
        }

        /// <summary>Destroys previous trail instance and instantiates item.particleEffectPrefab as a child of the player.</summary>
        public void ApplyParticleTrail(CosmeticItem item)
        {
            // Destroy old trail
            if (activeParticleTrailInstance != null)
            {
                Destroy(activeParticleTrailInstance);
                activeParticleTrailInstance = null;
            }

            if (item == null) return;
            if (item.trailParticleSystemPrefab == null) return;

            Transform parent = playerTransform != null ? playerTransform : transform;
            activeParticleTrailInstance = Instantiate(item.trailParticleSystemPrefab.gameObject, parent);
            activeParticleTrailInstance.transform.localPosition = Vector3.zero;
            activeParticleTrailInstance.transform.localRotation = Quaternion.identity;
        }

        /// <summary>Applies all four cosmetic types from the supplied loadout in one call.</summary>
        public void ApplyLoadout(CosmeticLoadout loadout)
        {
            if (loadout == null) return;

            currentLoadout = loadout;

            ApplyCosmeticById(loadout.suitId,          CosmeticType.Suit);
            ApplyCosmeticById(loadout.grappleSkinId,   CosmeticType.GrappleSkin);
            ApplyCosmeticById(loadout.ropeColorId,     CosmeticType.RopeColor);
            ApplyCosmeticById(loadout.particleTrailId, CosmeticType.ParticleTrail);
        }

        /// <summary>Persists the supplied loadout via PlayerPrefs.</summary>
        public void SaveLoadout(CosmeticLoadout loadout)
        {
            if (loadout == null) return;
            currentLoadout = loadout;
            string json = JsonUtility.ToJson(loadout);
            PlayerPrefs.SetString(SaveKey, json);
            PlayerPrefs.Save();
        }

        /// <summary>Loads and applies the previously saved loadout.</summary>
        public void LoadSavedLoadout()
        {
            if (!PlayerPrefs.HasKey(SaveKey)) return;

            try
            {
                string json = PlayerPrefs.GetString(SaveKey);
                CosmeticLoadout loadout = JsonUtility.FromJson<CosmeticLoadout>(json);
                if (loadout != null)
                    ApplyLoadout(loadout);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CosmeticSystem] Failed to load cosmetic loadout: {e.Message}");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void ApplyCosmeticById(string itemId, CosmeticType type)
        {
            if (string.IsNullOrEmpty(itemId)) return;

            CosmeticItem[] all = Resources.LoadAll<CosmeticItem>("Cosmetics");
            foreach (CosmeticItem item in all)
            {
                if (item.GetId() == itemId && item.itemType == type)
                {
                    ApplyItem(item);
                    return;
                }
            }
        }

        private void ApplyItem(CosmeticItem item)
        {
            switch (item.itemType)
            {
                case CosmeticType.Suit:          ApplySuit(item);         break;
                case CosmeticType.GrappleSkin:   ApplyGrappleSkin(item);  break;
                case CosmeticType.RopeColor:     ApplyRopeColor(item);    break;
                case CosmeticType.ParticleTrail: ApplyParticleTrail(item);break;
            }
        }

        private static void SetFirstMaterial(Renderer renderer, Material mat)
        {
            if (renderer == null || mat == null) return;
            Material[] mats = renderer.sharedMaterials;
            if (mats.Length == 0) return;
            mats[0] = mat;
            renderer.sharedMaterials = mats;
        }
    }
}
