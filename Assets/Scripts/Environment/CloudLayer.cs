using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TitanAscent.Environment
{
    // -----------------------------------------------------------------------
    // Data
    // -----------------------------------------------------------------------

    [System.Serializable]
    public class CloudLayerConfig
    {
        public float      height          = 800f;
        public GameObject cloudObject;
        public Vector2    scrollSpeed     = new Vector2(0.5f, 0f);
        public float      opacityAtBand   = 1f;

        [HideInInspector] public Renderer   cachedRenderer;
        [HideInInspector] public Material   instanceMaterial;  // instance copy so we don't dirty shared assets
        [HideInInspector] public Vector2    scrollOffset;
        [HideInInspector] public float      currentOpacity;
    }

    // -----------------------------------------------------------------------
    // CloudLayer
    // -----------------------------------------------------------------------

    /// <summary>
    /// Manages cloud layers at different altitudes.  Three bands: low (800 m),
    /// mid (3 500 m), high (7 000 m).  Scrolls horizontally, reduces opacity
    /// when player passes through, triggers whiteout flash, and darkens in
    /// storm zone (≥6 500 m).
    /// </summary>
    public class CloudLayer : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Inspector
        // ------------------------------------------------------------------

        [Header("Cloud Layer Configuration")]
        [SerializeField] private CloudLayerConfig[] cloudLayers = new CloudLayerConfig[]
        {
            new CloudLayerConfig { height = 800f,  scrollSpeed = new Vector2(0.3f, 0f),  opacityAtBand = 0.85f },
            new CloudLayerConfig { height = 3500f, scrollSpeed = new Vector2(0.15f, 0f), opacityAtBand = 0.75f },
            new CloudLayerConfig { height = 7000f, scrollSpeed = new Vector2(0.08f, 0f), opacityAtBand = 0.60f }
        };

        [Header("Player Proximity")]
        [SerializeField] private float cloudProximityRange = 100f;
        [SerializeField] private float proximityOpacityFade = 0.15f;

        [Header("Whiteout")]
        [SerializeField] private CanvasGroup whiteoutCanvasGroup;
        [SerializeField] private float whiteoutPeakOpacity = 0.30f;
        [SerializeField] private float whiteoutFadeDuration = 0.5f;

        [Header("Storm Zone")]
        [SerializeField] private float stormAltitudeStart  = 6500f;
        [SerializeField] private Color stormCloudColor     = new Color(0.25f, 0.27f, 0.32f, 1f);
        [SerializeField] private float turbulenceStrength  = 0.4f;

        // ------------------------------------------------------------------
        // Private state
        // ------------------------------------------------------------------

        private Player.PlayerController _player;
        private float                   _lastPlayerAltitude;
        private bool                    _isWhiteouting;

        // Tracks which clouds the player just passed through
        private readonly HashSet<int> _passedThrough = new HashSet<int>();

        // UV scroll property name — works with standard and URP Lit materials
        private static readonly int MainTexST = Shader.PropertyToID("_MainTex_ST");
        private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int ColorProp  = Shader.PropertyToID("_BaseColor");

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            _player = FindFirstObjectByType<Player.PlayerController>();

            foreach (CloudLayerConfig cfg in cloudLayers)
            {
                if (cfg.cloudObject == null) continue;

                cfg.cachedRenderer = cfg.cloudObject.GetComponent<Renderer>();
                if (cfg.cachedRenderer != null)
                {
                    cfg.instanceMaterial = cfg.cachedRenderer.material; // creates instance
                }
                cfg.currentOpacity = cfg.opacityAtBand;
            }

            if (whiteoutCanvasGroup != null)
                whiteoutCanvasGroup.alpha = 0f;
        }

        private void Update()
        {
            if (_player == null)
            {
                _player = FindFirstObjectByType<Player.PlayerController>();
                return;
            }

            float altitude    = _player.CurrentHeight;
            bool  inStorm     = altitude >= stormAltitudeStart;
            float deltaTime   = Time.deltaTime;

            for (int i = 0; i < cloudLayers.Length; i++)
            {
                CloudLayerConfig cfg = cloudLayers[i];
                if (cfg.cloudObject == null) continue;

                // ----------------------------------------------------------
                // Scroll
                // ----------------------------------------------------------
                Vector2 scrollDelta = cfg.scrollSpeed * deltaTime;

                // Add turbulence in storm zone
                if (inStorm)
                {
                    float noise = (Mathf.PerlinNoise(Time.time * 0.3f, i * 47.3f) * 2f - 1f);
                    scrollDelta += new Vector2(noise * turbulenceStrength, noise * turbulenceStrength * 0.5f) * deltaTime;
                }

                cfg.scrollOffset += scrollDelta;

                if (cfg.instanceMaterial != null)
                {
                    // Try URP BaseMap first, fall back to legacy MainTex
                    if (cfg.instanceMaterial.HasProperty(BaseMapST))
                    {
                        Vector4 st = cfg.instanceMaterial.GetVector(BaseMapST);
                        st.z = cfg.scrollOffset.x;
                        st.w = cfg.scrollOffset.y;
                        cfg.instanceMaterial.SetVector(BaseMapST, st);
                    }
                    else if (cfg.instanceMaterial.HasProperty(MainTexST))
                    {
                        Vector4 st = cfg.instanceMaterial.GetVector(MainTexST);
                        st.z = cfg.scrollOffset.x;
                        st.w = cfg.scrollOffset.y;
                        cfg.instanceMaterial.SetVector(MainTexST, st);
                    }
                }

                // ----------------------------------------------------------
                // Opacity — reduce when player is near band height
                // ----------------------------------------------------------
                float distToBand   = Mathf.Abs(altitude - cfg.height);
                float proxFactor   = 1f - Mathf.Clamp01(distToBand / cloudProximityRange);
                float targetOpacity = Mathf.Lerp(cfg.opacityAtBand, proximityOpacityFade, proxFactor);
                cfg.currentOpacity  = Mathf.Lerp(cfg.currentOpacity, targetOpacity, deltaTime * 4f);

                // ----------------------------------------------------------
                // Storm colour tint on high cloud layer
                // ----------------------------------------------------------
                if (cfg.instanceMaterial != null && cfg.instanceMaterial.HasProperty(ColorProp))
                {
                    Color baseCol = inStorm ? stormCloudColor : Color.white;
                    baseCol.a     = cfg.currentOpacity;
                    cfg.instanceMaterial.SetColor(ColorProp, baseCol);
                }

                // ----------------------------------------------------------
                // Whiteout: detect player passing through this band
                // ----------------------------------------------------------
                bool playerCrossedUp   = _lastPlayerAltitude < cfg.height && altitude >= cfg.height;
                bool playerCrossedDown = _lastPlayerAltitude > cfg.height && altitude <= cfg.height;

                if ((playerCrossedUp || playerCrossedDown) && !_passedThrough.Contains(i))
                {
                    _passedThrough.Add(i);
                    if (!_isWhiteouting)
                        StartCoroutine(WhiteoutFlash(i));
                }
                else if (!playerCrossedUp && !playerCrossedDown)
                {
                    _passedThrough.Remove(i);
                }
            }

            _lastPlayerAltitude = altitude;
        }

        // ------------------------------------------------------------------
        // Whiteout coroutine
        // ------------------------------------------------------------------

        private IEnumerator WhiteoutFlash(int bandIndex)
        {
            _isWhiteouting = true;

            if (whiteoutCanvasGroup == null)
            {
                _isWhiteouting = false;
                yield break;
            }

            // Fade in
            float elapsed = 0f;
            float halfDur = whiteoutFadeDuration * 0.5f;

            while (elapsed < halfDur)
            {
                elapsed += Time.deltaTime;
                whiteoutCanvasGroup.alpha = Mathf.Lerp(0f, whiteoutPeakOpacity, elapsed / halfDur);
                yield return null;
            }

            whiteoutCanvasGroup.alpha = whiteoutPeakOpacity;

            // Fade out
            elapsed = 0f;
            while (elapsed < halfDur)
            {
                elapsed += Time.deltaTime;
                whiteoutCanvasGroup.alpha = Mathf.Lerp(whiteoutPeakOpacity, 0f, elapsed / halfDur);
                yield return null;
            }

            whiteoutCanvasGroup.alpha = 0f;
            _isWhiteouting = false;
        }

        // ------------------------------------------------------------------
        // Gizmos
        // ------------------------------------------------------------------

        private void OnDrawGizmosSelected()
        {
            foreach (CloudLayerConfig cfg in cloudLayers)
            {
                Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
                Gizmos.DrawWireCube(
                    new Vector3(transform.position.x, cfg.height, transform.position.z),
                    new Vector3(200f, 1f, 200f));
            }
        }
    }
}
