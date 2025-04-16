# Explosion Spell Prefab Setup Guide

This document guides you through setting up the Explosion Spell (Spell_02) prefab properly in Unity.

## 1. Create the Base Prefab

1. Create a new empty GameObject in your scene
2. Name it `Spell_02_Explosion`
3. Add the following components:
   - `Spell_02` script
   - `Audio Source` (for explosion sound)

## 2. Add Visual Effects

1. Create a child GameObject named `ExplosionVFX`
2. Add a Particle System component to it
3. Configure the Particle System:
   - Set Start Lifetime to 1-2 seconds
   - Set Start Speed to around 5-10
   - Set Start Size to around 0.5-1
   - Set Start Color to orange-yellow gradient
   - Enable `Shape > Sphere` with radius around 1
   - Enable `Color over Lifetime` with fade-out
   - Add a material with `Particles/Additive` shader

4. In the `Spell_02` component, drag the Particle System to the `Explosion Effect` field

## 3. Add Audio Effects

1. Add an explosion sound file to your project
2. In the AudioSource component:
   - Drag your explosion sound to the `AudioClip` field
   - Set `Spatial Blend` to 1 (fully 3D)
   - Set `Min Distance` to the radius of your explosion
   - Set `Max Distance` to 2-3 times the radius

3. In the `Spell_02` component, assign the AudioSource to the `Explosion Sound` field

## 4. Configure Spell Properties

In the `Spell_02` component inspector:

1. Set `Explosion Radius` (default: 10)
2. Set `Explosion Damage` (default: 200)
3. Set `Damage Type` (default: Normal)
4. Set `Critical Strike Chance` (default: 0)
5. Set `Critical Strike Multiplier` (default: 2)
6. Enable `Show Radius Gizmo` to see the radius in the editor
7. Adjust `Gizmo Color` as desired

## 5. Testing in the Editor

1. Enter Play mode
2. Select the spell GameObject
3. You should see the radius gizmo
4. Manually place some enemy units within the radius
5. Check the console logs for damage application

## 6. Integration with Spell Database

1. Create a new entry in your SpellsDataBase ScriptableObject:
   ```csharp
   // In SpellsDataBase.cs or similar
   public NFTsSpell CreateExplosionSpell(int localId, int faction, int cost)
   {
       return CreateNFTsSpell(
           localId,
           faction,
           cost,
           explosionPrefab, // Reference to your saved prefab
           explosionIcon    // Reference to an icon sprite
       );
   }
   ```

## 7. Character Skills Integration

The following skills affect the Explosion Spell:

1. **ShieldMultiplier**: Increases explosion damage
2. **CriticalStrikeChance**: Adds chance for critical hits
3. **RadiusMultiplier**: Increases explosion radius

Example Character skill setup:
```csharp
// Example character with explosion-enhancing skills
public class ExplosionMaster : GameCharacter
{
    public override void DeploySpell(Spell spell)
    {
        base.DeploySpell(spell);
        
        Spell_02 explosionSpell = spell as Spell_02;
        if (explosionSpell != null)
        {
            explosionSpell.radiusMultiplier *= 1.5f; // 50% larger explosions
            explosionSpell.criticalStrikeChance += 0.2f; // 20% critical chance
            explosionSpell.damageMultiplier *= 1.3f; // 30% more damage
        }
    }
}
```

## 8. Common Issues

1. **Explosion not damaging units**:
   - Check if units have the "Unit" tag
   - Verify the colliders are set up correctly
   - Ensure units have appropriate health/shield components

2. **Visual effects not showing**:
   - Check particle system settings
   - Verify that the scale is appropriate

3. **Sound not playing**:
   - Check AudioSource settings
   - Verify that audio file is compatible 