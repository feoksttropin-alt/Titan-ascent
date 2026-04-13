#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TitanAscent.Environment;
// SurfaceType is in TitanAscent.Environment

namespace TitanAscent.Scene
{
    public static class ClimbAreaBuilder
    {
        [MenuItem("TitanAscent/Generate Anchor Grid")]
        public static void GenerateAnchorGrid()
        {
            AnchorGridWindow.ShowWindow();
        }

        [MenuItem("TitanAscent/Validate All Anchors")]
        public static void ValidateAllAnchors()
        {
            var validator = Object.FindFirstObjectByType<Debug.AnchorValidator>();
            if (validator == null)
            {
                EditorUtility.DisplayDialog("Validate Anchors", "No AnchorValidator found in scene.\nAdd one to a GameObject first.", "OK");
                return;
            }
            validator.ValidateRoute();
            EditorUtility.DisplayDialog("Anchor Validation Complete", "Results logged to Console.", "OK");
        }

        [MenuItem("TitanAscent/Clear All Anchors")]
        public static void ClearAllAnchors()
        {
            if (!EditorUtility.DisplayDialog("Clear All Anchors",
                "This will destroy all SurfaceAnchorPoint GameObjects. Undo is supported.", "Clear", "Cancel"))
                return;

            var anchors = Object.FindObjectsOfType<SurfaceAnchorPoint>();
            foreach (var a in anchors)
            {
                Undo.DestroyObjectImmediate(a.gameObject);
            }
            UnityEngine.Debug.Log($"[ClimbAreaBuilder] Cleared {anchors.Length} anchor points.");
        }

        public static void SpawnAnchorGrid(float startHeight, float endHeight,
            float horizontalSpread, float spacing, SurfaceType surfaceType)
        {
            GameObject parent = GameObject.Find("AnchorPoints");
            if (parent == null) parent = new GameObject("AnchorPoints");
            Undo.RegisterCreatedObjectUndo(parent, "Create Anchor Parent");

            int count = 0;
            float y = startHeight;
            while (y <= endHeight)
            {
                float xRange = horizontalSpread * 0.5f;
                float x = -xRange;
                while (x <= xRange)
                {
                    float jitterX = Random.Range(-spacing * 0.3f, spacing * 0.3f);
                    float jitterY = Random.Range(-spacing * 0.3f, spacing * 0.3f);
                    Vector3 pos = new Vector3(x + jitterX, y + jitterY, 0f);

                    GameObject go = new GameObject($"Anchor_H{y:000}_X{x:00}");
                    go.transform.position = pos;
                    go.transform.SetParent(parent.transform);

                    var anchor = go.AddComponent<SurfaceAnchorPoint>();
                    Undo.RegisterCreatedObjectUndo(go, "Create Anchor");

                    count++;
                    x += spacing;
                }
                y += spacing;
            }

            UnityEngine.Debug.Log($"[ClimbAreaBuilder] Created {count} anchors from {startHeight}m to {endHeight}m.");
            Selection.activeGameObject = parent;
        }
    }

    public class AnchorGridWindow : EditorWindow
    {
        private float startHeight = 0f;
        private float endHeight = 800f;
        private float horizontalSpread = 20f;
        private float spacing = 5f;
        private SurfaceType surfaceType = SurfaceType.ScaleArmor;

        public static void ShowWindow()
        {
            GetWindow<AnchorGridWindow>("Generate Anchor Grid");
        }

        private void OnGUI()
        {
            GUILayout.Label("Anchor Grid Generator", EditorStyles.boldLabel);
            startHeight = EditorGUILayout.FloatField("Start Height (m)", startHeight);
            endHeight = EditorGUILayout.FloatField("End Height (m)", endHeight);
            horizontalSpread = EditorGUILayout.FloatField("Horizontal Spread (m)", horizontalSpread);
            spacing = EditorGUILayout.FloatField("Anchor Spacing (m)", spacing);
            surfaceType = (SurfaceType)EditorGUILayout.EnumPopup("Surface Type", surfaceType);

            GUILayout.Space(8f);
            float height = endHeight - startHeight;
            float cols = Mathf.Floor(horizontalSpread / spacing) + 1;
            float rows = Mathf.Floor(height / spacing) + 1;
            EditorGUILayout.HelpBox($"Estimated anchors: ~{cols * rows:F0}", MessageType.Info);

            GUILayout.Space(4f);
            if (GUILayout.Button("Generate"))
                ClimbAreaBuilder.SpawnAnchorGrid(startHeight, endHeight, horizontalSpread, spacing, surfaceType);
        }
    }
}
#endif
