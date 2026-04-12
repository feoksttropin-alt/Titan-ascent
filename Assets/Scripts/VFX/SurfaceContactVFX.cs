using System.Collections;
using UnityEngine;
using TitanAscent.Environment;
using TitanAscent.Optimization;
using TitanAscent.Player;

namespace TitanAscent.VFX
{
    /// <summary>
    /// Visual effects for surface contact, landing dust, sliding sparks, grip state, and
    /// surface-type-specific particles. Uses ObjectPooler for all pooled instances.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SurfaceContactVFX : MonoBehaviour
    {
        // ── Surface-specific prefabs ──────────────────────────────────────────
        [Header("Surface Contact Prefabs (one per surface type)")]
        [SerializeField] private ParticleSystem scaleArmorDustPrefab;    // ScaleArmor  — grey dust puff
        [SerializeField] private ParticleSystem boneChipPrefab;          // BoneRidge   — bone chip particles
        [SerializeField] private ParticleSystem crystalSparkPrefab;      // Crystal     — shard sparkles
        [SerializeField] private ParticleSystem muscleSkinSmearPrefab;   // MuscleSkin  — dark red smear trail
        [SerializeField] private ParticleSystem wingMembraneFlutterPrefab; // WingMembrane — blue-grey flutter

        // ── Grip glow ─────────────────────────────────────────────────────────
        [Header("Grip Glow")]
        [SerializeField] private Light contactGlowLight;
        [SerializeField] private float gripGlowMaxIntensity = 1.2f;

        // ── Crystal flash ─────────────────────────────────────────────────────
        [Header("Crystal Contact Light")]
        [SerializeField] private Light crystalContactLight;
        [SerializeField] private float crystalFlashDuration = 0.08f;

        // ── Thresholds ────────────────────────────────────────────────────────
        [Header("Impact Velocity Thresholds")]
        [SerializeField] private float minLandingSpeed = 1.5f;
        [SerializeField] private float maxLandingSpeed = 20f;

        // ── System References ─────────────────────────────────────────────────
        private GripSystem     gripSystem;
        private Rigidbody      rb;

        // ── Runtime State ─────────────────────────────────────────────────────
        private SurfaceType  currentSurface = SurfaceType.ScaleArmor;
        private bool         isSliding;
        private bool         isGripping;
        private bool         wasGripping;

        // Sliding stream — pooled instance kept alive while sliding
        private GameObject   activeSlideStream;
        private ParticleSystem activeSlidePS;

        // Contact point for glow positioning
        private Vector3      contactPoint;
        private bool         hasContact;

        // Coroutine guards
        private Coroutine    crystalFlashCoroutine;
        private Coroutine    gripLostFlashCoroutine;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            rb         = GetComponent<Rigidbody>();
            gripSystem = GetComponent<GripSystem>();
        }

        private void OnEnable()
        {
            if (gripSystem != null)
            {
                gripSystem.OnGripActivated.AddListener(HandleGripActivated);
                gripSystem.OnGripReleased.AddListener(HandleGripReleased);
                gripSystem.OnGripDepleted.AddListener(HandleGripDepleted);
            }
        }

        private void OnDisable()
        {
            if (gripSystem != null)
            {
                gripSystem.OnGripActivated.RemoveListener(HandleGripActivated);
                gripSystem.OnGripReleased.RemoveListener(HandleGripReleased);
                gripSystem.OnGripDepleted.RemoveListener(HandleGripDepleted);
            }

            StopSlideStream();
        }

        private void Update()
        {
            UpdateSliding();
            UpdateGripGlow();
        }

        // ── Collision Events ──────────────────────────────────────────────────

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.contactCount == 0) return;

            contactPoint = collision.contacts[0].point;
            hasContact   = true;

            // Resolve surface type
            SurfaceType surface = ResolveSurface(collision.gameObject);
            currentSurface = surface;

            float impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed >= minLandingSpeed)
                SpawnLandingDust(contactPoint, surface, impactSpeed);
        }

        private void OnCollisionStay(Collision collision)
        {
            if (collision.contactCount == 0) return;

            contactPoint   = collision.contacts[0].point;
            hasContact     = true;
            currentSurface = ResolveSurface(collision.gameObject);
        }

        private void OnCollisionExit(Collision collision)
        {
            hasContact = false;
            StopSlideStream();
        }

        // ── Landing Dust ──────────────────────────────────────────────────────

        private void SpawnLandingDust(Vector3 position, SurfaceType surface, float speed)
        {
            ParticleSystem prefab = GetPrefabForSurface(surface);
            if (prefab == null || ObjectPooler.Instance == null) return;

            float t = Mathf.InverseLerp(minLandingSpeed, maxLandingSpeed, speed);
            int   count = Mathf.RoundToInt(Mathf.Lerp(4f, 25f, t));

            GameObject obj = ObjectPooler.Instance.Get(prefab.gameObject, position, Quaternion.identity);
            if (obj == null) return;

            ParticleSystem ps = obj.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                main.startSpeedMultiplier = Mathf.Lerp(0.5f, 3f, t);
                ps.Emit(count);
            }

            // Crystal extra flash
            if (surface == SurfaceType.CrystalSurface)
                TriggerCrystalFlash(position);

            ObjectPooler.Instance.ReturnAfter(obj, 1.2f);
        }

        // ── Sliding Stream ────────────────────────────────────────────────────

        private void UpdateSliding()
        {
            if (!hasContact) return;

            bool nowSliding = currentSurface == SurfaceType.MuscleSkin
                              && rb.velocity.magnitude > 0.5f
                              && !IsGripping();

            if (nowSliding && !isSliding)
            {
                isSliding = true;
                StartSlideStream();
            }
            else if (!nowSliding && isSliding)
            {
                isSliding = false;
                StopSlideStream();
            }

            // Follow player contact point while active
            if (isSliding && activeSlideStream != null)
                activeSlideStream.transform.position = contactPoint;
        }

        private void StartSlideStream()
        {
            if (muscleSkinSmearPrefab == null || ObjectPooler.Instance == null) return;

            StopSlideStream(); // safety

            activeSlideStream = ObjectPooler.Instance.Get(
                muscleSkinSmearPrefab.gameObject, contactPoint, Quaternion.identity);

            if (activeSlideStream == null) return;

            activeSlidePS = activeSlideStream.GetComponent<ParticleSystem>();
            if (activeSlidePS != null && !activeSlidePS.isPlaying)
                activeSlidePS.Play();
        }

        private void StopSlideStream()
        {
            if (activeSlideStream == null) return;

            if (activeSlidePS != null)
                activeSlidePS.Stop();

            if (ObjectPooler.Instance != null)
                ObjectPooler.Instance.ReturnAfter(activeSlideStream, 0.3f);
            else
                activeSlideStream.SetActive(false);

            activeSlideStream = null;
            activeSlidePS     = null;
            isSliding         = false;
        }

        // ── Grip Glow ─────────────────────────────────────────────────────────

        private void HandleGripActivated()
        {
            isGripping = true;
        }

        private void HandleGripReleased()
        {
            isGripping = false;
        }

        private void HandleGripDepleted()
        {
            isGripping = false;
            if (gripLostFlashCoroutine != null) StopCoroutine(gripLostFlashCoroutine);
            gripLostFlashCoroutine = StartCoroutine(GripLostFlashCoroutine());
        }

        private void UpdateGripGlow()
        {
            if (contactGlowLight == null || gripSystem == null) return;

            bool gripping = IsGripping();
            if (gripping)
            {
                // Map grip percent → green (1.0) → yellow (0.5) → red (0.0)
                float pct = gripSystem.GripPercent;
                Color glowColor;
                if (pct > 0.5f)
                    glowColor = Color.Lerp(Color.yellow, Color.green,  (pct - 0.5f) * 2f);
                else
                    glowColor = Color.Lerp(Color.red,   Color.yellow, pct * 2f);

                contactGlowLight.color     = glowColor;
                contactGlowLight.intensity = Mathf.Lerp(0.1f, gripGlowMaxIntensity, pct);
                contactGlowLight.enabled   = true;

                // Position at contact point
                if (hasContact)
                    contactGlowLight.transform.position = contactPoint;
            }
            else if (!isGripping && contactGlowLight.enabled)
            {
                // Fade out quickly when not gripping (unless the lost-flash coroutine is running)
                contactGlowLight.intensity = Mathf.MoveTowards(
                    contactGlowLight.intensity, 0f, Time.deltaTime * 8f);

                if (contactGlowLight.intensity <= 0f)
                    contactGlowLight.enabled = false;
            }
        }

        private IEnumerator GripLostFlashCoroutine()
        {
            if (contactGlowLight == null) yield break;

            contactGlowLight.color     = Color.red;
            contactGlowLight.intensity = gripGlowMaxIntensity * 1.5f;
            contactGlowLight.enabled   = true;

            float elapsed = 0f;
            float duration = 0.18f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                contactGlowLight.intensity = Mathf.Lerp(
                    gripGlowMaxIntensity * 1.5f, 0f, elapsed / duration);
                yield return null;
            }

            contactGlowLight.enabled = false;
            gripLostFlashCoroutine = null;
        }

        // ── Crystal Flash ─────────────────────────────────────────────────────

        private void TriggerCrystalFlash(Vector3 position)
        {
            if (crystalContactLight == null) return;
            if (crystalFlashCoroutine != null) StopCoroutine(crystalFlashCoroutine);
            crystalFlashCoroutine = StartCoroutine(CrystalFlashCoroutine(position));
        }

        private IEnumerator CrystalFlashCoroutine(Vector3 position)
        {
            if (crystalContactLight == null) yield break;

            crystalContactLight.transform.position = position;
            crystalContactLight.color              = new Color(0.6f, 0.85f, 1f);
            crystalContactLight.intensity          = 4f;
            crystalContactLight.enabled            = true;

            float elapsed = 0f;
            while (elapsed < crystalFlashDuration)
            {
                elapsed += Time.deltaTime;
                crystalContactLight.intensity = Mathf.Lerp(4f, 0f, elapsed / crystalFlashDuration);
                yield return null;
            }

            crystalContactLight.enabled = false;
            crystalFlashCoroutine = null;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private bool IsGripping()
        {
            return gripSystem != null && gripSystem.IsGripping;
        }

        private SurfaceType ResolveSurface(GameObject go)
        {
            SurfaceProperties sp = go.GetComponent<SurfaceProperties>();
            if (sp != null) return sp.Type;

            SurfaceAnchorPoint anchor = go.GetComponent<SurfaceAnchorPoint>();
            if (anchor != null) return anchor.AnchorSurfaceType;

            return SurfaceType.ScaleArmor;
        }

        private ParticleSystem GetPrefabForSurface(SurfaceType type)
        {
            switch (type)
            {
                case SurfaceType.ScaleArmor:   return scaleArmorDustPrefab;
                case SurfaceType.BoneRidge:    return boneChipPrefab;
                case SurfaceType.CrystalSurface: return crystalSparkPrefab;
                case SurfaceType.MuscleSkin:   return muscleSkinSmearPrefab;
                case SurfaceType.WingMembrane: return wingMembraneFlutterPrefab;
                default:                       return scaleArmorDustPrefab;
            }
        }
    }
}
