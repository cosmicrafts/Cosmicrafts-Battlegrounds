# AllIn1VfxToolkit Effects Documentation

This document provides a comprehensive list of all VFX effects included in the AllIn1VfxToolkit package and explains how to access them through the VFXPool system in Cosmicrafts Battlegrounds.

## Effect Categories

The AllIn1VfxToolkit organizes effects into the following categories:

### Areas and Auras
| Effect Name | Description |
|-------------|-------------|
| Red Area | Circular area effect with red particles |
| Purple Area | Circular area effect with purple particles |
| Green Area | Circular area effect with green particles |
| Blue Area + Distort Sphere | Blue area effect with distortion |
| Holy Aura | Bright golden aura for positive/healing effects |
| Evil Aura | Dark purple aura for negative/damage effects |

### Explosions and Impacts
| Effect Name | Description |
|-------------|-------------|
| Water Splash | Water splash impact effect |
| Toon Explosion | Cartoon-style explosion with bright colors |
| Fire Impact | Fire-based impact effect |
| Red Impact | Red-colored general impact |
| Purple Impact | Purple-colored general impact |
| Explosion Galaxy | Galaxy-themed explosion with stars |
| Explosion Bomb | Realistic bomb explosion with smoke |
| Blue Impact | Blue-colored general impact |
| Ice Impact | Ice-themed impact with frost particles |
| Gun Impact | Bullet impact effect for firearms |
| Digital Proj Impact | Digital/tech-themed impact |

### Shields
| Effect Name | Description |
|-------------|-------------|
| Water Shield | Bubble shield with water effects |
| SciFi Shield | High-tech energy shield |
| SciFi Shield 2 | Alternative sci-fi shield design |
| Sand Shield | Shield made of swirling sand particles |
| Fire Shield | Shield surrounded by fire effects |

### Projectiles
| Effect Name | Description |
|-------------|-------------|
| Ice Projectile | Ice-themed projectile with trailing frost |
| Gun Bullet | Standard bullet projectile |
| Fire Bullet | Fire-themed projectile |
| Digital Projectile | Digital/tech-themed projectile |

### Muzzle Flashes
| Effect Name | Description |
|-------------|-------------|
| Ice Muzzle Flash | Ice-themed muzzle flash for cold weapons |
| Gun Muzzle Flash | Standard gun muzzle flash |
| Fire Muzzle Flash | Fire-themed muzzle flash |
| Digital Muzzle Flash | Digital/tech-themed muzzle flash |

### Static Effects (Group 1)
| Effect Name | Description |
|-------------|-------------|
| Toon Character | Cartoon-styled character effect |
| Thick Smoke | Dense smoke cloud effect |
| Real Fire | Realistic fire effect |
| Pixel Fire | Pixelated fire effect |
| Pink Trail | Pink trailing effect |
| Ghost Trail | Ghostly transparent trail |
| Screenspace Galaxy Character | Character with galaxy overlay |
| Fire Trail | Fire trailing effect |
| Electricity | Electric sparks and arcs |
| Dark Magic Orb | Dark magic orb with particles |
| Blue Fire | Blue-colored fire effect |

### Static Effects (Group 2)
| Effect Name | Description |
|-------------|-------------|
| Plasma Ball | Energy ball with plasma effects |
| Magic Spiral | Spiral magic effect |
| Green Portal | Portal effect with green particles |
| Fire Tornado | Tornado made of fire |
| Blue Tornado | Tornado with blue particles |
| Blue Pixel Portal | Pixelated portal with blue theme |
| Air Column | Column of swirling air |

### Playable Effects - Slashes
| Effect Name | Description |
|-------------|-------------|
| Slash Venom | Venom/poison themed slash effect |
| Slash Orange | Orange-colored slash |
| Slash Magic | Magical slash with sparkles |
| Slash Blue | Blue-colored slash |

### Playable Effects - Beams and Others
| Effect Name | Description |
|-------------|-------------|
| Toon Beam Orange | Cartoon-styled orange beam |
| Beam Blue | Blue energy beam |
| Lightning Strike | Lightning bolt strike |
| Incinerate Spell | Fire-based incineration spell |
| Magic Explosive Spell | Explosive magic spell |

## Accessing Effects via VFXPool

The `VFXPool` class provides a convenient way to access and instantiate these effects in your game. The class uses object pooling to optimize performance when spawning multiple effects.

### Basic Usage

```csharp
// Get a reference to the VFXPool
VFXPool vfxPool = VFXPool.Instance;

// Play a specific effect from a category
GameObject effect = vfxPool.PlayEffect("Explosions", 0, transform.position);

// Play a random effect from a category
GameObject randomEffect = vfxPool.PlayRandomEffect("Shields", transform.position);

// Use convenience methods for common effects
GameObject impactEffect = vfxPool.PlayImpact(hitPosition);
GameObject explosionEffect = vfxPool.PlayExplosion(explosionPosition, 2.0f); // With scale
GameObject shieldEffect = vfxPool.PlayShieldEffect(playerPosition);
GameObject muzzleEffect = vfxPool.PlayMuzzleFlash(weaponMuzzleTransform);
```

### Importing Effects

The VFXPool includes a helper method to import effects directly from the AllIn1VfxDemoController. In the Unity Editor:

1. Add the AllIn1VfxDemoController to your scene temporarily
2. Select your VFXPool GameObject in the Inspector
3. Right-click on the VFXPool component and select "Import From AllIn1VfxDemoController"
4. The effects will be automatically organized into categories

## Effect Lifetime and Customization

By default, effects have a lifetime specified in the EffectCategory they belong to. This determines how long they remain active before returning to the pool. You can modify this value in the Inspector.

Each effect can be scaled during instantiation by providing a scale parameter to the Play methods. 