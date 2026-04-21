using UnityEngine;
using System.Collections.Generic;

namespace TitanAscent.Environment
{
    public enum SurfaceType
    {
        ScaleArmor,
        BoneRidge,
        CrystalSurface,
        MuscleSkin,
        WingMembrane,
        None
    }

    [CreateAssetMenu(fileName = "SurfaceProperties", menuName = "TitanAscent/Surface Properties")]
    public class SurfacePropertiesData : ScriptableObject
    {
        [Header("Surface Identification")]
        public SurfaceType surfaceType;
        public string displayName;

        [Header("Friction & Grip")]
        [Range(0f, 2f)] public float frictionCoefficient = 0.6f;
        [Range(0f, 2f)] public float gripMultiplier = 1f;
        [Range(0f, 2f)] public float slideAcceleration = 1f;

        [Header("Grapple")]
        public bool isGrappleable = true;
        [Range(0f, 1f)] public float grappleHoldStrength = 1f;

        [Header("Visual")]
        public Color surfaceDebugColor = Color.white;
    }

    /// <summary>
    /// MonoBehaviour placed on surfaces to define their physical properties.
    /// References a SurfacePropertiesData ScriptableObject and exposes helper accessors.
    /// </summary>
    public class SurfaceProperties : MonoBehaviour
    {
        [SerializeField] private SurfacePropertiesData propertiesData;

        [Header("Override (leave blank to use ScriptableObject)")]
        [SerializeField] private bool useOverride = false;
        [SerializeField] private SurfaceType overrideType = SurfaceType.ScaleArmor;
        [Range(0f, 2f)] [SerializeField] private float overrideFriction = 0.6f;
        [Range(0f, 2f)] [SerializeField] private float overrideGripMultiplier = 1f;
        [SerializeField] private bool overrideIsGrappleable = true;
        [Range(0f, 1f)] [SerializeField] private float overrideGrappleHoldStrength = 1f;
        [Range(0f, 2f)] [SerializeField] private float overrideSlideAcceleration = 1f;

        // Global surface type registry — allows runtime lookup without component queries
        private static Dictionary<SurfaceType, SurfacePropertiesData> surfaceRegistry =
            new Dictionary<SurfaceType, SurfacePropertiesData>();

        /// <summary>
        /// Global friction multiplier applied on top of per-surface values.
        /// Used by ChallengeManager for UltraSlippery modifier.
        /// </summary>
        public static float GlobalFrictionMultiplier { get; private set; } = 1f;

        /// <summary>Sets the global friction multiplier (affects all SurfaceProperties instances).</summary>
        public static void SetGlobalFrictionMultiplier(float multiplier)
        {
            GlobalFrictionMultiplier = Mathf.Max(0f, multiplier);
        }

        public SurfaceType Type => useOverride ? overrideType : (propertiesData != null ? propertiesData.surfaceType : SurfaceType.ScaleArmor);
        public float FrictionCoefficient => useOverride ? overrideFriction : (propertiesData != null ? propertiesData.frictionCoefficient : 0.6f);
        public float GripMultiplier => useOverride ? overrideGripMultiplier : (propertiesData != null ? propertiesData.gripMultiplier : 1f);
        public bool IsGrappleable => useOverride ? overrideIsGrappleable : (propertiesData != null ? propertiesData.isGrappleable : true);
        public float GrappleHoldStrength => useOverride ? overrideGrappleHoldStrength : (propertiesData != null ? propertiesData.grappleHoldStrength : 1f);
        public float SlideAcceleration => useOverride ? overrideSlideAcceleration : (propertiesData != null ? propertiesData.slideAcceleration : 1f);

        private void Awake()
        {
            if (propertiesData != null && !surfaceRegistry.ContainsKey(propertiesData.surfaceType))
                surfaceRegistry[propertiesData.surfaceType] = propertiesData;
        }

        /// <summary>
        /// Look up a surface's default properties by type.
        /// Returns null if not found.
        /// </summary>
        public static SurfacePropertiesData GetDataByType(SurfaceType type)
        {
            surfaceRegistry.TryGetValue(type, out SurfacePropertiesData data);
            return data;
        }

        public static void RegisterData(SurfacePropertiesData data)
        {
            if (data != null)
                surfaceRegistry[data.surfaceType] = data;
        }

        private void OnDrawGizmosSelected()
        {
            if (propertiesData != null)
                Gizmos.color = propertiesData.surfaceDebugColor;
            else
                Gizmos.color = Color.grey;
            Gizmos.DrawWireCube(GetComponent<Collider>() != null ? GetComponent<Collider>().bounds.center : transform.position,
                GetComponent<Collider>() != null ? GetComponent<Collider>().bounds.size : Vector3.one);
        }
    }
}
