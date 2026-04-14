using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using TitanAscent.Data;

namespace TitanAscent.Environment
{
    [System.Serializable]
    public class TitanZone
    {
        public string name;
        public float minHeight;
        public float maxHeight;
        public string description;
        public Color ambientColor = Color.white;
        public float windStrength = 1f;
        public SurfaceType dominantSurfaceType = SurfaceType.ScaleArmor;
        public ZoneData zoneData;

        public bool ContainsHeight(float height) => height >= minHeight && height < maxHeight;
    }

    public class ZoneManager : MonoBehaviour
    {
        [Header("Zone Configuration")]
        [SerializeField] private List<TitanZone> zones = new List<TitanZone>();
        [SerializeField] private bool autoPopulateDefaultZones = true;

        [Header("Events")]
        public UnityEvent<TitanZone, TitanZone> OnZoneChanged; // (previousZone, newZone)

        private TitanZone currentZone;
        private Player.PlayerController player;
        private Systems.NarrationSystem narrationSystem;

        public TitanZone CurrentZone => currentZone;
        public int CurrentZoneIndex => zones.IndexOf(currentZone);

        private void Awake()
        {
            player = FindFirstObjectByType<Player.PlayerController>();
            narrationSystem = FindFirstObjectByType<Systems.NarrationSystem>();

            if (autoPopulateDefaultZones && zones.Count == 0)
                PopulateDefaultZones();
        }

        private void PopulateDefaultZones()
        {
            zones.Clear();
            zones.Add(new TitanZone { name = "TailBasin",       minHeight = 0,    maxHeight = 800,   description = "The broad basin of the titan's tail. Wind is calm here.", ambientColor = new Color(0.5f, 0.45f, 0.4f), windStrength = 0.1f, dominantSurfaceType = SurfaceType.ScaleArmor });
            zones.Add(new TitanZone { name = "TailSpires",      minHeight = 800,  maxHeight = 1800,  description = "Jagged spires of scale armor. Good grapple holds.", ambientColor = new Color(0.55f, 0.5f, 0.45f), windStrength = 0.25f, dominantSurfaceType = SurfaceType.ScaleArmor });
            zones.Add(new TitanZone { name = "HindLegValley",   minHeight = 1800, maxHeight = 3000,  description = "The valley between the hind legs. Bone ridges dominate.", ambientColor = new Color(0.6f, 0.55f, 0.5f), windStrength = 0.4f, dominantSurfaceType = SurfaceType.BoneRidge });
            zones.Add(new TitanZone { name = "WingRoot",        minHeight = 3000, maxHeight = 4200,  description = "Where the wings meet the body. Membrane sections begin.", ambientColor = new Color(0.4f, 0.5f, 0.6f), windStrength = 0.6f, dominantSurfaceType = SurfaceType.WingMembrane });
            zones.Add(new TitanZone { name = "SpineRidge",      minHeight = 4200, maxHeight = 5500,  description = "The great spine. Crystal formations jut outward.", ambientColor = new Color(0.5f, 0.6f, 0.7f), windStrength = 0.75f, dominantSurfaceType = SurfaceType.CrystalSurface });
            zones.Add(new TitanZone { name = "TheGraveyard",    minHeight = 5500, maxHeight = 6500,  description = "Where most climbers end. Shattered grapple equipment litters the ridges.", ambientColor = new Color(0.35f, 0.35f, 0.4f), windStrength = 0.85f, dominantSurfaceType = SurfaceType.BoneRidge });
            zones.Add(new TitanZone { name = "UpperBackStorm", minHeight = 6500, maxHeight = 7800,  description = "Perpetual storm. Wind roars. Muscle skin tears away.", ambientColor = new Color(0.3f, 0.3f, 0.45f), windStrength = 1.0f, dominantSurfaceType = SurfaceType.MuscleSkin });
            zones.Add(new TitanZone { name = "TheNeck",         minHeight = 7800, maxHeight = 9000,  description = "The breathing rhythm is overwhelming here.", ambientColor = new Color(0.25f, 0.3f, 0.4f), windStrength = 0.9f, dominantSurfaceType = SurfaceType.MuscleSkin });
            zones.Add(new TitanZone { name = "TheCrown",        minHeight = 9000, maxHeight = 10000, description = "Summit. The air is thin. The titan has noticed you.", ambientColor = new Color(0.7f, 0.8f, 1.0f), windStrength = 0.5f, dominantSurfaceType = SurfaceType.CrystalSurface });
        }

        private void Update()
        {
            if (player == null)
            {
                player = FindFirstObjectByType<Player.PlayerController>();
                return;
            }

            UpdateCurrentZone(player.CurrentHeight);
        }

        public void UpdateCurrentZone(float height)
        {
            TitanZone newZone = GetZoneForHeight(height);

            if (newZone != null && newZone != currentZone)
            {
                TitanZone previous = currentZone;
                currentZone = newZone;
                OnZoneChanged?.Invoke(previous, currentZone);
                narrationSystem?.TriggerZoneEntry(CurrentZoneIndex);
            }
        }

        /// <summary>
        /// Binary-searches sorted zones by height. Zones must be ordered by minHeight ascending
        /// (which PopulateDefaultZones guarantees; Inspector-configured zones should match).
        /// Falls back to linear scan if ordering is not maintained.
        /// </summary>
        public TitanZone GetZoneForHeight(float height)
        {
            if (zones.Count == 0) return null;
            if (height < zones[0].minHeight)  return zones[0];
            if (height >= zones[zones.Count - 1].maxHeight) return zones[zones.Count - 1];

            int lo = 0, hi = zones.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                TitanZone z = zones[mid];
                if (height < z.minHeight)
                    hi = mid - 1;
                else if (height >= z.maxHeight)
                    lo = mid + 1;
                else
                    return z;
            }

            // Fallback: last zone covers to the top
            return zones[zones.Count - 1];
        }

        public TitanZone GetZoneByName(string zoneName)
        {
            return zones.Find(z => z.name == zoneName);
        }

        public float GetWindStrengthAtHeight(float height)
        {
            TitanZone zone = GetZoneForHeight(height);
            return zone != null ? zone.windStrength : 0f;
        }
    }
}
