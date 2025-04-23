# Player Death and Respawn System Documentation

## Overview
This document summarizes the implementation of a World of Warcraft-style death and respawn system for Cosmicrafts Battlegrounds. The system features:
- Player transitions to a ghostly "soul" form upon death
- Camera effects during death sequence
- Respawn countdown UI with manual respawn option
- Fixed issues with player combat after respawning

## Key Components

### Camera Effects (CameraController.cs)
- Simplified zoom-in effect during player death
- Support for a death effect overlay (semi-transparent dark UI panel)
- Camera resets properly on respawn

### Soul Transformation (Unit.cs)
- Player appears as a ghostly/transparent blue-white entity after death
- Applied via material property modifications (transparency and color tinting)
- Basic explosion effect at death moment

### Respawn UI (UIGameMng.cs)
- Added respawn countdown display showing time until auto-respawn
- Implemented manual "Respawn Now" button
- UI properly appears on death and disappears on respawn

### Respawn Handling (GameMng.cs)
- Split into waiting and execution phases
- Tracks pending respawn unit for better control
- Added forced respawn functionality
- Properly restores player's materials, position, rotation
- Re-enables all components after respawn

## Fixed Issues

### Enemy Detection After Respawn
A critical bug was fixed where the player couldn't shoot enemies after respawning. The problem was:
- The EnemyDetector collider's OnTriggerEnter wouldn't fire for enemies already in range after respawn
- Added a `ResetEnemyDetection` coroutine that:
  1. Temporarily disables and re-enables the detector collider
  2. Manually detects enemies within range
  3. Populates the Shooter's target list with nearby enemies

## Setup Instructions

1. **Death Effect Overlay:**
   - Create a UI panel in your Canvas that covers the screen
   - Make it semi-transparent dark gray or blue (40% opacity)
   - Assign to CameraController's `deathEffectOverlay` field
   - Ensure it's inactive by default

2. **Respawn UI:**
   - Create a panel with countdown text and respawn button
   - Assign to UIGameMng's respawn UI fields
   - Ensure button is properly connected for manual respawns

## Future Enhancements

Potential improvements to consider:
- Add screen desaturation effect using a custom shader
- Implement floating/levitation for the soul using animation
- Add soul-specific particle effects
- Implement penalty or bonus for waiting vs. immediate respawn

## Technical Notes

- Material changes are applied at runtime and reset on respawn
- The system relies on proper faction settings to detect enemies
- Camera effects are implemented without post-processing requirements
- The respawn system is robust against game ending during respawn sequence 