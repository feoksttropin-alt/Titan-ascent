#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using TitanAscent.Environment;

namespace TitanAscent.Scene
{
    /// <summary>
    /// Generates placeholder geometry for a zone to enable early playtesting
    /// without final art.  Accessible via the TitanAscent menu in the Unity editor.
    /// </summary>
    public static class ZoneBlockout
    {
        // ------------------------------------------------------------------
        // Surface type colour map
        // ------------------------------------------------------------------

        private static readonly Dictionary<SurfaceType, Color> SurfaceColors = new Dictionary<SurfaceType, Color>
        {
            { SurfaceType.ScaleArmor,   new Color(0.45f, 0.45f, 0.45f) },          // grey
            { SurfaceType.BoneRidge,    new Color(0.88f, 0.85f, 0.78f) },           // off-white
            { SurfaceType.MuscleSkin,   new Color(0.50f, 0.08f, 0.08f) },           // dark red
            { SurfaceType.WingMembrane, new Color(0.30f, 0.45f, 0.75f, 0.55f) },   // translucent blue
            { SurfaceType.CrystalSurface, new Color(0.75f, 0.88f, 1.00f) }         // light blue-white
        };

        // ------------------------------------------------------------------
        // Zone configurations
        // ------------------------------------------------------------------

        private struct ZoneConfig
        {
            public string      name;
            public float       baseHeight;
            public float       heightRange;
            public SurfaceType surfaceType;
        }

        private static readonly ZoneConfig[] ZoneConfigs = new ZoneConfig[]
        {
            new ZoneConfig { name = "TailBasin",       baseHeight = 0f,    heightRange = 800f,   surfaceType = SurfaceType.ScaleArmor   },
            new ZoneConfig { name = "TailSpires",      baseHeight = 800f,  heightRange = 1000f,  surfaceType = SurfaceType.ScaleArmor   },
            new ZoneConfig { name = "HindLegValley",   baseHeight = 1800f, heightRange = 1200f,  surfaceType = SurfaceType.BoneRidge    },
            new ZoneConfig { name = "WingRoot",        baseHeight = 3000f, heightRange = 1200f,  surfaceType = SurfaceType.WingMembrane },
            new ZoneConfig { name = "SpineRidge",      baseHeight = 4200f, heightRange = 1300f,  surfaceType = SurfaceType.CrystalSurface},
            new ZoneConfig { name = "TheGraveyard",    baseHeight = 5500f, heightRange = 1000f,  surfaceType = SurfaceType.BoneRidge    },
            new ZoneConfig { name = "UpperBackStorm",  baseHeight = 6500f, heightRange = 1300f,  surfaceType = SurfaceType.MuscleSkin   },
            new ZoneConfig { name = "TheNeck",         baseHeight = 7800f, heightRange = 1200f,  surfaceType = SurfaceType.MuscleSkin   },
            new ZoneConfig { name = "TheCrown",        baseHeight = 9000f, heightRange = 1000f,  surfaceType = SurfaceType.CrystalSurface}
        };

        // ------------------------------------------------------------------
        // Menu items
        // ------------------------------------------------------------------

        [MenuItem("TitanAscent/Generate Zone Blockout")]
        public static void MenuGenerateZoneBlockout()
        {
            int zoneIndex = EditorUtility.DisplayDialogComplex(
                "Generate Zone Blockout",
                "Select zone to generate (0–8).\n\n" +
                "0: Tail Basin  1: Tail Spires  2: Hind Leg Valley\n" +
                "3: Wing Root   4: Spine Ridge  5: The Graveyard\n" +
                "6: Upper Back  7: The Neck     8: The Crown",
                "Zones 0–4", "Zones 5–8", "Cancel");

            if (zoneIndex == 2) return; // Cancel

            if (zoneIndex == 0)
            {
                // Show a second dialog to pick exact zone 0-4
                int zone = PickZoneDialog(0, 4);
                if (zone < 0) return;
                GenerateZone(zone);
            }
            else
            {
                int zone = PickZoneDialog(5, 8);
                if (zone < 0) return;
                GenerateZone(zone);
            }
        }

        [MenuItem("TitanAscent/Clear Zone Blockout")]
        public static void MenuClearZoneBlockout()
        {
            if (!EditorUtility.DisplayDialog(
                "Clear Zone Blockout",
                "Destroy all blockout geometry (GameObjects tagged 'blockout')?\n" +
                "This action supports Undo.",
                "Clear", "Cancel"))
                return;

            ClearAllBlockout();
        }

        // ------------------------------------------------------------------
        // Core generation
        // ------------------------------------------------------------------

        public static void GenerateZone(int zoneIndex)
        {
            if (zoneIndex < 0 || zoneIndex >= ZoneConfigs.Length)
            {
                Debug.LogError($"[ZoneBlockout] Zone index {zoneIndex} out of range (0–{ZoneConfigs.Length - 1}).");
                return;
            }

            ZoneConfig cfg = ZoneConfigs[zoneIndex];

            GameObject zoneRoot = new GameObject($"Blockout_Zone{zoneIndex}_{cfg.name}");
            zoneRoot.tag = "blockout";
            Undo.RegisterCreatedObjectUndo(zoneRoot, "Generate Zone Blockout");

            List<GameObject> pieces = BuildZoneGeometry(zoneIndex, cfg, zoneRoot.transform);

            // Assign material and SurfaceAnchorPoint to each piece
            Material mat = CreateBlockoutMaterial(cfg.surfaceType);

            foreach (GameObject piece in pieces)
            {
                Renderer rend = piece.GetComponent<Renderer>();
                if (rend != null)
                    rend.sharedMaterial = mat;

                // Add SurfaceAnchorPoint so the anchor placer / validators can work
                if (piece.GetComponent<SurfaceAnchorPoint>() == null)
                    piece.AddComponent<SurfaceAnchorPoint>();

                // Add collider if absent
                if (piece.GetComponent<Collider>() == null)
                    piece.AddComponent<MeshCollider>();

                piece.tag = "TitanSurface";
            }

            Debug.Log($"[ZoneBlockout] Generated {pieces.Count} pieces for Zone {zoneIndex} ({cfg.name}).");
            Selection.activeGameObject = zoneRoot;
        }

        private static List<GameObject> BuildZoneGeometry(int zoneIndex, ZoneConfig cfg, Transform parent)
        {
            var pieces = new List<GameObject>();

            switch (zoneIndex)
            {
                case 0: // Tail Basin — wide flat cubes, gentle incline
                    pieces.AddRange(BuildTailBasin(cfg, parent));
                    break;

                case 1: // Tail Spires — tall thin cylinders grouped as spikes
                    pieces.AddRange(BuildTailSpires(cfg, parent));
                    break;

                case 2: // Hind Leg Valley — wide gaps between angled platforms
                    pieces.AddRange(BuildHindLegValley(cfg, parent));
                    break;

                case 3: // Wing Root — platforms at various angles
                    pieces.AddRange(BuildWingRoot(cfg, parent));
                    break;

                case 4: // Spine Ridge — narrow elongated boxes along ridge
                    pieces.AddRange(BuildSpineRidge(cfg, parent));
                    break;

                default: // Zones 5–8 — increasingly sparse and angled
                    pieces.AddRange(BuildSparseAngled(zoneIndex, cfg, parent));
                    break;
            }

            return pieces;
        }

        // ------------------------------------------------------------------
        // Zone-specific geometry builders
        // ------------------------------------------------------------------

        private static List<GameObject> BuildTailBasin(ZoneConfig cfg, Transform parent)
        {
            var pieces = new List<GameObject>();
            int columns = 5;
            int rows    = 6;
            float tileW = 12f;
            float tileD = 10f;

            for (int c = 0; c < columns; c++)
            {
                for (int r = 0; r < rows; r++)
                {
                    float x = (c - columns / 2) * tileW;
                    float z = r * tileD;
                    float y = cfg.baseHeight + r * (cfg.heightRange / rows);

                    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.transform.SetParent(parent);
                    cube.transform.localPosition = new Vector3(x, y, z);
                    cube.transform.localScale    = new Vector3(tileW - 0.5f, 1.2f, tileD - 0.5f);
                    cube.name = $"TailBasin_Scale_{c}_{r}";
                    pieces.Add(cube);
                }
            }
            return pieces;
        }

        private static List<GameObject> BuildTailSpires(ZoneConfig cfg, Transform parent)
        {
            var pieces = new List<GameObject>();
            int spikeCount = 14;

            for (int i = 0; i < spikeCount; i++)
            {
                float t  = (float)i / spikeCount;
                float x  = Mathf.Sin(t * Mathf.PI * 2f) * 10f + Random.Range(-3f, 3f);
                float z  = Mathf.Cos(t * Mathf.PI * 2f) * 8f  + Random.Range(-3f, 3f);
                float h  = Random.Range(8f, 25f);
                float y  = cfg.baseHeight + Random.Range(0f, cfg.heightRange * 0.8f);

                GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cyl.transform.SetParent(parent);
                cyl.transform.localPosition = new Vector3(x, y, z);
                cyl.transform.localScale    = new Vector3(1.2f, h * 0.5f, 1.2f);
                cyl.name = $"TailSpire_{i}";
                pieces.Add(cyl);
            }
            return pieces;
        }

        private static List<GameObject> BuildHindLegValley(ZoneConfig cfg, Transform parent)
        {
            var pieces = new List<GameObject>();
            float[] xPositions = { -18f, -8f, 5f, 16f };
            int platforms = 8;

            foreach (float x in xPositions)
            {
                for (int p = 0; p < platforms; p++)
                {
                    float y   = cfg.baseHeight + (p / (float)platforms) * cfg.heightRange;
                    float z   = p * 6f;
                    float tilt = Random.Range(-20f, 20f);

                    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.transform.SetParent(parent);
                    cube.transform.localPosition = new Vector3(x + Random.Range(-2f, 2f), y, z);
                    cube.transform.localScale    = new Vector3(7f, 1f, 5f);
                    cube.transform.localRotation = Quaternion.Euler(tilt * 0.5f, 0f, tilt);
                    cube.name = $"HindLeg_Platform_{p}_x{x}";
                    pieces.Add(cube);
                }
            }
            return pieces;
        }

        private static List<GameObject> BuildWingRoot(ZoneConfig cfg, Transform parent)
        {
            var pieces = new List<GameObject>();
            int count = 12;

            for (int i = 0; i < count; i++)
            {
                float t     = (float)i / count;
                float angle = t * 140f - 70f;
                float r     = Random.Range(6f, 15f);
                float x     = Mathf.Sin(angle * Mathf.Deg2Rad) * r;
                float z     = Mathf.Cos(angle * Mathf.Deg2Rad) * r;
                float y     = cfg.baseHeight + t * cfg.heightRange;

                Vector3 scale;
                PrimitiveType prim;

                if (i % 3 == 0)
                {
                    prim  = PrimitiveType.Cube;
                    scale = new Vector3(6f, 0.8f, 8f);
                }
                else if (i % 3 == 1)
                {
                    prim  = PrimitiveType.Cylinder;
                    scale = new Vector3(1.5f, 4f, 1.5f);
                }
                else
                {
                    prim  = PrimitiveType.Cube;
                    scale = new Vector3(9f, 0.6f, 4f);
                }

                GameObject piece = GameObject.CreatePrimitive(prim);
                piece.transform.SetParent(parent);
                piece.transform.localPosition = new Vector3(x, y, z);
                piece.transform.localScale    = scale;
                piece.transform.localRotation = Quaternion.Euler(0f, angle, Random.Range(-25f, 25f));
                piece.name = $"WingRoot_Piece_{i}";
                pieces.Add(piece);
            }
            return pieces;
        }

        private static List<GameObject> BuildSpineRidge(ZoneConfig cfg, Transform parent)
        {
            var pieces = new List<GameObject>();
            int ridgeSegments = 10;

            for (int i = 0; i < ridgeSegments; i++)
            {
                float t = (float)i / ridgeSegments;
                float y = cfg.baseHeight + t * cfg.heightRange;
                float z = i * 4f;
                float tiltX = Random.Range(-8f, 8f);

                // Main ridge piece — long narrow box
                GameObject ridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ridge.transform.SetParent(parent);
                ridge.transform.localPosition = new Vector3(0f, y, z);
                ridge.transform.localScale    = new Vector3(2f, 1.5f, 5f);
                ridge.transform.localRotation = Quaternion.Euler(tiltX, 0f, 0f);
                ridge.name = $"SpineRidge_Main_{i}";
                pieces.Add(ridge);

                // Side crystal spurs
                for (int side = -1; side <= 1; side += 2)
                {
                    if (Random.value > 0.6f) continue;

                    GameObject spur = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    spur.transform.SetParent(parent);
                    spur.transform.localPosition = new Vector3(side * 3f, y + 0.5f, z);
                    spur.transform.localScale    = new Vector3(0.8f, 3f, 0.8f);
                    spur.transform.localRotation = Quaternion.Euler(0f, 0f, side * Random.Range(20f, 45f));
                    spur.name = $"SpineRidge_Spur_{i}_side{side}";
                    pieces.Add(spur);
                }
            }
            return pieces;
        }

        private static List<GameObject> BuildSparseAngled(int zoneIndex, ZoneConfig cfg, Transform parent)
        {
            var pieces = new List<GameObject>();

            // Sparseness increases with zone index
            float sparseness = Mathf.InverseLerp(5, 8, zoneIndex); // 0 at zone 5, 1 at zone 8
            int   count      = Mathf.RoundToInt(Mathf.Lerp(10f, 4f, sparseness));

            for (int i = 0; i < count; i++)
            {
                float t     = (float)i / count;
                float y     = cfg.baseHeight + t * cfg.heightRange;
                float xOff  = Random.Range(-12f, 12f);
                float zOff  = Random.Range(-8f,  8f);
                float tiltZ = Random.Range(-40f, 40f) * sparseness;
                float tiltX = Random.Range(-20f, 20f) * sparseness;

                Vector3 scale = new Vector3(
                    Mathf.Lerp(8f, 4f, sparseness),
                    Mathf.Lerp(1f, 0.7f, sparseness),
                    Mathf.Lerp(6f, 3f, sparseness));

                GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.transform.SetParent(parent);
                piece.transform.localPosition = new Vector3(xOff, y, zOff);
                piece.transform.localScale    = scale;
                piece.transform.localRotation = Quaternion.Euler(tiltX, Random.Range(0f, 360f), tiltZ);
                piece.name = $"Zone{zoneIndex}_Piece_{i}";
                pieces.Add(piece);
            }
            return pieces;
        }

        // ------------------------------------------------------------------
        // Material creation
        // ------------------------------------------------------------------

        private static Material CreateBlockoutMaterial(SurfaceType surfaceType)
        {
            Color color = SurfaceColors.ContainsKey(surfaceType)
                ? SurfaceColors[surfaceType]
                : Color.grey;

            // Try URP Lit first, fall back to Standard
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = new Material(shader);
            mat.name = $"Blockout_{surfaceType}";

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);

            // Wing membrane should be semi-transparent
            if (surfaceType == SurfaceType.WingMembrane)
            {
                mat.SetFloat("_Surface", 1f);      // Transparent in URP
                mat.SetFloat("_Mode", 3f);          // Transparent in Standard
                mat.renderQueue = 3000;
            }

            return mat;
        }

        // ------------------------------------------------------------------
        // Clear
        // ------------------------------------------------------------------

        public static void ClearAllBlockout()
        {
            GameObject[] tagged = GameObject.FindGameObjectsWithTag("blockout");
            int count = 0;
            foreach (GameObject go in tagged)
            {
                if (go == null) continue;
                Undo.DestroyObjectImmediate(go);
                count++;
            }
            Debug.Log($"[ZoneBlockout] Cleared {count} blockout root(s).");
        }

        // ------------------------------------------------------------------
        // Dialog helpers
        // ------------------------------------------------------------------

        private static int PickZoneDialog(int min, int max)
        {
            string input = "0";
            // Unity doesn't have a text-input dialog so we use a sequence of DisplayDialogComplex
            // to select the zone. Build button labels.
            var labels = new List<string>();
            for (int i = min; i <= max; i++)
                labels.Add($"Zone {i}: {ZoneConfigs[i].name}");

            // Only three button slots — iterate in pairs
            for (int i = min; i <= max; i++)
            {
                string btn2 = (i + 1 <= max) ? $"Zone {i + 1}" : "";
                string btn3 = "Cancel";

                int choice = EditorUtility.DisplayDialogComplex(
                    "Pick Zone",
                    $"Generate zone {i} ({ZoneConfigs[i].name})" +
                        (i + 1 <= max ? $" or {i + 1} ({ZoneConfigs[i + 1].name})?" : "?"),
                    $"Zone {i}",
                    btn2 != "" ? btn2 : "Cancel",
                    btn3);

                if (choice == 0) return i;
                if (choice == 1 && btn2 != "" && i + 1 <= max) return i + 1;
                if (choice == 2 || (choice == 1 && btn2 == "")) return -1; // Cancel
                i++; // skip the next since we handled it
            }

            return -1;
        }
    }
}
#endif
