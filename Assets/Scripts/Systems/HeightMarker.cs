using UnityEngine;

namespace TitanAscent.Systems
{
    public class HeightMarker : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private GameObject markerLinePrefab;
        [SerializeField] private Color markerColor = new Color(1f, 0.85f, 0.2f, 0.7f);
        [SerializeField] private float markerWidth = 50f;

        [Header("References")]
        [SerializeField] private FallTracker fallTracker;

        private GameObject currentMarker;
        private float lastBestHeight = 0f;
        private LineRenderer markerLine;

        private void Start()
        {
            if (fallTracker != null)
                fallTracker.OnNewHeightRecord.AddListener(UpdateMarker);

            SpawnMarker(0f);
        }

        private void UpdateMarker(float newHeight)
        {
            if (newHeight <= lastBestHeight) return;
            lastBestHeight = newHeight;

            if (currentMarker == null)
                SpawnMarker(newHeight);
            else
                MoveMarker(newHeight);
        }

        private void SpawnMarker(float height)
        {
            if (markerLinePrefab != null)
            {
                currentMarker = Instantiate(markerLinePrefab, new Vector3(0f, height, 0f), Quaternion.identity);
                markerLine = currentMarker.GetComponent<LineRenderer>();
            }
            else
            {
                currentMarker = new GameObject("BestHeightMarker");
                currentMarker.transform.position = new Vector3(0f, height, 0f);
                markerLine = currentMarker.AddComponent<LineRenderer>();
                ConfigureLineRenderer(markerLine);
            }

            MoveMarker(height);
        }

        private void MoveMarker(float height)
        {
            if (currentMarker == null) return;
            currentMarker.transform.position = new Vector3(0f, height, 0f);

            if (markerLine != null)
            {
                markerLine.SetPosition(0, new Vector3(-markerWidth * 0.5f, height, 0f));
                markerLine.SetPosition(1, new Vector3(markerWidth * 0.5f, height, 0f));
            }
        }

        private void ConfigureLineRenderer(LineRenderer lr)
        {
            lr.positionCount = 2;
            lr.startWidth = 0.15f;
            lr.endWidth = 0.15f;
            lr.startColor = markerColor;
            lr.endColor = markerColor;
            lr.useWorldSpace = true;

            Material mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = markerColor;
            lr.material = mat;
        }
    }
}
