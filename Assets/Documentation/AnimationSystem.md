# Cosmicrafts Animation System Documentation

## Overview
The animation system in Cosmicrafts Battlegrounds handles all animation states for units and spaceships. It provides a consistent framework for managing transitions between idle, movement, attack, special state animations, and intro animations when units first appear.

## Core Components

### UnitAnimLis (Unit Animation Listener)
This is the primary class responsible for controlling animations on units. It:
- Interfaces with the Unit and Shooter components
- Handles state transitions based on unit status
- Manages animation speeds and blending
- Processes animation events
- Provides entry animation support for unit spawning

### Animation States
The system supports several animation states:
1. **Idle**: Default state when unit is not moving or attacking
2. **Moving**: Active when the unit is in motion
3. **Attacking**: Triggered when the unit is firing weapons
4. **Entry**: Intro animation played when unit first appears (triggered animation)
5. **Warp**: Special effect for teleporting/dashing (triggered animation)
6. **PowerUp**: Special effect for spell buffs (triggered animation)
7. **Death**: Final animation played on unit destruction

## Required Components for Unit Setup

### On the Unit GameObject:
- Unit script
- Shooter script (for attack animations)
- Rigidbody (for physics and movement)

### On the Model/Mesh Child GameObject:
- Animator component with properly configured animator controller
- UnitAnimLis script

## Animator Setup

### Required Parameters
- **Idle** (bool): Set when unit is not moving or attacking
- **Moving** (bool): Set when unit is in motion
- **Attacking** (bool): Set when unit is firing weapons
- **Entry** (trigger): Triggered when unit spawns
- **Die** (trigger): Triggered when unit is destroyed
- **Warp** (trigger): Triggered for teleport/dash effects
- **PowerUp** (trigger): Triggered for buff effects

### Optional Parameters
- **AttackSpeed** (float): Controls attack animation speed
- **MoveSpeed** (float): For blend trees to control movement animation speed

## Configuration Options

### Basic Settings
- **movementBlendTime**: Controls transition time between idle and movement animations
- **syncAttackWithWeaponSpeed**: Automatically adjusts attack animation speed to match weapon cooldown
- **debugAnimations**: Enables console logging for animation state changes
- **autoPlayEntryAnimation**: When enabled, automatically plays entry animation when unit spawns

### Advanced Settings
- **autoUpdateAnimatorParams**: Toggles automatic parameter updates
- **updateFrequency**: Controls how often animator parameters are updated (in seconds)

## Animation Events
The system supports several animation events that can be called from keyframes:

- **AE_EntryStart/End**: Marks beginning/end of entry/intro animation
- **AE_PowerUpStart/End**: Marks beginning/end of power-up animation
- **AE_WarpStart/End**: Marks beginning/end of warp animation
- **AE_AttackStart**: Called when attack animation begins
- **AE_FireWeapon**: Exact moment when weapon fires during attack animation
- **AE_EndDeath**: Called when death animation completes
- **AE_BlowUpUnit**: Triggers explosion effects

## Integration with Other Systems

### Unit Class Integration
The animation system relies on several methods from the Unit class:
- `IsMoving()`: Determines if unit is in motion
- `GetCurrentSpeed()`: Retrieves current movement speed
- `GetNormalizedSpeed()`: Gets speed as a 0-1 normalized value
- `PlayEntryAnimation()`: Triggers the entry animation
- `PlayPowerUpAnimation()`: Triggers power-up effect
- `PlayWarpAnimation()`: Triggers warp/dash effect
- `RefreshAnimationState()`: Forces animation state update

### Shooter Class Integration
- Uses `IsEngagingTarget()` to determine attack state
- Uses `CoolDown` to synchronize attack animation speed

## Implementation Examples

### Basic Unit Setup
```csharp
// Attach UnitAnimLis to the model containing the Animator
UnitAnimLis animController = modelObject.AddComponent<UnitAnimLis>();
animController.movementBlendTime = 0.2f;
animController.syncAttackWithWeaponSpeed = true;
animController.autoPlayEntryAnimation = true;
```

### Triggering Special Animations
```csharp
// Get reference to Unit component
Unit unit = GetComponent<Unit>();

// Trigger entry animation
unit.PlayEntryAnimation();

// Trigger a warp animation
unit.PlayWarpAnimation();

// Trigger a power-up animation
unit.PlayPowerUpAnimation();
```

## Best Practices
1. Ensure all required animator parameters are present in your animator controllers
2. Use animation events for precise timing of effects and behaviors
3. For complex units, consider using blend trees for smoother transitions
4. Keep animations relatively short for responsive gameplay
5. Use the debug option during development to verify state transitions
6. The Entry animation should be short and visually impactful

## Setting Up Entry Animation
1. Create an entry animation clip for your unit (typically 1-2 seconds long)
2. Set up a transition from Entry state to Idle in your animator controller
3. Add "Entry" trigger parameter to your animator
4. Place AE_EntryStart and AE_EntryEnd events at appropriate frames
5. Configure entry visuals and effects in the AE_EntryStart event handler

## Common Issues and Solutions
- **Animations not playing**: Verify animator has all required parameters
- **Jerky transitions**: Adjust blend times or use crossfade transitions
- **Attack animations out of sync**: Enable syncAttackWithWeaponSpeed
- **Animation events not firing**: Check event names match exactly in the animation clips
- **Entry animation not playing**: Verify autoPlayEntryAnimation is enabled and Entry trigger exists 