# Cosmicrafts Battlegrounds - Gameplay Systems and Rebuild Roadmap

## Current Gameplay Systems Overview

### Core Gameplay Loop

Cosmicrafts Battlegrounds is a real-time strategy game where players deploy units from their deck to destroy the opponent's base while defending their own. The gameplay revolves around:

1. Energy management (regenerating resource)
2. Unit deployment in strategic locations
3. Automated unit combat and movement
4. Base destruction win condition

### Key Systems

1. **Unit System**
   - Unit base class with health, shields, and team affiliation
   - Ship extension for movement capabilities
   - Combat through Shooter component
   - Visual representation with UI elements

2. **Energy and Card System**
   - Energy regenerates over time
   - Players have a deck of 8 cards (units and spells)
   - Cards have energy costs to deploy
   - NFT integration for unit stats

3. **Spawn System**
   - Units can only be deployed in designated spawn areas
   - Spawn areas are attached to existing friendly units
   - Drag and drop interface for placement

4. **Team-Based Combat**
   - Units automatically target enemies in range
   - Units belong to either Red or Blue team
   - Projectile system with different trajectory types

5. **Game State Management**
   - GameMng singleton manages global game state
   - Tracks all active units and spells
   - Handles win conditions and game over state

6. **NFT Integration**
   - Units load stats from NFT data
   - Scriptable objects define unit properties
   - Integration with blockchain services

## Architectural Issues in Current Implementation

1. **Monolithic Classes**
   - Several large classes with multiple responsibilities
   - Heavy use of inheritance over composition
   - Tight coupling between systems

2. **Singleton Pattern Overuse**
   - Heavy reliance on static references (GameMng, Player)
   - Difficult to test components in isolation
   - Creates hidden dependencies

3. **Update Method Inefficiency**
   - Many components use Update() for continuous checks
   - No batching of similar operations
   - Performance impact with many units

4. **Direct GameObject Manipulation**
   - Frequent use of instantiate/destroy for objects
   - No object pooling for frequently created objects
   - GC pressure from temporary objects

5. **Mixed Responsibilities**
   - UI and gameplay logic mixed in same components
   - Data and behavior tightly coupled
   - Difficulty extending or replacing components

## Rebuild Roadmap

### Phase 1: Core Architecture Setup

**Goal:** Establish a modular, extensible foundation with clean separation of concerns.

#### Tasks:

1. **Create Entity Component System (ECS) Foundation**
   - Define component interfaces for core capabilities
   - Implement entity container/manager
   - Create system managers for specific game functions

2. **Setup Service Locator Pattern**
   - Replace singletons with service locator
   - Register game services through DI container
   - Create interfaces for all major services

3. **Implement Event System**
   - Create game-wide event bus
   - Define core game events
   - Setup subscription mechanism

4. **Design Data Layer**
   - Create data contracts separate from game logic
   - Setup scriptable object architecture
   - Implement serialization system for game state

**Testing Criteria:**
- Verify component registration and retrieval
- Validate event propagation between systems
- Confirm clean separation between data and logic
- Test system initialization and teardown

### Phase 2: Resource and Input Systems

**Goal:** Build resource management and player input handling with clean separation.

#### Tasks:

1. **Energy System Implementation**
   - Create energy manager service
   - Implement configurable regeneration
   - Add energy modification events

2. **Card System Framework**
   - Design card data structure
   - Implement deck management
   - Setup card selection interface

3. **Input Handling System**
   - Create input abstraction layer
   - Implement mouse/touch handling
   - Build command pattern for actions

4. **UI Framework**
   - Design UI service architecture
   - Implement resource displays
   - Create card UI system

**Testing Criteria:**
- Verify energy regeneration works as expected
- Test deck loading and card selection
- Validate input handling across devices
- Confirm UI updates correctly

### Phase 3: Unit and Combat Systems

**Goal:** Create modular, performant unit and combat systems.

#### Tasks:

1. **Unit Component System**
   - Implement health component
   - Create shield component
   - Build team affiliation component
   - Design unit stats component

2. **Movement System**
   - Implement navigation component
   - Create movement strategies (patterns)
   - Build path planning system
   - Design obstacle avoidance

3. **Combat System**
   - Create targeting component
   - Implement attack system
   - Build projectile manager with object pooling
   - Design damage resolution system

4. **Object Pooling**
   - Implement generic object pool
   - Create specialized pools for projectiles, effects
   - Setup automatic pool sizing

**Testing Criteria:**
- Verify units behave correctly in isolation
- Test combat between units of different teams
- Validate movement and pathfinding
- Measure performance with many units

### Phase 4: Spawn System and Game Flow

**Goal:** Implement strategic unit deployment and game progression.

#### Tasks:

1. **Spawn Area System**
   - Create spawn area component
   - Implement spawn validation
   - Build visual indicators for valid placements

2. **Unit Deployment System**
   - Implement drag and drop functionality
   - Create unit preview visualization
   - Build deployment validation

3. **Game State Management**
   - Create game state machine
   - Implement win/loss conditions
   - Build turn/phase system if needed

4. **AI Foundation**
   - Create AI decision system
   - Implement basic opponent logic
   - Design difficulty scaling

**Testing Criteria:**
- Verify spawn areas work correctly
- Test unit deployment workflow
- Validate game state transitions
- Confirm AI makes reasonable decisions

### Phase 5: Visual and Effects Systems

**Goal:** Enhance game feel with visual feedback and effects.

#### Tasks:

1. **Visual Feedback System**
   - Implement health/shield visualization
   - Create damage number display
   - Build status effect indicators

2. **Animation System**
   - Create animation controller
   - Implement state-based animations
   - Build transition system

3. **Particle Effect System**
   - Implement effect manager with pooling
   - Create common effect templates
   - Build trigger system for effects

4. **Camera System**
   - Create camera control service
   - Implement focus/follow logic
   - Build screen shake and effects

**Testing Criteria:**
- Verify visual clarity of game state
- Test performance with many effects
- Validate animation transitions
- Confirm camera behavior

### Phase 6: Data Persistence and NFT Integration

**Goal:** Create robust data systems and NFT integration.

#### Tasks:

1. **Save/Load System**
   - Implement state serialization
   - Create save file management
   - Build auto-save functionality

2. **NFT Data Integration**
   - Design NFT data adapter
   - Create card-to-unit translation
   - Implement NFT property application

3. **Statistics Tracking**
   - Create stats tracking service
   - Implement player progression
   - Build achievements system

4. **Analytics and Telemetry**
   - Design analytics service
   - Implement event tracking
   - Create performance monitoring

**Testing Criteria:**
- Verify save/load functionality
- Test NFT data application to units
- Validate statistics tracking
- Confirm telemetry collection

### Phase 7: Optimization and Polish

**Goal:** Ensure game performs well and presents professionally.

#### Tasks:

1. **Performance Optimization**
   - Implement spatial partitioning for unit queries
   - Create LOD system for units
   - Build rendering optimizations

2. **Memory Management**
   - Audit and optimize garbage collection
   - Implement asset bundles/addressables
   - Create memory usage monitoring

3. **Polish and Feedback**
   - Add juice (screen shake, hit stop, etc.)
   - Implement sound effects system
   - Create tutorial and onboarding

4. **Quality Assurance**
   - Create automated tests
   - Implement error logging
   - Build debug visualization tools

**Testing Criteria:**
- Measure performance metrics
- Test memory usage over time
- Evaluate game feel and polish
- Verify error handling

## Architecture Best Practices for Implementation

### Component-Based Design

```csharp
// Instead of inheritance:
public class Ship : Unit { ... }

// Use composition:
public class Unit : MonoBehaviour
{
    [SerializeField] private HealthComponent health;
    [SerializeField] private ShieldComponent shield;
    [SerializeField] private TeamComponent team;
    // ...
}
```

### Service Locator Pattern

```csharp
// Instead of static references:
public static GameMng GM;

// Use service locator:
public class GameServices
{
    private static Dictionary<Type, object> services = new Dictionary<Type, object>();
    
    public static void Register<T>(T service) where T : class
    {
        services[typeof(T)] = service;
    }
    
    public static T Get<T>() where T : class
    {
        return (T)services[typeof(T)];
    }
}

// Usage:
var gameManager = GameServices.Get<IGameManager>();
```

### Event-Based Communication

```csharp
// Instead of direct calls:
GameMng.GM.DeleteUnit(this);

// Use events:
public static class GameEvents
{
    public static event Action<Unit> OnUnitDestroyed;
    
    public static void UnitDestroyed(Unit unit)
    {
        OnUnitDestroyed?.Invoke(unit);
    }
}

// In the unit:
private void OnDestroy()
{
    GameEvents.UnitDestroyed(this);
}

// In the manager:
private void Start()
{
    GameEvents.OnUnitDestroyed += HandleUnitDestroyed;
}

private void HandleUnitDestroyed(Unit unit)
{
    units.Remove(unit);
}
```

### Object Pooling

```csharp
public class ObjectPool<T> where T : Component
{
    private Queue<T> pool = new Queue<T>();
    private T prefab;
    private Transform parent;
    
    public ObjectPool(T prefab, int initialSize, Transform parent)
    {
        this.prefab = prefab;
        this.parent = parent;
        
        for (int i = 0; i < initialSize; i++)
        {
            CreateObject();
        }
    }
    
    private T CreateObject()
    {
        var obj = GameObject.Instantiate(prefab, parent);
        obj.gameObject.SetActive(false);
        pool.Enqueue(obj);
        return obj;
    }
    
    public T Get()
    {
        if (pool.Count == 0)
        {
            return CreateObject();
        }
        
        var obj = pool.Dequeue();
        obj.gameObject.SetActive(true);
        return obj;
    }
    
    public void Return(T obj)
    {
        obj.gameObject.SetActive(false);
        pool.Enqueue(obj);
    }
}

// Usage:
projectilePool.Get();
projectilePool.Return(projectile);
```

### Data-Oriented Design

```csharp
// Instead of behaviors with data:
public class Unit : MonoBehaviour
{
    public int HitPoints;
    
    public void TakeDamage(int amount)
    {
        HitPoints -= amount;
        if (HitPoints <= 0) Die();
    }
}

// Separate data from behavior:
[System.Serializable]
public struct UnitData
{
    public int maxHitPoints;
    public int currentHitPoints;
    public float shield;
    public int teamId;
}

public class HealthSystem : MonoBehaviour
{
    private List<UnitData> unitsData = new List<UnitData>();
    
    public void Update()
    {
        for (int i = 0; i < unitsData.Count; i++)
        {
            // Process all health-related logic here
            if (unitsData[i].shield > 0)
            {
                // Shield regeneration logic
            }
        }
    }
    
    public void ApplyDamage(int unitIndex, int amount)
    {
        var data = unitsData[unitIndex];
        
        if (data.shield > 0)
        {
            // Shield damage logic
        }
        else
        {
            data.currentHitPoints -= amount;
        }
        
        unitsData[unitIndex] = data;
        
        if (data.currentHitPoints <= 0)
        {
            GameEvents.UnitDied(unitIndex);
        }
    }
}
```

## Conclusion

Rebuilding Cosmicrafts Battlegrounds with a modern, modular architecture will significantly improve maintainability, performance, and extensibility. By breaking the rebuild into discrete phases with clear testing criteria, we ensure steady progress while maintaining a functional game at each milestone.

The architectural shift from inheritance-heavy, monolithic classes to a component-based design with clean separation of concerns will make the codebase more robust and easier to expand. Implementing systems like object pooling, event-based communication, and data-oriented design will improve performance, especially with large numbers of units.

Each phase builds on the previous one, ensuring that core functionality is solid before adding complexity. This approach allows for testing and validation at each step, reducing the risk of compounding issues. 