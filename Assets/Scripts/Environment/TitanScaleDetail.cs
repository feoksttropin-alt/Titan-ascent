using System.Collections;
using UnityEngine;

namespace TitanAscent.Environment
{
    /// <summary>
    /// Procedural visual variation for titan scale plate geometry.
    /// - Applies random rotation (±3°) and scale variation (±5%) to child "ScaleDetail" objects.
    /// - Uses MaterialPropertyBlock for per-instance hue/saturation shifts.
    /// - Optionally pulses scale to simulate slow "breathing" on living surfaces.
    /// - Only active within 30m of the player (checked every 2s for performance).
    /// </summary>
    public class TitanScaleDetail : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [Header("Variation")]
        [SerializeField] private float rotationVariance  = 3f;   // ±degrees per axis
        [SerializeField] private float scaleVariance     = 0.05f; // ±5%
        [SerializeField] private float hueShiftRange     = 0.03f;
        [SerializeField] private float satVarianceRange  = 0.1f;

        [Header("Breathing")]
        [SerializeField] private bool  enableBreathing   = true;
        [SerializeField] private float breathAmplitude   = 0.002f;
        [SerializeField] private float breathFrequency   = 0.05f;  // Hz

        [Header("LOD")]
        [SerializeField] private float activationRadius  = 30f;
        [SerializeField] private float checkInterval     = 2f;

        // -----------------------------------------------------------------------
        // Private state
        // -----------------------------------------------------------------------

        private Transform[]            _details;
        private MaterialPropertyBlock  _propBlock;
        private Renderer[]             _detailRenderers;
        private Vector3[]              _baseScales;
        private float[]                _breathOffsets;

        private bool     _isActive     = false;
        private Transform _playerTf;

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            CollectDetails();
            ApplyStaticVariation();
        }

        private void Start()
        {
            _propBlock = new MaterialPropertyBlock();

            // Cache player transform
            Player.PlayerController pc = FindFirstObjectByType<Player.PlayerController>();
            if (pc != null) _playerTf = pc.transform;

            StartCoroutine(ProximityCheckRoutine());
        }

        private void Update()
        {
            if (!_isActive || !enableBreathing || _details == null) return;

            float t = Time.time * breathFrequency * Mathf.PI * 2f;
            for (int i = 0; i < _details.Length; i++)
            {
                if (_details[i] == null) continue;
                float pulse = Mathf.Sin(t + _breathOffsets[i]) * breathAmplitude;
                Vector3 bs  = _baseScales[i];
                _details[i].localScale = new Vector3(bs.x + pulse, bs.y + pulse, bs.z + pulse);
            }
        }

        // -----------------------------------------------------------------------
        // Detail collection
        // -----------------------------------------------------------------------

        private void CollectDetails()
        {
            var list = new System.Collections.Generic.List<Transform>();

            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child == transform) continue;
                if (child.CompareTag("ScaleDetail"))
                    list.Add(child);
            }

            _details         = list.ToArray();
            _detailRenderers = new Renderer[_details.Length];
            _baseScales      = new Vector3[_details.Length];
            _breathOffsets   = new float[_details.Length];

            for (int i = 0; i < _details.Length; i++)
            {
                _detailRenderers[i] = _details[i].GetComponent<Renderer>();
                _baseScales[i]      = _details[i].localScale;
                _breathOffsets[i]   = Random.value * Mathf.PI * 2f;
            }
        }

        // -----------------------------------------------------------------------
        // Static variation (applied once on Awake)
        // -----------------------------------------------------------------------

        private void ApplyStaticVariation()
        {
            if (_details == null) return;

            for (int i = 0; i < _details.Length; i++)
            {
                Transform t = _details[i];
                if (t == null) continue;

                // Rotation
                Vector3 rot = t.localEulerAngles;
                rot.x += Random.Range(-rotationVariance, rotationVariance);
                rot.y += Random.Range(-rotationVariance, rotationVariance);
                rot.z += Random.Range(-rotationVariance, rotationVariance);
                t.localEulerAngles = rot;

                // Scale
                float scaleFactor  = 1f + Random.Range(-scaleVariance, scaleVariance);
                t.localScale       = _baseScales[i] * scaleFactor;
                _baseScales[i]     = t.localScale; // update base for breathing

                // Color via MaterialPropertyBlock
                Renderer rend = _detailRenderers[i];
                if (rend == null) continue;

                rend.GetPropertyBlock(_propBlock);

                Color baseColor = rend.sharedMaterial != null
                    ? rend.sharedMaterial.color
                    : Color.white;

                Color.RGBToHSV(baseColor, out float h, out float s, out float v);
                h = Mathf.Repeat(h + Random.Range(-hueShiftRange, hueShiftRange), 1f);
                s = Mathf.Clamp01(s + Random.Range(-satVarianceRange, satVarianceRange));

                _propBlock.SetColor("_BaseColor", Color.HSVToRGB(h, s, v));
                _propBlock.SetColor("_Color",     Color.HSVToRGB(h, s, v));
                rend.SetPropertyBlock(_propBlock);
            }
        }

        // -----------------------------------------------------------------------
        // Proximity check
        // -----------------------------------------------------------------------

        private IEnumerator ProximityCheckRoutine()
        {
            var wait = new WaitForSeconds(checkInterval);
            while (true)
            {
                yield return wait;

                if (_playerTf == null)
                {
                    Player.PlayerController pc = FindFirstObjectByType<Player.PlayerController>();
                    if (pc != null) _playerTf = pc.transform;
                }

                bool shouldBeActive = _playerTf != null &&
                    Vector3.Distance(transform.position, _playerTf.position) <= activationRadius;

                if (shouldBeActive != _isActive)
                {
                    _isActive = shouldBeActive;

                    // Restore base scales when deactivating
                    if (!_isActive && _details != null)
                    {
                        for (int i = 0; i < _details.Length; i++)
                        {
                            if (_details[i] != null)
                                _details[i].localScale = _baseScales[i];
                        }
                    }
                }
            }
        }
    }
}
