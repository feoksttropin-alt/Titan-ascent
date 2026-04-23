using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TitanAscent.Environment;

namespace TitanAscent.Debug
{
    /// <summary>
    /// Validates anchor point placement and visualizes connectivity gaps across the scene.
    /// </summary>
    public class AnchorValidator : MonoBehaviour
    {
        [Header("Validation Thresholds")]
        [SerializeField] private float wellConnectedDistance = 8f;
        [SerializeField] private float sparseDistance = 15f;

        // Runtime state
        private SurfaceAnchorPoint[] anchors = new SurfaceAnchorPoint[0];
        private AnchorConnectivity[] connectivityData = new AnchorConnectivity[0];
        private bool visualizationActive = false;

        public bool IsVisualizationActive => visualizationActive;

        private struct AnchorConnectivity
        {
            public SurfaceAnchorPoint anchor;
            public float nearestNeighborDistance;
            public ConnectivityStatus status;
        }

        private enum ConnectivityStatus
        {
            WellConnected,  // green
            Sparse,         // yellow
            Isolated        // red
        }

        public void ToggleVisualization()
        {
            visualizationActive = !visualizationActive;
            if (visualizationActive)
                RefreshAnchors();
        }

        public void ValidateRoute()
        {
            RefreshAnchors();

            if (anchors.Length == 0)
            {
                UnityEngine.Debug.LogWarning("[AnchorValidator] No SurfaceAnchorPoint objects found in scene.");
                return;
            }

            int total = anchors.Length;
            int isolated = 0;
            int sparse = 0;
            int wellConnected = 0;

            // Band analysis: 100m height bands from 0 to 10000m
            var bandCounts = new Dictionary<int, int>();

            foreach (AnchorConnectivity data in connectivityData)
            {
                switch (data.status)
                {
                    case ConnectivityStatus.Isolated:    isolated++;      break;
                    case ConnectivityStatus.Sparse:      sparse++;        break;
                    case ConnectivityStatus.WellConnected: wellConnected++; break;
                }

                int band = Mathf.FloorToInt(data.anchor.transform.position.y / 100f);
                if (!bandCounts.ContainsKey(band))
                    bandCounts[band] = 0;
                bandCounts[band]++;
            }

            StringBuilder report = new StringBuilder();
            report.AppendLine("=== ANCHOR VALIDATOR REPORT ===");
            report.AppendLine($"Total anchors:      {total}");
            report.AppendLine($"Well-connected:     {wellConnected} (nearest < {wellConnectedDistance}m)");
            report.AppendLine($"Sparse:             {sparse} (nearest {wellConnectedDistance}-{sparseDistance}m)");
            report.AppendLine($"Isolated:           {isolated} (nearest > {sparseDistance}m)");
            report.AppendLine();
            report.AppendLine("--- Per 100m Height Band Density ---");

            for (int band = 0; band <= 100; band++)
            {
                if (bandCounts.TryGetValue(band, out int count) && count > 0)
                {
                    float bandStart = band * 100f;
                    float bandEnd = bandStart + 100f;
                    report.AppendLine($"  [{bandStart:0000}-{bandEnd:0000}m]: {count} anchors");
                }
            }

            if (isolated > 0)
            {
                report.AppendLine();
                report.AppendLine("--- WARNING: Isolated Anchors (likely unreachable gaps) ---");
                foreach (AnchorConnectivity data in connectivityData)
                {
                    if (data.status == ConnectivityStatus.Isolated)
                    {
                        Vector3 pos = data.anchor.transform.position;
                        report.AppendLine(
                            $"  {data.anchor.name} at height {pos.y:0.0}m " +
                            $"(nearest neighbor: {data.nearestNeighborDistance:0.0}m away)");
                    }
                }
            }

            if (sparse > 0)
            {
                report.AppendLine();
                report.AppendLine("--- CAUTION: Sparse Anchors (may require perfect momentum) ---");
                foreach (AnchorConnectivity data in connectivityData)
                {
                    if (data.status == ConnectivityStatus.Sparse)
                    {
                        Vector3 pos = data.anchor.transform.position;
                        report.AppendLine(
                            $"  {data.anchor.name} at height {pos.y:0.0}m " +
                            $"(nearest neighbor: {data.nearestNeighborDistance:0.0}m away)");
                    }
                }
            }

            report.AppendLine("=== END REPORT ===");
            UnityEngine.Debug.Log(report.ToString());
        }

        private void RefreshAnchors()
        {
            anchors = FindObjectsByType<SurfaceAnchorPoint>(FindObjectsSortMode.None);
            connectivityData = new AnchorConnectivity[anchors.Length];

            for (int i = 0; i < anchors.Length; i++)
            {
                float nearestDist = float.MaxValue;

                for (int j = 0; j < anchors.Length; j++)
                {
                    if (i == j) continue;
                    float dist = Vector3.Distance(anchors[i].transform.position, anchors[j].transform.position);
                    if (dist < nearestDist)
                        nearestDist = dist;
                }

                ConnectivityStatus status;
                if (nearestDist <= wellConnectedDistance)
                    status = ConnectivityStatus.WellConnected;
                else if (nearestDist <= sparseDistance)
                    status = ConnectivityStatus.Sparse;
                else
                    status = ConnectivityStatus.Isolated;

                connectivityData[i] = new AnchorConnectivity
                {
                    anchor = anchors[i],
                    nearestNeighborDistance = nearestDist,
                    status = status
                };
            }
        }

        private void OnDrawGizmos()
        {
            if (!visualizationActive || connectivityData == null) return;

            foreach (AnchorConnectivity data in connectivityData)
            {
                if (data.anchor == null) continue;

                Color sphereColor;
                switch (data.status)
                {
                    case ConnectivityStatus.WellConnected: sphereColor = Color.green;  break;
                    case ConnectivityStatus.Sparse:        sphereColor = Color.yellow; break;
                    default:                               sphereColor = Color.red;    break;
                }

                Gizmos.color = sphereColor;
                Gizmos.DrawSphere(data.anchor.transform.position, 0.4f);
            }

            // Draw connections to nearest neighbor
            for (int i = 0; i < anchors.Length; i++)
            {
                if (anchors[i] == null) continue;

                SurfaceAnchorPoint nearest = null;
                float nearestDist = float.MaxValue;

                for (int j = 0; j < anchors.Length; j++)
                {
                    if (i == j || anchors[j] == null) continue;
                    float dist = Vector3.Distance(anchors[i].transform.position, anchors[j].transform.position);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearest = anchors[j];
                    }
                }

                if (nearest != null)
                {
                    Color lineColor;
                    switch (connectivityData[i].status)
                    {
                        case ConnectivityStatus.WellConnected: lineColor = new Color(0f, 1f, 0f, 0.5f);  break;
                        case ConnectivityStatus.Sparse:        lineColor = new Color(1f, 1f, 0f, 0.5f);  break;
                        default:                               lineColor = new Color(1f, 0f, 0f, 0.6f);  break;
                    }

                    Gizmos.color = lineColor;
                    Gizmos.DrawLine(anchors[i].transform.position, nearest.transform.position);
                }
            }
        }
    }
}
