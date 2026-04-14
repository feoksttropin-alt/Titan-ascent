# Titan Ascent — Game Design Document

**Version:** 1.0  
**Engine:** Unity 6 LTS (6000.0.26f1)  
**Platform:** PC (Steam)  
**Genre:** Physics-based rage climber / platformer  

---

## 1. Vision Statement

> *Scale a living titan 10,000 meters tall.*

Titan Ascent is a physics-based rage climber in which the player scales a colossal, breathing, shifting creature using grappling claws, grip gloves, and a limited thruster pack. Every meter climbed is earned through momentum management, grapple timing, and surface reading. Every fall is spectacular, punishing, and instructive. Reaching the crown is an unforgettable achievement — one that almost no one will accomplish on their first attempt, or their tenth.

The game is designed around a single, unbroken journey. There are no checkpoints. Progress is psychological as much as physical. The titan is not a puzzle to be solved; it is a force of nature to be respected.

---

## 2. Core Design Pillars

### Pillar 1 — Physics Mastery
Every interaction is physically simulated. The rope uses Verlet integration with 5 constraint passes per FixedUpdate. The player is a Rigidbody that responds to momentum, surface friction, and wind force. Mastering the game means mastering physics — understanding how swing arcs work, how to bleed momentum before a landing, and how tension in a rope translates to launch force. There are no canned animations for "good grapple." Either the physics works or it does not.

### Pillar 2 — Meaningful Progress
There are no checkpoints, but progress is never truly lost. The player's best-height marker persists on the titan's body as a visible ring. Every fall teaches the next attempt. The narration, fall tracker, and achievement system all reflect and reward cumulative progress over sessions. A player who dies at 4,000m for the tenth time is a player who has learned 4,000 meters of the titan.

### Pillar 3 — Dramatic Failure
Falls are designed to feel cinematic. The `FallTracker` classifies falls by severity (Small: 5m, Medium: 20m, Large: 100m, Catastrophic: 500m, Run-Ending: 1,500m+). Each threshold triggers distinct camera behavior, audio, narration, and screen effects via `JuiceController`. Catastrophic falls slow the FOV, trigger the emergency recovery window, and are deliberately awe-inspiring. Dying should feel worth watching.

### Pillar 4 — Massive Scale
The titan is 10,000 meters tall. Scale is the game's central aesthetic. At Zone 1 the titan's individual scale plates are the size of buildings. By Zone 9 the player can see weather systems far below. The camera, atmospheric controller, wind volume curve, and titan breathing audio all shift continuously with altitude to communicate this scale. The player should feel genuinely small.

### Pillar 5 — Viral Moments
The game is designed to produce clippable moments. The rope tension system colors the rope red under strain. The `JuiceController` freeze-frames grapple impacts. The `GhostSystem` records every run so players can replay and share their best and worst moments. Recovery from a 300m fall using a last-second grapple catch should look as dramatic as it feels.

---

## 3. Gameplay Loop

```
START RUN
    │
    ▼
CLIMB (grapple, grip, thrust)
    │
    ├─── Reach new height record → NarrationSystem fires, JuiceController gold flash
    │
    ├─── Enter new zone → ZoneManager fires OnZoneChanged → narrator, atmosphere update
    │
    ├─── FALL DETECTED (FallTracker)
    │        │
    │        ├─── Small/Medium → brief narrator comment, camera shake
    │        ├─── Large → emergency window opens, extended camera zoom
    │        ├─── Catastrophic → FOV pulse, slow-motion, full narration
    │        └─── Run-Ending → full reset, PostRunSummary displayed
    │
    ├─── RECOVERY
    │        └─── EmergencyRecovery window used → MajorRecovery event
    │
    └─── SUMMIT (10,000m)
             └─── Victory sequence → slow motion, gold vignette, achievement unlock
```

**Session end** triggers `PostRunSummary`: best height, total falls, longest fall, total time, zones reached. Stats are persisted via `SaveManager`.

---

## 4. Player Tools

### 4.1 Grappling Claw (`GrappleController` + `RopeSimulator`)
The primary movement tool. Fires a claw that anchors to any `SurfaceAnchorPoint` on the titan.

| Parameter | Value |
|---|---|
| Default max range | Configurable per anchor (typically 25–60m) |
| Rope segments | 20 (Verlet chain) |
| Constraint iterations per FixedUpdate | 5 |
| Tension color range | White (0%) → Red (100%) |
| Snap-on impact | 1-frame freeze + camera shake (0.15 mag, 0.08s) |

Soft aim assist (`GrappleAimAssist`) bends the targeting ray toward valid anchor points within a configurable cone. Aim assist strength scales down at higher zones. The `MultiGrappleManager` allows two simultaneous grapple points for advanced swing techniques.

### 4.2 Grip Gloves (`GripSystem`)
Activates on contact with any surface tagged with `SurfaceProperties`. Reduces slide velocity by applying friction force proportional to grip percentage and surface `FrictionCoefficient`.

| Parameter | Value |
|---|---|
| Max grip | 100 units |
| Drain rate (on surface) | 25 units/sec × surface `GripMultiplier` |
| Regen delay | 1.5 sec after release |
| Regen rate | 30 units/sec |
| Activation cost | 5 units |

When grip is depleted the player enters `GripState.Recovering` and loses the ability to grip until fully regenerated. The `ChallengeManager`'s `UltraSlippery` modifier reduces all surface `FrictionCoefficient` values by 80%.

### 4.3 Thruster Pack (`ThrusterSystem`)
Provides short directional bursts for repositioning, gap-crossing, or escaping surface slides.

| Parameter | Value |
|---|---|
| Max energy | 100 units |
| Thrust force | 8 N (applied to Rigidbody) |
| Energy cost per burst | 12 units |
| Cooldown | 0.2 sec |
| Regen delay | 0.5 sec after last use |
| Regen rate | 15 units/sec |
| `LowFuel` modifier multiplier | 0.35× (35 max units) |

The thruster fires in the direction of player input at time of activation. Cannot fire while grounded. The `NoThrusterZone1` achievement tracks whether thrusters were used during Zone 1.

### 4.4 Emergency Recovery (`EmergencyRecovery`)
Opens a time-limited window during large falls (≥100m) in which grapple range is extended (+20m) and grapple pull force is boosted (+300 N). Activated automatically by `FallTracker.OnEmergencyWindowOpen`.

| Parameter | Value |
|---|---|
| Base window duration | 0.5 sec |
| Extended window (≥500m fall) | 2.0 sec |
| Grapple range bonus | +20m |
| Grapple force bonus | +300 N |
| Visual | Pulsing orange ring indicator |

A successful grapple inside the window fires `NarrationSystem.TriggerMajorRecovery()` and the `JuiceController.TriggerRecovery()` screen flash. The window closes on use or expiry.

---

## 5. Physics Philosophy

Titan Ascent does not fake physics. All player motion derives from Rigidbody forces, not animation state transitions. This means:

- **Momentum is conserved.** A player who swings at speed and releases will travel in an arc. The game does not correct this.
- **Surface friction is real.** Each surface type has a `frictionCoefficient` that determines how much a gripping player slows. Lower friction surfaces require faster grapple play.
- **The rope has mass.** The Verlet rope sags under gravity. A long rope low on the titan will form a deep catenary curve. Rope length directly affects swing speed.
- **Wind is a force.** `WindSystem` applies continuous directional force to the player Rigidbody, scaled by altitude via `ZoneManager.GetWindStrengthAtHeight()`. Wind is not decorative.
- **The titan moves.** `TitanMovement` produces breathing expansions, wing tremors, and body contractions. These are genuine physics events — a breathing contraction can knock a player off a surface they were stable on.

The design philosophy accepts that this creates unpredictability. Unpredictability is a feature. The game rewards players who adapt to physics rather than fight it.

---

## 6. Zones

### Zone 1 — Tail Basin (0–800m)
**Goal:** Introduce all tools in a forgiving environment.  
**Design Notes:** Wide ledges, abundant anchor points, gentle slope. Wind is negligible (strength 0.1). Scale plates are clean and flat. Tutorial text is embedded in the environment geometry — no UI overlays.  
**Danger Profile:** Low. Occasional tail sway from `TitanMovement`. Falls typically under 50m.  
**Dominant Surface:** Scale Armor (friction 0.6, grip multiplier 1.0)

### Zone 2 — Tail Spires (800–1800m)
**Goal:** Introduce precision grappling with higher anchor points and narrower ledges.  
**Design Notes:** Bone spire formations create natural anchor chains. Players learn to chain swings. First significant fall risk for new players.  
**Danger Profile:** Moderate. Spire edges can redirect falls into open air. Wind 0.25.  
**Dominant Surface:** Scale Armor with Bone Ridge protrusions

### Zone 3 — Hind Leg Valley (1800–3000m)
**Goal:** Introduce surface type variety and terrain gaps.  
**Design Notes:** Transition from scale to bone ridge. Large gaps between leg tendons require grapple accuracy. Slippery muscle skin patches appear near the back of the knee.  
**Danger Profile:** Moderate–High. Gaps can result in 200–400m falls if grapple is missed.  
**Dominant Surface:** Bone Ridge (friction 1.2, grip multiplier 1.4)

### Zone 4 — Wing Root (3000–4200m)
**Goal:** Introduce moving environment. First wing membrane surfaces.  
**Design Notes:** Wing tremors begin here. Membrane sections flex, displacing the player slightly on each tremor. Players must time movement between tremor cycles.  
**Danger Profile:** High. Wing tremors can displace players 10–20m laterally. Wind 0.6.  
**Dominant Surface:** Wing Membrane (friction 0.5, grip multiplier 0.9)

### Zone 5 — Spine Ridge (4200–5500m)
**Goal:** Introduce crystal surfaces and strong lateral wind.  
**Design Notes:** Crystal formations provide extreme grip but narrow footholds. Wind columns run vertically between spine ridges — players can use these to gain altitude. High fall risk at ridge edges.  
**Danger Profile:** Very High. Ridge edges + strong wind = long falls. Wind 0.75.  
**Dominant Surface:** Crystal Surface (friction 1.8, grip multiplier 1.9)

### Zone 6 — The Graveyard (5500–6500m)
**Goal:** Environmental storytelling and psychological pressure.  
**Design Notes:** Shattered grapple equipment, torn rope fragments, and skeletal remains of previous climbers decorate the bone ridge. Landmark locations include named fallen climbers. Wind howls through gaps in the ridge.  
**Danger Profile:** Very High. Dense debris can obscure fall funnels. Wind 0.85. First zone where a fall from zone height to zone floor is potentially run-ending.  
**Dominant Surface:** Bone Ridge

### Zone 7 — Upper Back Storm (6500–7800m)
**Goal:** Maximum environmental hostility. Test of accumulated skill.  
**Design Notes:** Perpetual electrical storm. Lightning strikes illuminate anchor points at intervals. Muscle skin tears away in strips, removing anchor points dynamically. `ExtremeWind` modifier is effectively always active here.  
**Danger Profile:** Extreme. Active wind 1.0 (maximum). Muscle skin low friction means grip is essential. Any fall from upper portion of zone is likely run-ending.  
**Dominant Surface:** Muscle Skin (friction 0.25, grip multiplier 0.6)

### Zone 8 — The Neck (7800–9000m)
**Goal:** Timing-based challenge through titan breathing rhythm.  
**Design Notes:** The titan's breathing pulse is overwhelming — 8–12 second cycle. Breathing expansions swell the neck's diameter by ~3m, then contract. Players must time rest stops to avoid being shaken loose. Some anchor points only become accessible at peak expansion.  
**Danger Profile:** Extreme. Contraction shakes with strong downward impulse. Wind 0.9.  
**Dominant Surface:** Muscle Skin

### Zone 9 — The Crown (9000–10,000m)
**Goal:** Final synthesis of all mechanics. High difficulty with earned drama.  
**Design Notes:** Crystal surfaces dominate. The titan's breathing becomes audible and visual. The ambient light shifts to pale blue. A fall from Zone 9 back to Zone 8 is always run-ending. Victory sequence triggers at 10,000m.  
**Danger Profile:** Maximum. Every anchor point matters. Wind returns to 0.5 but the player's accumulated fatigue and stress make errors more likely.  
**Dominant Surface:** Crystal Surface

---

## 7. Surface Types

| Surface | Friction Coefficient | Grip Multiplier | Grappleable | Role |
|---|---|---|---|---|
| Scale Armor | 0.6 | 1.0 | Yes (strength 1.0) | Standard introductory surface. Reliable, forgiving. |
| Bone Ridge | 1.2 | 1.4 | Yes (strength 1.0) | High grip, reliable anchors. Rewarding to read correctly. |
| Crystal Surface | 1.8 | 1.9 | Yes (strength 0.8) | Highest grip, narrow zones. Precision required. |
| Muscle Skin | 0.25 | 0.6 | Yes (strength 0.6) | Dangerous. Sliding inevitable without active grip. |
| Wing Membrane | 0.5 | 0.9 | Partial (strength 0.5) | Flexible, unpredictable. Anchors may shift on tremor. |

`GlobalFrictionMultiplier` (default 1.0) is multiplied on top of all per-surface values. The `UltraSlippery` challenge modifier sets it to 0.2, making even Crystal Surfaces dangerous.

---

## 8. Fall System

The `FallTracker` component monitors vertical velocity each frame. A fall begins when `velocity.y < -3 m/s`. It ends when `velocity.y > -2 m/s` after a 0.3-second debounce.

| Severity | Distance Threshold | Events Fired |
|---|---|---|
| Small | ≥5m | NarrationSystem (SmallFall), minor camera shake |
| Medium | ≥20m | NarrationSystem (MediumFall), moderate shake |
| Large | ≥100m | NarrationSystem (LargeFall), EmergencyWindow opens, zoom |
| Catastrophic | ≥500m | NarrationSystem (CatastrophicFall), FOV pulse, JuiceController |
| Run-Ending | ≥1500m | Full reset + PostRunSummary |

**Design Philosophy:** Falls are not failure screens. They are events with escalating consequence. A 50m fall costs time. A 500m fall costs progress. A 1,500m fall ends the run. This gradient ensures that not every mistake feels equally devastating, while preserving the weight of major failures. The emergency recovery window gives skilled players a chance to convert catastrophe into a highlight reel moment.

---

## 9. Narration Tone Guide

The narrator is a single, consistent voice. Calm. Philosophical. Observant. Slightly sarcastic — but never mean. They do not cheer. They do not panic. They have seen this happen before.

**Key rules:**
- Lines under 12 words where possible.
- No exclamation marks. Ever.
- The narrator does not explain the game. They comment on it.
- Victory lines are quiet, not triumphant.
- Fall lines acknowledge what happened without dwelling on it.
- Minimum 8 seconds between any two lines.
- No line repeats back-to-back (enforced by `NarrationSystem.PickLine()`).

---

## 10. Replayability Systems

### Daily Challenge
`ChallengeManager` seeds modifiers from `DateTime.UtcNow.Date` → integer seed → `Random.InitState()`. Same seed = same modifiers for all players worldwide on a given date. Modifiers: `LowFuel`, `ExtremeWind`, `UltraSlippery`, or combinations. The `Daily7Streak` achievement tracks completion of 7 daily challenges.

### Speedrun Mode
`SpeedrunManager` activates a visible timer at run start. Time is compared against `SaveData.speedrunPB` on victory. Sub-2-hour completion unlocks the `Efficiency` achievement. Leaderboard integration via `LeaderboardManager` posts times to Steam.

### Ghost System
`GhostSystem` records at 20 fps (positions + rotations + grapple state). Up to 1 hour of data (72,000 frames). Ghost file saved to `Application.persistentDataPath` as binary. On playback, a semi-transparent avatar follows the recorded path with linear position interpolation between frames. Ghost memory is a known technical risk (see TECHNICAL_ARCHITECTURE.md).

### Challenge Mode
`ChallengeManager` + `ChallengeModeHUD` support named challenge runs with custom modifier presets. Challenge completions are logged to `SaveData.completedChallenges`.

---

## 11. Cosmetics Policy

All cosmetics are zero gameplay impact. No cosmetic item affects `frictionCoefficient`, `gripMultiplier`, `thrustForce`, `maxEnergy`, or any numeric gameplay parameter.

**Available cosmetic types** (post-launch):
- Suit skins (player model material override)
- Grapple skins (grapple head material override)
- Rope colors (LineRenderer gradient override)
- Particle trails (instantiated as child of player transform)

All cosmetics are stored as `CosmeticItem` ScriptableObjects. Loadout is persisted to PlayerPrefs via `CosmeticSystem.SaveLoadout()`. Cosmetics are unlockable through play (achievements, challenge completions) or purchasable post-launch at cosmetic-only price points.

---

## 12. Difficulty Philosophy

Titan Ascent has no difficulty slider. The difficulty is the physics. However, several design decisions make the game approachable without compromising its identity:

- **Soft aim assist** on grapple targeting (configurable off in settings).
- **Coyote time system** (`CoyoteTimeSystem`) extends the jump/grapple window by 0.1–0.15 sec after leaving a surface.
- **Emergency recovery window** for large falls — a lifeline that rewards quick thinking.
- **Visible best-height marker** so players can see their own progress across sessions.
- **`MovementPresets`** — three preset configurations (Accessible, Standard, Authentic) that tune mass, drag, thruster force, and grip drain rate. These are tuning presets, not difficulty modes — Authentic matches the design intent.

The game is hard. It is supposed to be hard. The design does not apologize for this. It does, however, ensure that every death teaches something.

---

## 13. Success Metrics

| Metric | Target | Notes |
|---|---|---|
| Average session length | 25–40 min | Enough to feel investment without exhaustion |
| First summit rate | <5% of players | Reaching the crown should feel rare |
| Return rate (Day 7) | >35% | Game should pull players back |
| Average falls before summit | 80–150 | Calibrates against "too easy / too hard" |
| Clip share rate | Track via Steam broadcast + social | Viral moments pillar validation |
| Daily challenge completion | >15% of daily players | Challenge design validation |
| Speedrun sub-2h | <1% of players | Correct difficulty calibration for achievement |
