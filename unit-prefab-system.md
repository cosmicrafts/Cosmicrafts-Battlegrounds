# Cosmicrafts Battlegrounds - Unit Prefab System Documentation

## Overview

Cosmicrafts Battlegrounds employs a modular unit system that centers around prefabs. Each unit/ship in the game is implemented as a prefab with attached components that define its behavior, appearance, and stats. The system allows for the creation of diverse units while maintaining a consistent architecture.

## Core Architecture

### Class Hierarchy

```
MonoBehaviour
  └── Unit (base class)
       └── Ship (specialized unit with movement capabilities)
```

### Component Composition

Units are composed of multiple components that handle specific aspects of functionality:

- **Unit**: Base component that manages health, shields, and core functionality
- **Ship**: Extends Unit with movement capabilities (only on mobile units)
- **Shooter**: Handles attack behavior and projectile management
- **UIUnit**: Manages the UI elements associated with the unit (health bars, etc.)
- **EDetector**: Detects enemies within range
- **SensorToolkit components**: Used for movement and obstacle avoidance

## Key Components

### Unit (Unit.cs)

The foundational class for all units in the game with the following features:

- **Health system**: Manages hit points and shields with regeneration
- **Team affiliation**: Units belong to either Blue or Red team
- **Death handling**: Manages unit destruction and death effects
- **Animation control**: Controls unit animations
- **Level system**: Units have levels that affect their stats
- **NFT integration**: Can be linked to NFT data to define stats

Key properties:
- `HitPoints`: Current health of the unit
- `Shield`: Current shield value
- `Level`: Unit's level (1-999)
- `MyTeam`: Team affiliation (Blue/Red)

Key methods:
- `AddDmg()`: Apply damage to the unit
- `Die()`: Handle unit death
- `SetNfts()`: Set NFT data for the unit

### Ship (Ship.cs)

Extends Unit with movement capabilities:

- **Movement system**: Controls unit movement with acceleration, max speed, and turning
- **Obstacle avoidance**: Uses SensorToolkit for intelligent pathfinding
- **Thruster effects**: Controls visual thruster effects based on movement

Key properties:
- `MaxSpeed`: Maximum movement speed
- `TurnSpeed`: How quickly the unit can rotate
- `AvoidanceRange`: Range for obstacle detection

Key methods:
- `Move()`: Handle movement logic
- `SetDestination()`: Set a target destination
- `ResetDestination()`: Reset to default destination

### Shooter (Shooter.cs)

Handles attack behavior:

- **Target acquisition**: Detects and selects targets to attack
- **Attack cooldown**: Manages firing rate
- **Projectile management**: Creates and configures projectiles
- **Critical hits**: Supports critical hit chance and damage multiplier

Key properties:
- `RangeDetector`: Attack range
- `CoolDown`: Time between attacks
- `BulletDamage`: Base damage of projectiles
- `criticalStrikeChance`: Chance to land a critical hit

Key methods:
- `ShootTarget()`: Attack current target
- `AddEnemy()`: Add enemy to potential targets
- `FindNewTarget()`: Select new target based on proximity

### Projectile (Projectile.cs)

Handles projectile behavior:

- **Movement types**: Supports different trajectory types (Straight, Wavering, Zigzag, Circular)
- **Damage application**: Applies damage to targets
- **AoE damage**: Can deal area-of-effect damage
- **Impact effects**: Visual effects on impact

Key properties:
- `Speed`: Projectile movement speed
- `Dmg`: Damage value
- `trajectoryType`: Movement pattern
- `IsAoE`: Whether projectile deals area damage

### UIUnit (UIUnit.cs)

Manages visual representation of unit stats:

- **Health bars**: Visual representation of hit points
- **Shield bars**: Visual representation of shields
- **Team colors**: Different colors based on team
- **Damage animation**: Visual feedback when unit takes damage

Key methods:
- `SetHPBar()`: Update health bar display
- `SetShieldBar()`: Update shield bar display
- `OnDamageTaken()`: Trigger damage animation

### EDetector (EDetector.cs)

Simple component that detects enemies and notifies the Shooter:

- Detects when enemies enter/exit attack range
- Adds/removes enemies from the Shooter's potential target list

## NFT Integration

The system supports NFT integration through the `NFTsUnit` class:

- `HitPoints`: Health from NFT data
- `Shield`: Shield value from NFT data
- `Dammage`: Attack damage from NFT data
- `Speed`: Movement speed from NFT data

The `Unit.SetNfts()` method applies these values to the unit.

## Prefab Structure

A typical unit prefab contains:

1. Root GameObject with `Unit` component and colliders
2. Mesh GameObject with visual representation
3. UI elements (health bars, level indicator)
4. Weapon points/cannons
5. Thruster effects (for ships)
6. Sensor components for movement and targeting

## Team System

Units belong to either:
- `Team.Blue`
- `Team.Red`

Teams affect:
- UI colors
- Target selection (only target opposite team)
- Movement destination

## Conclusion

The unit prefab system in Cosmicrafts Battlegrounds provides a flexible, modular approach to unit creation. By combining various components and configuring their properties, a wide variety of units can be created while maintaining consistent core functionality. The NFT integration allows for unique unit properties based on NFT data.

The system separates concerns well:
- Core unit logic (Unit.cs)
- Movement (Ship.cs)
- Combat (Shooter.cs, Projectile.cs)
- Enemy detection (EDetector.cs)
- Visual representation (UIUnit.cs)

This separation makes the system maintainable and extensible for future development. 