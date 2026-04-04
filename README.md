# Titan Ascent

A physics-driven rage climbing game where players scale a colossal living titan 10,000 meters tall.

## Overview

Titan Ascent is built around precision physics, momentum-based movement, and punishing consequences for mistakes. Every meter climbed feels meaningful. Every fall is devastating. Reaching the crown is an unforgettable achievement.

## Engine & Tech

- **Engine:** Unity 2022.3 LTS
- **Physics:** Rigidbody-based custom rope physics with Verlet integration
- **Target Platform:** PC (Steam)
- **Performance Target:** 60 FPS on mid-range hardware

## Project Structure

```
Assets/Scripts/
├── Player/
│   ├── PlayerController.cs       — Rigidbody movement, states, momentum
│   ├── ThrusterSystem.cs         — Mid-air thrust with limited energy
│   ├── GripSystem.cs             — Surface grip gloves
│   └── EmergencyRecovery.cs      — Emergency catch window during falls
├── Grapple/
│   ├── GrappleController.cs      — Core grappling claw system
│   ├── RopeSimulator.cs          — Verlet rope physics with tension
│   └── GrappleAimAssist.cs       — Soft aim assist for anchor points
├── Physics/
│   ├── MomentumTracker.cs        — Momentum conservation system
│   └── SurfaceProperties.cs      — Surface type definitions and friction
├── Environment/
│   ├── ZoneManager.cs            — 9-zone titan climb management
│   ├── WindSystem.cs             — Wind columns and altitude wind
│   ├── TitanMovement.cs          — Wing tremors, breathing, contractions
│   ├── SurfaceAnchorPoint.cs     — Grappleable surface anchor logic
├── Systems/
│   ├── GameManager.cs            — Central game state, singleton
│   ├── FallTracker.cs            — Fall detection and emotional impact
│   ├── NarrationSystem.cs        — Dynamic narrator reactions
│   ├── SaveManager.cs            — PlayerPrefs + JSON save system
│   ├── ChallengeManager.cs       — Daily/challenge modifiers
│   └── HeightMarker.cs           — Visual best-height marker
├── UI/
│   ├── HUDController.cs          — Minimal HUD (height, falls, energy)
│   ├── NarrationUI.cs            — Narrator subtitle display
│   └── CameraController.cs       — Dynamic camera with fall zoom
├── Audio/
│   └── AudioManager.cs           — Altitude-aware audio channels
└── Data/
    ├── ZoneData.cs               — ScriptableObject zone config
    └── CosmeticItem.cs           — ScriptableObject cosmetic items
```

## Nine Zones

| # | Zone | Height Range | Key Feature |
|---|------|-------------|-------------|
| 1 | Tail Basin | 0–800m | Tutorial, large scale plates |
| 2 | Tail Spires | 800–1800m | Bone spikes, precision grappling |
| 3 | Hind Leg Valley | 1800–3000m | Terrain gaps, slippery skin |
| 4 | Wing Root | 3000–4200m | First moving environment |
| 5 | Spine Ridge | 4200–5500m | Narrow paths, strong wind |
| 6 | The Graveyard | 5500–6500m | Ruins, weapons, fallen climbers |
| 7 | Upper Back Storm | 6500–7800m | Extreme wind, lightning |
| 8 | The Neck | 7800–9000m | Breathing expansions, timing |
| 9 | The Crown | 9000–10000m | Final ascent, hardest section |

## Surface Types

| Surface | Grip | Notes |
|---------|------|-------|
| Scale Armor | Moderate | Standard climbing surface |
| Bone Ridges | High | Reliable anchors |
| Crystal Surfaces | Very High | Narrow anchor zones |
| Muscle Skin | Low | Sliding, rapid grappling required |
| Wing Membranes | Moderate | Slightly flexible |

## Game Modes

- **Standard Climb** — Full run from base to crown
- **Daily Challenge** — Date-seeded random modifiers
- **Speedrun Mode** — Built-in timer with splits
- **Ghost Mode** — Race previous runs

## Monetization

Single purchase, $6.99–$9.99. No pay-to-win. Optional cosmetic DLC post-launch.
