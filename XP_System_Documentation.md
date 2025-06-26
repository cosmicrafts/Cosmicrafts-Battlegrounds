# Cosmicrafts XP System Documentation

## Overview
The XP system in Cosmicrafts is designed to reward players for destroying enemy units and provide progression through level-ups. The system is integrated with the existing unit destruction mechanics and UI framework. It uses Scriptable Objects for level persistence and features visual feedback through floating numbers.

## Core Components

### 1. Player Class (`Player.cs`)
```csharp
[Header("XP System")]
[Range(0, 9999)]
public int CurrentXP = 0;
[Range(0, 9999)]
public int MaxXP = 100;
[Range(1, 99)]
public int PlayerLevel = 1;
public int XPPerKill = 10; // Base XP gained from killing a unit

// Reference to the character SO for persistence
private CharacterBaseSO characterSO;
```

#### Key Methods:
- `AddXP(int amount)`: Handles XP gain and level-up checks
- `LevelUp()`: Manages level progression and bonuses
- `SaveProgress()`: Saves current level to CharacterBaseSO
- `LoadProgress()`: Loads level from CharacterBaseSO

### 2. UnitXP Class (`UnitXP.cs`)
A specialized Unit class that handles XP-related functionality:
```csharp
public class UnitXP : Unit
{
    [Header("XP Display Settings")]
    [SerializeField] private float xpNumberDuration = 1f;
    [SerializeField] private float xpNumberFloatSpeed = 1f;
    [SerializeField] private float xpNumberFadeSpeed = 1f;
    [SerializeField] private Color xpGainColor = new Color(0.2f, 0.8f, 0.2f);
}
```

#### Key Features:
- Inherits from Unit class
- Handles XP calculation and award
- Manages floating number display
- Integrates with UIUnit for visual feedback

### 3. UIUnit Class (`UIUnit.cs`)
Enhanced UI system for units with floating numbers:
```csharp
[Header("Floating Numbers")]
[SerializeField] private GameObject floatingNumberPrefab;
[SerializeField] private Transform floatingNumberContainer;
[SerializeField] private float defaultDuration = 1f;
[SerializeField] private float defaultFloatSpeed = 1f;
[SerializeField] private float defaultFadeSpeed = 1f;
```

#### Key Features:
- Floating number system for damage and XP
- Configurable duration, float speed, and fade speed
- Automatic cleanup of finished floating numbers
- Team-specific colors for visual feedback

### 4. CharacterBaseSO
The CharacterBaseSO class provides level persistence:
```csharp
[Header("Level Override")]
[Range(1, 99)]
[SerializeField]
private int levelOverride = -1;

public void UpdateLevel(int newLevel)
{
    levelOverride = Mathf.Clamp(newLevel, 1, 99);
}
```

### 5. UI Management (`UIGameMng.cs`)
```csharp
[Header("XP System UI")]
public TMP_Text LevelLabel;
public TMP_Text XPLabel;
public Image XPBar;

public void UpdateXP(int currentXP, int maxXP, int level)
{
    LevelLabel.text = $"Level {level}";
    XPLabel.text = $"{currentXP}/{maxXP} XP";
    XPBar.fillAmount = (float)currentXP / maxXP;
}
```

## XP Calculation
- Base XP for killing a level 1 unit: 10 XP
- XP scaling uses a logarithmic scale with base 2
- Formula: `XP = baseXP * log2(unitLevel + 1)`
- Example XP rewards:
  - Level 1 unit: 10 XP
  - Level 2 unit: 15.8 XP
  - Level 3 unit: 20 XP
  - Level 4 unit: 23.2 XP
  - Level 5 unit: 25.8 XP
- Base stations award 5x normal XP

## Visual Feedback System

### 1. Floating Numbers
- Configurable duration, float speed, and fade speed
- Color-coded for different types (damage, XP)
- Automatic cleanup system
- Smooth animation and fade effects

### 2. UI Updates
- Real-time XP bar updates
- Level display
- Current/Max XP display
- Automatic updates on:
  - XP gain
  - Level up
  - Game start

## Level Up Benefits
1. Player Level increases
2. Max Energy increases by 2
3. Energy fully restored
4. XP requirement increases by 50%
5. Level persistence through CharacterBaseSO

## Implementation Details

### 1. XP Gain Flow
1. Unit is destroyed
2. UnitXP calculates XP reward based on unit level
3. Player's `AddXP()` method is called with the reward
4. Floating number displays XP gained
5. Level up check is performed
6. UI is updated

### 2. Level Persistence
1. Player level is stored in CharacterBaseSO
2. Level is loaded on game start
3. Level is saved after each level up
4. Level override is applied to new units

### 3. Debug System
Comprehensive logging for:
- XP gains
- Level ups
- UI updates
- Null checks
- Floating number creation and cleanup

## Future Expansion Ideas

### 1. Enhanced Progression
- **Skill Points**: Award skill points on level up
- **Ability Unlocks**: New abilities at specific levels
- **Unit Upgrades**: Level-based unit improvements
- **Prestige System**: Reset with permanent bonuses

### 2. XP Mechanics
- **Combo System**: Bonus XP for quick successive kills
- **Objective XP**: XP rewards for completing objectives
- **Team XP**: Shared XP for team-based gameplay
- **XP Boosters**: Temporary XP multipliers

### 3. Level Up Benefits
- **Stat Increases**: 
  - Unit damage
  - Unit health
  - Energy regeneration
  - Movement speed
- **New Features**:
  - Additional unit slots
  - Special abilities
  - Resource generation
  - Defensive bonuses

### 4. UI Enhancements
- **Level Up Effects**: Visual and sound effects
- **XP Gain Indicators**: Enhanced floating numbers
- **Progress Predictions**: Time to next level
- **Milestone Rewards**: Special rewards at key levels

### 5. Balance Features
- **Dynamic Scaling**: Adjust XP requirements based on performance
- **Catch-up Mechanics**: Bonus XP for lower-level players
- **XP Caps**: Prevent excessive leveling
- **Level-based Matchmaking**: Match players by level

### 6. Social Features
- **Level Leaderboards**: Compare progression
- **Level-based Achievements**: Milestone rewards
- **Level-based Titles**: Special player titles
- **Level-based Emotes**: Unlockable emotes

### 7. Technical Improvements
- **Save System**: Persist XP and level data
- **Analytics**: Track progression patterns
- **Anti-Cheat**: Validate XP gains
- **Cloud Sync**: Cross-device progression

## Implementation Priority
1. Core progression system (current)
2. Enhanced level-up benefits
3. UI improvements
4. Save system
5. Social features
6. Advanced mechanics

## Notes
- Current system is designed for easy expansion
- Debug logs help track system behavior
- UI components are modular and reusable
- XP calculations are centralized for easy balancing
- Floating number system provides clear visual feedback
- UnitXP class allows for easy extension of XP functionality 