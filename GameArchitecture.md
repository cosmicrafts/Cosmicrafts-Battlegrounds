# Cosmicrafts Battlegrounds - Game Architecture Documentation

## Table of Contents
1. [Team Management System](#team-management-system)
2. [Unit Hierarchy & Core Components](#unit-hierarchy--core-components)
3. [Shooter Targeting System](#shooter-targeting-system)
4. [Ship Movement System](#ship-movement-system) 
5. [Companion System](#companion-system)
6. [Spawning & Object Pooling](#spawning--object-pooling)
7. [Game Manager References](#game-manager-references)
8. [AI Bot System](#ai-bot-system)
9. [Wave System](#wave-system)
10. [Flow Diagrams](#flow-diagrams)
11. [Troubleshooting Guide](#troubleshooting-guide)
12. [Future Extensions](#future-extensions)

---

## Team Management System

### Core Team Structure
- Each `Unit` has a `MyTeam` property (enum with `Blue`/`Red` values)
- `Team.Blue` represents the player's team
- `Team.Red` represents the enemy/bot team
- Each unit also has a `PlayerId` (1=Player, 2=Bot) which sometimes duplicates team info

### Team Assignment Process
- Initial team assignment occurs during unit creation in `GameMng.CreateUnit()`
- Player base is assigned `Team.Blue` in `GameMng.SpawnPlayerBase()`
- Enemy base is assigned `Team.Red` in `GameMng.SpawnEnemyBase()`
- Player-deployed units inherit `Player.MyTeam` (Blue)
- Bot-deployed units inherit `BotEnemy.MyTeam` (Red)

### Team Relationship Methods
The foundation of team-based targeting is in these methods in `Unit.cs`:

```csharp
// Checks if the provided unit is an ally (same team, not self)
public bool IsAlly(Unit otherUnit) {
    if (otherUnit == null || otherUnit == this) return false;
    return this.MyTeam == otherUnit.MyTeam;
}

// Checks if the provided unit is an enemy (different team)
public bool IsEnemy(Unit otherUnit) {
    if (otherUnit == null || otherUnit == this) return false;
    return this.MyTeam != otherUnit.MyTeam;
}
```

### Team-Based Visual Indicators
- `OutlineController` component applies team-colored outlines to units
- `UIUnit` displays health/shield bars with team-appropriate colors
- Spawned projectiles inherit their parent's team to calculate damage

---

## Unit Hierarchy & Core Components

### Base Unit Class
`Unit.cs` is the foundation for all game entities and handles:
- Team and player ID assignment
- Health and shield management
- Death and destruction logic
- Damage application (`AddDmg()`)
- Team relationship methods (`IsAlly()`, `IsEnemy()`)

### Specialized Unit Types
- `Ship.cs`: Extends `Unit` with movement capabilities
  - Controls speed, turning, and movement
  - Handles ship-specific animations and effects
  - Manages regeneration of health/shields

- `BaseStation`: Special type of stationary unit for each team's base
  - Player's base is controlled by `Player.cs`
  - Enemy's base is managed by `BotEnemy.cs` logic

- `CompanionUnit`: Specialized units that orbit and assist other units
  - Behavior managed by `CompanionController.cs`
  - Can have different roles (Attacker, Healer, Shield, Scout)

### Component Relationships
Units can have these major components:
- `Unit` (required): Base functionality and team identity
- `Ship` (optional): Movement capabilities
- `Shooter` (optional): Attack capabilities
- `CompanionController` (optional): For units that orbit/assist others
- `UIUnit` (typically required): Displays health/shields
- `OutlineController` (typically required): Visual team indication

---

## Shooter Targeting System

### Core Components
- `EnemyDetector`: SphereCollider set to radius of `DetectionRange`
- `DetectionRange`: Radius within which enemies are initially detected 
- `AttackRange`: Radius within which the unit can actually attack (≤ DetectionRange)
- `InRange`: HashSet storing all detected enemies in DetectionRange
- `Target`: Current target unit being engaged
- `currentState`: Either `Patrolling` (no target) or `Engaging` (has target)

### Range Visualization Options
```csharp
public enum RangeVisualizationType
{
    None,   // No visualization
    Circle, // Draw a circle representing the AttackRange
    Line    // Draw a line pointing forward representing the AttackRange
}
```

### Detection Flow
1. Enemy enters `EnemyDetector` trigger → `OnTriggerEnter` → `AddEnemy(Unit)` → adds to `InRange` HashSet
2. Enemy leaves `EnemyDetector` trigger → `OnTriggerExit` → `RemoveEnemy(Unit)` → removes from `InRange` HashSet 
3. The `InRange` HashSet is cleaned of invalid entries during target acquisition

### Shooter's Update Loop Logic
```
if (Unit is dead OR can't attack OR not in control)
    Clear target, reset state, return
    
if (no enemies in detection range)
    If was previously engaging, reset target and revert to patrol
    Return
    
Switch to Engaging state
    
if (current target invalid or out of range)
    Find new target (closest valid enemy)
    
if (have valid target)
    Move towards it if Ship component exists and StopToAttack is true
    Rotate towards it if RotateToEnemy is true
    
    if (target in AttackRange)
        ShootTarget()
else
    Reset destination if needed
```

### Target Selection
1. `FindNewTarget()` selects closest valid enemy from the `InRange` HashSet
2. Valid enemies must be: non-null, alive, and confirmed enemies via `MyUnit.IsEnemy()`
3. Priority is given to closer enemies (distance-based selection)

### Rotation & Movement Integration
1. If unit has target and `RotateToEnemy` is true:
   - `RotateTowardsTarget()` calculates direction to target
   - Applies smooth rotation using `Quaternion.Slerp` and `rotationSpeed`

2. If unit has `Ship` component and `StopToAttack` is true:
   - Calls `MyShip.SetDestination(Target.position, AttackRange * 0.9f)`
   - Ship moves until just inside attack range, then stops

### Attack Logic
1. `ShootTarget()` verifies target and checks cooldown (`DelayShoot <= 0`)
2. If ready, calls `FireProjectiles()` which:
   - Instantiates bullet prefabs from cannon positions
   - Sets bullet's team to match shooter's team
   - Configures bullet damage (regular or critical)
   - Plays visual muzzle flash effects
3. After firing, resets cooldown timer (`DelayShoot = CoolDown`)

### Range Visualizer
The shooter can display its attack range using different visualization methods:
- Circle: Shows 360° range as a circle around the unit
- Line: Shows a directional line indicating forward range
- Visualizer uses a LineRenderer component to draw the range indicator

---

## Ship Movement System

### Core Components
- `SteeringRig`: Handles path finding and obstacle avoidance
- Movement parameters: `MaxSpeed`, `Acceleration`, `TurnSpeed`, etc.
- `Thrusters`: Visual effects activated during movement

### Movement States
- Normal movement: Following patrol routes or moving to destination
- Combat movement: When engaged with targets (if `Shooter.StopToAttack` is true)
- Stationary: When stopped or destination reached

### Ship Methods
- `SetDestination(Vector3 pos, float stopDistance)`: Sets a new target destination
- `ResetDestination()`: Returns to default patrol behavior
- `SetCustomRotation(Quaternion rot)`: Override automatic rotation temporarily
- `Move()`: Core movement update handling acceleration, rotation and steering

### Regeneration System
Ships can regenerate shields and health based on configurable parameters:
- `ShieldRegenRate`: Shield points regenerated per second
- `RegenShieldWhileDamaged`: Whether shields can regenerate while taking damage
- `HPRegenRate`: Health points regenerated per second
- `HPRegenDelay`: Delay before health regeneration begins after taking damage

---

## Companion System

### Companion Configuration
`CompanionConfig` class defines:
- Reference to companion prefab
- Orbit distance and speed
- Visual settings (color, scale)
- Behavior type (Attacker, Healer, Shield, Scout)

### Companion Controller
The `CompanionController` component:
- Manages orbital movement around parent unit
- Handles special behaviors based on type
- Prevents targeting of parent or allies

### Companion Behaviors
```csharp
public enum CompanionBehaviorType
{
    Default,    // Standard orbiting behavior
    Attacker,   // Focuses on attacking enemies
    Healer,     // Provides healing to parent
    Shield,     // Enhances parent's shield
    Scout       // Orbits at greater distance and detects enemies
}
```

### Companion Targeting
Companions use the `CompanionTargetFilter` component to ensure they:
- Only target enemy units
- Never target their parent unit
- Never target other friendly units

---

## Spawning & Object Pooling

### Player Spawning
- `Player.DeplyUnit()`: Spawns units at the player's command
- Units are created via `GameMng.CreateUnit()` with `Team.Blue`
- Player manages energy costs for spawning

### BotEnemy Spawning 
- Uses object pooling for efficient unit management
- `GetUnitFromPool()`: Retrieves or creates units as needed
- `ReturnUnitToPool()`: Returns units to pool on death instead of destroying
- Pool organized by unit type (`Dictionary<ShipsDataBase, List<Unit>>`)

### Unit Creation Process
```csharp
// In GameMng.cs
public Unit CreateUnit(GameObject obj, Vector3 position, Team team, string nftKey = "none", int playerId = -1)
{
    // Instantiate the prefab
    GameObject unitObj = Instantiate(obj, position, Quaternion.identity);
    Unit unit = unitObj.GetComponent<Unit>();
    
    if (unit != null)
    {
        // Set IsDeath false first
        unit.IsDeath = false;
        
        // Set team and ID
        unit.MyTeam = team;
        unit.PlayerId = playerId == -1 ? (team == Team.Blue ? 1 : 2) : playerId;
        unit.setId(GenerateUnitId());
        
        // Apply NFT data if provided
        // [NFT application logic]
        
        // Register with manager
        AddUnit(unit);
    }
    
    return unit;
}
```

---

## Game Manager References

### GameMng (GM)
The central game manager handles:
- Spawning of player and enemy bases
- Unit creation and tracking
- Game state management
- Team targeting assignments

### Key Static References
- `GameMng.GM`: Main game manager singleton
- `GameMng.P`: Reference to Player component
- `GameMng.MT`: Metrics tracking 
- `GameMng.UI`: UI manager

### Important Collections
- `GameMng.Targets`: Array holding base stations [0]=Enemy (Red), [1]=Player (Blue)
- `GameMng.units`: List of all active units in the game

### Helper Methods
- `GetDefaultTargetPosition(Team)`: Returns position of opposing team's base
- `GetFinalTransformTarget(Team)`: Returns Transform of opposing team's base
- `GetColorUnit(Team, playerId)`: Returns team-appropriate color

---

## AI Bot System

### BotEnemy
The main AI controller that:
- Manages AI energy and unit spawning
- Decides which units to deploy
- Handles unit pooling and reuse
- Controls the "Red" team

### AI Decision Making
- Coroutine-based decisions every `waitSpawn` seconds
- Selection based on available energy
- Random positioning from predefined spawn points
- Limited by `maxActiveUnits` count

### BotEnemy Structure
```csharp
public class BotEnemy : MonoBehaviour
{
    // Bot identity
    public string botName = "DefaultScriptName";
    public int botLv = 5;
    public readonly int ID = 2;
    public readonly Team MyTeam = Team.Red;
    
    // Resources
    public float CurrentEnergy = 30;
    public float MaxEnergy = 30;
    public float SpeedEnergy = 5;
    
    // AI settings
    public float waitSpawn = 0.75f;
    public int maxActiveUnits = 8;
    
    // Unit management
    private List<Unit> activeUnits = new List<Unit>();
    private Dictionary<ShipsDataBase, List<Unit>> unitPool;
    
    // AI loop
    private IEnumerator IA() {
        // [Decision making logic]
    }
}
```

---

## Wave System

### WaveController
- Manages waves of enemies with increasing difficulty
- Disables default enemy base and replaces with wave-specific bases
- Handles progression between waves

### Wave Progression
1. Initial wave activated at start
2. When current wave's base is destroyed:
   - Deactivate current wave
   - Activate next wave prefab
   - Setup new enemy base
   - Clear player's deployed units
3. When all waves complete, player wins

---

## Flow Diagrams

### Unit Team Assignment Flow
```
GameMng.SpawnPlayerBase() → Unit.MyTeam = Team.Blue
GameMng.SpawnEnemyBase() → Unit.MyTeam = Team.Red
Player.DeplyUnit() → GameMng.CreateUnit(team=Player.MyTeam)
BotEnemy.CreateNewUnit() → GameMng.CreateUnit(team=BotEnemy.MyTeam)
```

### Target Acquisition Flow
```
Enemy enters EnemyDetector → OnTriggerEnter → AddEnemy(enemy) → InRange.Add(enemy)
Update() → if InRange.Count > 0 → currentState = Engaging → FindNewTarget()
FindNewTarget() → loops through InRange → selects closest valid enemy → SetTarget(closestEnemy)
```

### Ship Movement Flow
```
Unit enters Engaging state → SetDestination(Target.position, AttackRange*0.9f)
Ship.Move() → Calculate steering direction → Apply rotation → Apply velocity
Unit loses target → ResetDestination() → Return to patrol behavior
```

### Attack Flow
```
Update() → if Target valid && distance <= AttackRange → ShootTarget()
ShootTarget() → if DelayShoot <= 0 → FireProjectiles() → DelayShoot = CoolDown
FireProjectiles() → For each cannon → Instantiate bullet → Set properties → Play effects
```

### Death & Pooling Flow
```
Unit.AddDmg() → HitPoints <= 0 → Die()
Die() → IsDeath = true → OnUnitDeath.Invoke()
OnUnitDeath → BotEnemy.ReturnUnitToPool() → Deactivate → Add to pool
...Later...
BotEnemy.GetUnitFromPool() → Take from pool → ResetUnit() → Reactivate
```

---

## Troubleshooting Guide

### Unit Not Rotating Towards Enemies
- **Check `RotateToEnemy` flag:** Ensure it's set to `true` on the Shooter component
- **Verify `EnemyDetector` setup:** 
  - Ensure the collider radius matches `DetectionRange`
  - Check that the collider is a trigger
  - Make sure its layer can interact with unit layers
- **Debug `InRange` collection:**
  ```csharp
  // Add in Shooter.AddEnemy()
  Debug.Log($"Added {enemy.name} to {gameObject.name}'s targets. Count: {InRange.Count}");
  ```
- **Validate rotation method:**
  ```csharp
  // Add in RotateTowardsTarget()
  Debug.Log($"Rotating towards {Target.name}, angle: {Vector3.Angle(transform.forward, direction)}");
  ```

### Unit Not Attacking
- **Check range values:** Ensure `AttackRange` is appropriate (not too small)
- **Verify distance calculation:** 
  ```csharp
  // Add in Update()
  if (Target != null)
      Debug.Log($"Distance to target: {Vector3.Distance(transform.position, Target.transform.position)}, AttackRange: {AttackRange}");
  ```
- **Check Cooldown and CanAttack:** Make sure `CoolDown` isn't too high and `CanAttack` is true
- **Verify `Bullet` and `Cannons`:** These must be properly assigned in the inspector

### Targeting Wrong Units
- **Check team assignment:**
  ```csharp
  // Add in Shooter.AddEnemy()
  Debug.Log($"Enemy check: {enemy.name} (Team: {enemy.MyTeam}) vs this unit (Team: {MyUnit.MyTeam})");
  ```
- **Validate IsEnemy logic:** Temporarily modify it to print debug info
  ```csharp
  public bool IsEnemy(Unit otherUnit) {
      bool result = (otherUnit != null && otherUnit != this && otherUnit.MyTeam != this.MyTeam);
      Debug.Log($"IsEnemy check: {this.name}({this.MyTeam}) vs {otherUnit?.name}({otherUnit?.MyTeam}) = {result}");
      return result;
  }
  ```
- **Check inheritance of team at creation:** In GameMng.CreateUnit(), verify team assignment

### Spawned Units Don't Behave Correctly
- **Verify prefab setup:** Ensure prefabs have all required components
- **Check initialization:** Debug the ResetUnit() method to ensure proper initialization
- **Monitor object pooling:** Validate the pool is managing units correctly

---

## Future Extensions

### Multi-Faction System
To support neutral or multiple faction types:

1. **Extend Team Enum**
   ```csharp
   public enum Team
   {
       Blue,    // Player
       Red,     // Enemy
       Neutral, // Non-hostile NPCs
       Bandit,  // Hostile to all
       Guard    // Friendly to Blue, hostile to Red/Bandit
   }
   ```

2. **Create Relationship Matrix**
   ```csharp
   // In a new FactionManager class
   public enum Relationship { Friendly, Neutral, Hostile }
   
   private static Relationship[,] relationshipMatrix = {
       // Blue  Red   Neutral Bandit Guard
       { Friendly, Hostile, Neutral, Hostile, Friendly }, // Blue's relationships
       { Hostile, Friendly, Neutral, Hostile, Hostile },  // Red's relationships
       { Neutral, Neutral, Neutral, Neutral, Neutral },   // Neutral's relationships
       { Hostile, Hostile, Neutral, Friendly, Hostile },  // Bandit's relationships
       { Friendly, Hostile, Neutral, Hostile, Friendly }  // Guard's relationships
   };
   
   public static Relationship GetRelationship(Team a, Team b) {
       return relationshipMatrix[(int)a, (int)b];
   }
   ```

3. **Update IsAlly and IsEnemy methods**
   ```csharp
   public bool IsAlly(Unit otherUnit) {
       if (otherUnit == null || otherUnit == this) return false;
       return FactionManager.GetRelationship(this.MyTeam, otherUnit.MyTeam) == Relationship.Friendly;
   }
   
   public bool IsEnemy(Unit otherUnit) {
       if (otherUnit == null || otherUnit == this) return false;
       return FactionManager.GetRelationship(this.MyTeam, otherUnit.MyTeam) == Relationship.Hostile;
   }
   
   public bool IsNeutral(Unit otherUnit) {
       if (otherUnit == null || otherUnit == this) return false;
       return FactionManager.GetRelationship(this.MyTeam, otherUnit.MyTeam) == Relationship.Neutral;
   }
   ```

### Advanced AI Behaviors
The current BotEnemy system could be extended with:

1. **Strategy Patterns**
   - Defensive: Focus on protecting the base
   - Aggressive: Prioritize attacking player
   - Economic: Build up resources before attacking
   
2. **Difficulty Scaling**
   - Adjustable parameters for different difficulty levels
   - Progressive scaling during gameplay
   - Customized unit stats based on difficulty

3. **Dynamic Target Selection**
   - Prioritize weakened units
   - Target player's resource gatherers
   - Focus fire on specific unit types 