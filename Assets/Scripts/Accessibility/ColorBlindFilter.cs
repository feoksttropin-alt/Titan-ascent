using System.Collections.Generic;
using UnityEngine;
using TitanAscent.Environment;

namespace TitanAscent.Accessibility
{
    /// <summary>
    /// Applies color-blind compensation by remapping the key gameplay colors used on
    /// SurfaceAnchorPoint renderers.  Called by AccessibilityManager.ApplyColorBlindMode().
    ///
    /// No full-screen post-process shader is required: the approach modifies
    /// MaterialPropertyBlocks on every SurfaceAnchorPoint renderer each time the mode
    /// changes, keeping the runtime cost negligible.
    ///
    /// Color mappings:
    ///   Deuteranopia  — valid (green → white, high contrast); invalid (red → orange)
    ///   Protanopia    — red family shifted to yellow-green
    ///   Tritanopia    — blue family shifted to green
    ///   HighContrast  — ALL anchors forced to bright yellow regardless of type
    ///   None          — restores original inspector-authored colors
    /// </summary>
    public class ColorBlindFilter : MonoBehaviour
    {
        // ── Default anchor colors (must match SurfaceAnchorPoint inspector defaults) ──
        private static readonly Color DefaultIdle       = new Color(0.50f, 0.50f, 1.00f, 0.50f);
        private static readonly Color DefaultHighlight  = new Color(0.00f, 1.00f, 0.50f, 0.80f);
        private static readonly Color DefaultAttached   = new Color(1.00f, 0.80f, 0.00f, 1.00f);

        // ── Deuteranopia remaps ────────────────────────────────────────────────────
        // Green (highlight / valid) → high-contrast white; red/orange tones → orange.
        private static readonly Color DeuterIdle       = new Color(0.60f, 0.60f, 1.00f, 0.55f); // blue-ish unchanged
        private static readonly Color DeuterHighlight  = new Color(1.00f, 1.00f, 1.00f, 0.95f); // white — maximum contrast
        private static readonly Color DeuterAttached   = new Color(1.00f, 0.55f, 0.00f, 1.00f); // orange (was yellow-gold)

        // ── Protanopia remaps ──────────────────────────────────────────────────────
        // Red channel compressed; shift toward yellow-green so reds become visible.
        private static readonly Color ProtaIdle        = new Color(0.50f, 0.50f, 1.00f, 0.50f); // blue unchanged
        private static readonly Color ProtaHighlight   = new Color(0.55f, 1.00f, 0.00f, 0.90f); // yellow-green (was green)
        private static readonly Color ProtaAttached    = new Color(0.90f, 0.90f, 0.10f, 1.00f); // yellow (red → yellow)

        // ── Tritanopia remaps ──────────────────────────────────────────────────────
        // Blue channel affected; shift blues toward green.
        private static readonly Color TritaIdle        = new Color(0.30f, 0.80f, 0.30f, 0.55f); // green (was blue)
        private static readonly Color TritaHighlight   = new Color(0.00f, 1.00f, 0.50f, 0.85f); // green-cyan (acceptable)
        private static readonly Color TritaAttached    = new Color(1.00f, 0.80f, 0.00f, 1.00f); // yellow unchanged

        // ── High Contrast ──────────────────────────────────────────────────────────
        private static readonly Color HighContrastAll  = new Color(1.00f, 0.95f, 0.00f, 1.00f); // bright yellow

        // ── Current mode ──────────────────────────────────────────────────────────
        private ColorBlindMode currentMode = ColorBlindMode.None;

        // ── Cached anchor list ─────────────────────────────────────────────────────
        // Re-scanned each Apply call so newly spawned anchors are captured.
        private readonly List<SurfaceAnchorPoint> anchorCache = new List<SurfaceAnchorPoint>();

        // ──────────────────────────────────────────────────────────────────────────
        // Public API — called by AccessibilityManager
        // ──────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Sets the active color-blind compensation mode and immediately repaints all
        /// SurfaceAnchorPoint renderers in the scene.
        /// </summary>
        public void ApplyMode(ColorBlindMode mode)
        {
            currentMode = mode;
            RefreshAnchorCache();
            PaintAllAnchors(mode);
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Internal helpers
        // ──────────────────────────────────────────────────────────────────────────

        private void RefreshAnchorCache()
        {
            anchorCache.Clear();
            SurfaceAnchorPoint[] found = FindObjectsByType<SurfaceAnchorPoint>(FindObjectsSortMode.None);
            anchorCache.AddRange(found);
        }

        private void PaintAllAnchors(ColorBlindMode mode)
        {
            foreach (SurfaceAnchorPoint anchor in anchorCache)
            {
                if (anchor == null) continue;

                Renderer r = anchor.GetComponent<Renderer>();
                if (r == null) continue;

                Color idleColor, highlightColor, attachedColor;

                if (mode == ColorBlindMode.None)
                {
                    // Restore defaults — anchor will repaint itself on next state change.
                    // Forcing a trivial property-block write with the originals is safest.
                    idleColor      = DefaultIdle;
                    highlightColor = DefaultHighlight;
                    attachedColor  = DefaultAttached;
                }
                else
                {
                    ResolveColors(mode, out idleColor, out highlightColor, out attachedColor);
                }

                // Determine which color to apply right now based on current visual state.
                Color target;
                switch (anchor.VisualState)
                {
                    case AnchorVisualState.Highlighted:
                        target = highlightColor;
                        break;
                    case AnchorVisualState.Attached:
                        target = attachedColor;
                        break;
                    default:
                        target = idleColor;
                        break;
                }

                // Write via a property block so we don't create new material instances.
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", target);
                block.SetColor("_EmissionColor", target * (anchor.VisualState == AnchorVisualState.Idle ? 0.3f : 1.5f));
                r.SetPropertyBlock(block);

                // Store the resolved palette so the anchor can re-apply on future state changes
                // via the ColorBlindAnchorOverride helper component.
                ColorBlindAnchorOverride helper = anchor.GetComponent<ColorBlindAnchorOverride>();
                if (helper == null && mode != ColorBlindMode.None)
                    helper = anchor.gameObject.AddComponent<ColorBlindAnchorOverride>();

                if (helper != null)
                {
                    if (mode == ColorBlindMode.None)
                    {
                        Destroy(helper);
                    }
                    else
                    {
                        helper.IdleColor      = idleColor;
                        helper.HighlightColor = highlightColor;
                        helper.AttachedColor  = attachedColor;
                        helper.Active         = true;
                    }
                }
            }
        }

        private static void ResolveColors(
            ColorBlindMode mode,
            out Color idle,
            out Color highlight,
            out Color attached)
        {
            switch (mode)
            {
                case ColorBlindMode.Deuteranopia:
                    idle      = DeuterIdle;
                    highlight = DeuterHighlight;
                    attached  = DeuterAttached;
                    break;

                case ColorBlindMode.Protanopia:
                    idle      = ProtaIdle;
                    highlight = ProtaHighlight;
                    attached  = ProtaAttached;
                    break;

                case ColorBlindMode.Tritanopia:
                    idle      = TritaIdle;
                    highlight = TritaHighlight;
                    attached  = TritaAttached;
                    break;

                default: // HighContrast or any future mode
                    idle      = HighContrastAll;
                    highlight = HighContrastAll;
                    attached  = HighContrastAll;
                    break;
            }
        }

        // Re-paint whenever the scene loads new anchors (e.g. zone transitions).
        private void OnEnable()
        {
            if (currentMode != ColorBlindMode.None)
                ApplyMode(currentMode);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Helper component — lives on each anchor while a color-blind mode is active.
    // SurfaceAnchorPoint calls UpdateVisuals() internally; this component intercepts
    // the property block writes to keep the color-blind palette in place.
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Lightweight per-anchor component injected by ColorBlindFilter.
    /// Overrides the property-block color written by SurfaceAnchorPoint.UpdateVisuals
    /// so that the color-blind palette is maintained even after state changes.
    /// </summary>
    [AddComponentMenu("")] // Hidden — managed exclusively by ColorBlindFilter.
    public class ColorBlindAnchorOverride : MonoBehaviour
    {
        public Color IdleColor      = Color.white;
        public Color HighlightColor = Color.white;
        public Color AttachedColor  = Color.white;
        public bool  Active         = false;

        private SurfaceAnchorPoint anchor;
        private Renderer anchorRenderer;

        private void Awake()
        {
            anchor         = GetComponent<SurfaceAnchorPoint>();
            anchorRenderer = GetComponent<Renderer>();
        }

        // LateUpdate runs after SurfaceAnchorPoint.UpdateVisuals (which is called in
        // response to SetHighlighted / SetAttached, not in Update).  We correct the
        // property block one frame late; the visual delta is imperceptible.
        private void LateUpdate()
        {
            if (!Active || anchor == null || anchorRenderer == null) return;

            Color target;
            switch (anchor.VisualState)
            {
                case AnchorVisualState.Highlighted: target = HighlightColor; break;
                case AnchorVisualState.Attached:    target = AttachedColor;  break;
                default:                            target = IdleColor;      break;
            }

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            anchorRenderer.GetPropertyBlock(block);

            Color current = block.GetColor("_BaseColor");
            // Only re-write if the anchor actually changed its own color this frame.
            if (current == target) return;

            block.SetColor("_BaseColor", target);
            block.SetColor("_EmissionColor", target * (anchor.VisualState == AnchorVisualState.Idle ? 0.3f : 1.5f));
            anchorRenderer.SetPropertyBlock(block);
        }
    }
}
