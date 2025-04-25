# Cosmicrafts Battlegrounds - Procedural Space System

This documentation provides an overview of the procedural space generation system implemented in Cosmicrafts Battlegrounds, along with details about the texture assets used.

## Procedural Space Generation System

The procedural space generation system creates infinite, dynamic space backgrounds with nebulae, stars, and other celestial features. The system consists of two main components:

### 1. ProceduralSpaceBackground

The core class that generates the visual appearance of space. Located in `Assets/Resources/Mats/SpaceProceduralBackground.cs`.

**Key Features:**
- Generates procedural space textures with various scene types:
  - Dense Nebula
  - Star Field
  - Deep Void
  - Molecular Cloud
  - Energy Rift
- Combines noise patterns, stars, dust lanes, and galaxy overlays
- Offers customizable parameters for fine-tuning visual appearance
- Can use predefined textures or generate purely procedural patterns
- Supports random generation with seeds for reproducible results

**Generation Methods:**
- Texture-based generation using predefined assets
- Procedural generation using Perlin/Worley noise
- Combined approach with both textures and procedural elements

### 2. InfiniteSpaceGenerator

Manages a grid of space background chunks around a target transform (typically the camera). Located in `Assets/Scripts/Gameplay/Environment/InfiniteSpaceGenerator.cs`.

**Key Features:**
- Creates a scrolling grid of background chunks
- Pools and reuses chunks for performance optimization
- Positions chunks based on the target's movement
- Each chunk has unique but deterministic generation based on its position

## Texture Assets

The system utilizes various texture assets stored in the following locations:

### Main Texture Folder Structure

```
Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Textures/
├── Color Gradients/
├── Distortion Normal Maps/
├── Greyscale Gradients/
├── Noise/
├── Others/
├── Shapes/
└── Trails/

Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Textures Demo/Textures/
└── DistortionMapsCopyNoNormalMap/

Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Demo/Texutres/
```

### Texture Categories

#### UI Elements

| Filename | Description | Location |
|----------|-------------|----------|
| UiTriangle.png | Triangle UI element | Both texture folders |
| UiButton.png | Button UI element | Both texture folders |
| UiCircleSmall.png | Small circle UI element | Demo/Texutres |
| FloorTexture.png | Floor texture | Demo/Texutres |
| DemoTexturesBack.png | Background texture for demos | Textures Demo/Textures |

#### Noise Textures
Located in `Textures/Noise/` - Used for nebula and dust cloud patterns:
- PerlinNoise.png, PerlinNoise2.png, PerlinNoise3.png
- SmokeNoise.png
- Various Noise files (Noise80.png, Noise84.png, etc.)

#### Star and Shape Textures
Located in `Textures/Shapes/` - Used for star sprite rendering:
- Star1.png through Star7.png
- Spiral shapes for galaxy overlays

#### Gradients
Located in `Textures/Greyscale Gradients/` and `Textures/Color Gradients/`:
- Used for nebula coloring and patterns
- Gradient1.png through Gradient22.png
- AngleGradient.png
- Black.png and White.png for base coloring

#### Distortion Maps
Located in `Textures Demo/Textures/DistortionMapsCopyNoNormalMap/` and `Textures/Distortion Normal Maps/`:
- Used for visual distortion effects in space rendering

## Usage Guidelines

### Basic Setup

1. Add a `ProceduralSpaceBackground` component to a plane or quad mesh
2. Configure the desired scene type and parameters
3. Set `generateOnStart` to true for immediate generation

### Infinite Scrolling Setup

1. Create a chunk prefab with the `ProceduralSpaceBackground` component
2. Add an `InfiniteSpaceGenerator` component to a manager object
3. Assign the chunk prefab and a target transform (usually the camera)
4. Configure grid size and chunk world size

### Customizing Appearance

- Adjust the `sceneType` parameter for different space environments
- Fine-tune individual parameters like nebula intensity, star density, etc.
- Modify the nebula gradient for different color schemes
- For more variety, add custom textures to the appropriate folders

## Troubleshooting

If textures aren't loading properly:
1. Ensure all texture assets are imported correctly
2. Check the console for loading error messages
3. Try using the `EmergencyTextureLoadingFix()` method
4. Set `useTextureForNoise` to false for pure procedural generation
5. The system now looks for textures in the VFX Toolkit folder structure
