# Cosmicrafts Kinetic Animation System Documentation

## Overview
The animation system in Cosmicrafts Battlegrounds utilizes a purely kinetic (code-based) approach for all unit visual animations. This system eliminates the need for traditional Animator components and animation clips for standard unit states, offering significant performance benefits, easier customization, and highly responsive visuals.

It handles idle, movement, attack, entry, warp, power-up, and death states procedurally by directly manipulating the unit's visual transform (`kineticTransform`).

## Core Component: UnitAnimLis

The `UnitAnimLis.cs` script is the heart of the kinetic animation system. It should be attached to the GameObject containing the unit's visual model (the object that will be animated).

**Key Responsibilities:**
- Manages all animation calculations based on selected styles.
- Applies procedural effects like hover, banking, pitching, recoil, and shake.
- Executes one-shot animations (Entry, Warp, PowerUp, Death) via coroutines.
- Provides customizable style presets for each animation type via Inspector enums.
- Maintains animation event timings through code (`AE_` methods).

**Setup:**
1. Attach `UnitAnimLis` to the visual model GameObject.
2. Assign the visual model's `Transform` to the `kineticTransform` field in the Inspector.
3. Select the desired animation `Style` for each state (Idle, Move, Attack, etc.) in the Inspector.

## Animation States & Styles

Instead of Animator states, the system uses internal logic combined with selectable styles to define behavior:

### 1. Idle State
Active when the unit is not moving or attacking.
- **`IdleStyle` Enum:**
    - `GentleHover`: Subtle up/down bobbing, minimal side-to-side wobble.
    - `HeavyFloat`: Slower, larger bobbing, noticeable pitch/roll wobble.
    - `AgitatedBuzz`: Fast, small-amplitude bobbing, quick random jitters.

### 2. Moving State
Active when `Unit.IsMoving()` is true.
- **`MoveStyle` Enum:**
    - `SmoothFlight`: Moderate banking into turns, gentle pitch on acceleration, slight forward lean.
    - `AggressiveStrafe`: Sharp, high-angle banking, pronounced pitch, significant forward lean, quick recovery to neutral.
    - `HeavyDrift`: Slow, low-angle banking/pitch, minimal lean, feels weighty and less responsive.

### 3. Attacking State
Active when `Shooter.IsEngagingTarget()` is true.
- **`AttackStyle` Enum:**
    - `QuickRecoil`: Sharp but small upward pitch recoil on firing, quick recovery, minimal shake.
    - `HeavyCannon`: Large upward pitch recoil, slower recovery, noticeable model shake.
    - `EnergyPulse`: Minimal physical recoil/shake; primarily intended to pair with visual effects (scaling, glow) triggered by `AE_` events.

### 4. Entry Animation (One-Shot)
Played automatically on spawn (`autoPlayEntryAnimation`) or via `Unit.PlayEntryAnimation()`.
- **`EntryStyle` Enum:**
    - `FastDrop`: Unit quickly drops from slightly above, bounces gently, and scales briefly larger.
    - `PortalEmerge`: Unit scales up from zero size at its spawn point (best with a portal visual effect).
    - `Materialize`: Unit fades into view while scaling up (requires shader support, triggered by `AE_EntryStart`).

### 5. Warp Animation (One-Shot)
Triggered by `Unit.PlayWarpAnimation()` or player input.
- **`WarpStyle` Enum:**
    - `QuickBlink`: Very short duration, scales down then back up rapidly, quick spin.
    - `PhaseShift`: Short duration, subtle scale and position wobble (best with a blur/distortion shader).
    - `StreakingWarp`: Longer duration, stretches scale along the direction of movement (best with a trail effect).

### 6. PowerUp Animation (One-Shot)
Triggered by `Unit.PlayPowerUpAnimation()`.
- **`PowerUpStyle` Enum:**
    - `SteadyGlow`: Smooth pulsing scale effect that fades out over the duration.
    - `EnergySurge`: Faster, sharper scale pulses with a quicker fade-out (good for intense buffs).
    - `ShieldOvercharge`: Slower, larger scale pulses (intended to pair with shield visuals).

### 7. Death Animation (One-Shot)
Triggered internally when `Unit.Die()` calls `PlayDeathAnimation()`.
- **`DeathStyle` Enum:**
    - `QuickExplosion`: Unit rapidly spins and tumbles while shrinking/fading (pairs with `AE_BlowUpUnit` visual).
    - `DamagedFall`: Unit slowly spins and tumbles based on the last impact direction while falling.
    - `EngineFailure`: Erratic spinning and tumbling, visual flickering of scale/position, then falls (pairs well with spark effects).

## Configuration

- **`kineticTransform`**: **Crucial.** Assign the root visual transform here.
- **Style Enums**: Select the desired visual style for each animation state.
- **`debugAnimations`**: Enable console logs for state changes and event triggers.

*(Note: The detailed parameters like `currentHoverAmount`, `currentMaxBankAngle`, etc., are now derived internally from the selected Styles and marked ReadOnly in the Inspector for clarity.)*

## Integration with Game Systems

- **`Unit.cs`**: Triggers `PlayWarpAnimation`, `PlayPowerUpAnimation`, `PlayEntryAnimation`, and initiates the death sequence which calls `PlayDeathAnimation`. Also provides movement status (`IsMoving`) and last impact position.
- **`Shooter.cs`**: Provides attack status (`IsEngagingTarget`) and cooldown timing (`GetAttackCooldownProgress`) used for syncing attack recoil/shake.
- **`PlayerMovement.cs`**: Can trigger `PlayWarpAnimation` directly for player dashes.

## Animation Events (`AE_` Methods)

Even without an Animator, the system simulates animation events using the `AE_` methods within `UnitAnimLis.cs`. These are called at the start/end of the kinetic animation coroutines or at specific logical points.

- `AE_EntryStart` / `AE_EntryEnd`
- `AE_WarpStart` / `AE_WarpEnd`
- `AE_PowerUpStart` / `AE_PowerUpEnd`
- `AE_AttackStart` / `AE_FireWeapon` (Less critical now, but still available)
- `AE_EndDeath` (Called when the death *animation* finishes)
- `AE_BlowUpUnit` (Called *during* the death animation to trigger visual explosion)

Use these methods to hook up particle effects, sound effects, shader changes, or other logic that needs to sync with the kinetic animations.

## Benefits

- **Performance**: Significantly lighter than Animator + Animation Clips.
- **Responsiveness**: Animations react instantly to state changes.
- **Customization**: Easily define unit archetypes via Style enums.
- **Scalability**: Adding new units doesn't require creating new animation assets.
- **Simplicity**: Reduces reliance on complex Animator Controllers.

## Best Practices

1.  **Assign `kineticTransform` Correctly**: This is essential for the system to work.
2.  **Choose Appropriate Styles**: Select styles that match the unit's role and feel (e.g., HeavyDrift for a slow tank, AggressiveStrafe for a fighter).
3.  **Use `AE_` Events for Effects**: Trigger particles, sounds, and shaders from the `AE_` methods for proper timing.
4.  **Coordinate Visuals**: Some styles (PortalEmerge, Materialize, PhaseShift, StreakingWarp, EnergyPulse) strongly suggest accompanying visual effects (particles, shaders) for the best result.
5.  **Test Extensively**: Adjust styles and timings to achieve the desired look and feel for each unit.

## Troubleshooting

- **Animation Not Playing**: Check if `kineticTransform` is assigned. Ensure the parent `Unit` component exists. Check for console errors.
- **Looks Stiff/Unnatural**: Adjust the Style enum selections. Consider if custom parameters are needed (requires modifying the script).
- **Special Animations Don't Trigger**: Ensure the corresponding `Unit.cs` methods (`PlayWarpAnimation`, etc.) are being called correctly.
- **Death Animation Issues**: Verify `Unit.Die()` is called and that it correctly triggers `PlayDeathAnimation` in `UnitAnimLis`. Ensure the `deathStyle` is set. 