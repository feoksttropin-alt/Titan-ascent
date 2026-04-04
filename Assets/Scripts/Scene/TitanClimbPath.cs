using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TitanAscent.Environment;

namespace TitanAscent.Scene
{
    [System.Serializable]
    public class RouteSegment
    {
        public string segmentName;
        public float startHeight;
        public float endHeight;
        public float estimatedClimbTime; // seconds
        public SurfaceType primarySurface;
        public float windStrength;
        public bool hasMajorFallRisk;
        public bool hasRecoveryOpportunity;
        public string primaryLandmarkDescription;
    }

    public class TitanClimbPath : MonoBehaviour
    {
        public List<RouteSegment> segments = new List<RouteSegment>();

        private void Awake()
        {
            BuildRoute();
        }

        private void BuildRoute()
        {
            segments.Clear();

            // Zone 1 – Tail Basin (0–800m)
            Add("Tail Basin – Lower Plates",      0f,    400f,  240f, SurfaceType.ScaleArmor,   0.1f, false, false, "Giant scale plates, grapple tutorial");
            Add("Tail Basin – Upper Lip",         400f,  800f,  240f, SurfaceType.ScaleArmor,   0.15f, false, true,  "Wide lip where tail meets hind leg");

            // Zone 2 – Tail Spires (800–1800m)
            Add("Tail Spires – Lower Spikes",     800f,  1300f, 360f, SurfaceType.BoneRidge,    0.2f, false, false, "First bone spines jutting from tail");
            Add("Tail Spires – The Needle",       1300f, 1800f, 360f, SurfaceType.BoneRidge,    0.25f, true,  false, "Narrow bone needle — first big fall risk");

            // Zone 3 – Hind Leg Valley (1800–3000m)
            Add("Hind Leg – Muscle Flats",        1800f, 2200f, 420f, SurfaceType.MuscleSkin,   0.3f, true,  false, "Slippery muscle skin between leg joints");
            Add("Hind Leg – Tendon Run",          2200f, 2600f, 420f, SurfaceType.MuscleSkin,   0.35f, true,  true,  "Exposed tendons act as recovery lines");
            Add("Hind Leg – Knee Cap Ledge",      2600f, 3000f, 420f, SurfaceType.BoneRidge,    0.3f, false, false, "Large bony knee — first visual milestone");

            // Zone 4 – Wing Root (3000–4200m)
            Add("Wing Root – Shoulder Scales",    3000f, 3600f, 480f, SurfaceType.ScaleArmor,   0.4f, false, true,  "Moving wing muscles shift surface slightly");
            Add("Wing Root – Wing Junction",      3600f, 4200f, 480f, SurfaceType.WingMembrane, 0.45f, true,  true,  "First membrane surface — flexible, tricky");

            // Zone 5 – Spine Ridge (4200–5500m)
            Add("Spine Ridge – Lower Vertebrae",  4200f, 4850f, 540f, SurfaceType.BoneRidge,    0.7f, true,  false, "Narrow bone ridge with sidewind");
            Add("Spine Ridge – Crest",            4850f, 5500f, 540f, SurfaceType.CrystalSurface,0.8f,true,  false, "Crystal growths along spine — thin anchors");

            // Zone 6 – The Graveyard (5500–6500m)
            Add("Graveyard – Weapon Field",       5500f, 6000f, 480f, SurfaceType.BoneRidge,    0.5f, false, false, "Ancient weapons and ruins embedded in titan");
            Add("Graveyard – Skeleton Row",       6000f, 6500f, 480f, SurfaceType.ScaleArmor,   0.55f, true,  true,  "Previous climbers' gear visible — recovery lines");

            // Zone 7 – Upper Back Storm (6500–7800m)
            Add("Storm – Lightning Shelf",        6500f, 7150f, 600f, SurfaceType.CrystalSurface,1.5f, true,  false, "Extreme horizontal wind, crystal anchors");
            Add("Storm – Eye of the Storm",       7150f, 7800f, 600f, SurfaceType.MuscleSkin,   2.0f, true,  false, "Most exposed section, highest fall risk");

            // Zone 8 – The Neck (7800–9000m)
            Add("Neck – Lower Throat",            7800f, 8400f, 540f, SurfaceType.MuscleSkin,   0.8f, true,  true,  "Breathing contractions shift surfaces");
            Add("Neck – Jaw Approach",            8400f, 9000f, 540f, SurfaceType.ScaleArmor,   0.9f, true,  false, "Scales tighten — small anchors, precise timing");

            // Zone 9 – The Crown (9000–10000m)
            Add("The Crown – Final Ascent",       9000f, 10000f,720f, SurfaceType.CrystalSurface,1.2f, true,  false, "Crystal crown — beautiful, merciless, no recovery");
        }

        private void Add(string name, float start, float end, float time, SurfaceType surface,
                         float wind, bool fallRisk, bool recovery, string landmark)
        {
            segments.Add(new RouteSegment
            {
                segmentName = name, startHeight = start, endHeight = end,
                estimatedClimbTime = time, primarySurface = surface,
                windStrength = wind, hasMajorFallRisk = fallRisk,
                hasRecoveryOpportunity = recovery, primaryLandmarkDescription = landmark
            });
        }

        public RouteSegment GetSegmentAtHeight(float height) =>
            segments.FirstOrDefault(s => height >= s.startHeight && height < s.endHeight);

        public float GetEstimatedTotalClimbTime() =>
            segments.Sum(s => s.estimatedClimbTime);

        public float GetRemainingTime(float currentHeight) =>
            segments.Where(s => s.endHeight > currentHeight).Sum(s =>
            {
                if (s.startHeight >= currentHeight) return s.estimatedClimbTime;
                float ratio = (s.endHeight - currentHeight) / (s.endHeight - s.startHeight);
                return s.estimatedClimbTime * ratio;
            });

        public List<float> GetMajorFallRisksAbove(float height) =>
            segments.Where(s => s.hasMajorFallRisk && s.startHeight > height)
                    .Select(s => s.startHeight).ToList();

        public void LogFullRouteReport()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== TITAN CLIMB ROUTE ===");
            sb.AppendLine($"Total estimated time: {GetEstimatedTotalClimbTime() / 60f:F0} min");
            sb.AppendLine($"Segments: {segments.Count}");
            sb.AppendLine();
            sb.AppendLine($"{"Segment",-36} {"Start",6} {"End",6} {"Time",6} {"Wind",5} {"Fall?",6} {"Recov?",7}");
            sb.AppendLine(new string('-', 80));
            foreach (var s in segments)
                sb.AppendLine($"{s.segmentName,-36} {s.startHeight,6:F0} {s.endHeight,6:F0} {s.estimatedClimbTime/60f,5:F1}m {s.windStrength,5:F1} {(s.hasMajorFallRisk?"YES":"   "),6} {(s.hasRecoveryOpportunity?"YES":"   "),7}");
            UnityEngine.Debug.Log(sb.ToString());
        }
    }
}
