# Modern Mesh Outline System for Unity

## Overview

This document outlines a modern, performant approach to implementing outlines for meshes in Unity, with a particular focus on SkinnedMeshRenderer components. This system is designed as a replacement for deprecated outline plugins like EPOOutline, optimized for Unity 6 and modern rendering pipelines.

## Outline Techniques

There are several approaches to creating outlines in Unity:

### 1. Silhouette-Based Outlines

**Concept**: Render the mesh twice - once normally, and once slightly larger with an outline shader.

**Implementation**:
- Create a duplicate mesh that scales slightly larger than the original
- Apply an outline material that renders only the silhouette
- Ensure the outline renders behind the main mesh

**Pros**:
- Works well with complex meshes
- Can handle animation and deformation of SkinnedMeshRenderer
- Relatively easy to implement

**Cons**:
- Can be expensive for high-poly meshes
- May have Z-fighting issues

### 2. Post-Processing Outlines

**Concept**: Apply outlines as a post-processing effect by detecting edges in the screen-space.

**Implementation**:
- Create a custom render feature/pass that detects edges based on normal and depth discontinuities
- Apply edge detection algorithm (e.g., Sobel filter) to find silhouettes
- Draw outlines around detected edges

**Pros**:
- Can be very performant
- Works universally on all visible objects
- Modern approach that works well with URP/HDRP

**Cons**:
- Less control over specific objects
- Can outline unintended parts of the scene
- May require custom render pipeline integration

### 3. Stencil Buffer Approach

**Concept**: Use the stencil buffer to mark areas for outlining.

**Implementation**:
- Render the object normally
- Use stencil buffer to mark the object's pixels
- Render a slightly larger version of the mesh with a stencil test
- Draw only the pixels that pass the stencil test (the outline)

**Pros**:
- Very precise control over outlines
- Good performance
- Works well with modern render pipelines

**Cons**:
- More complex to implement
- Requires understanding of stencil buffer operations

## Current Implementation Analysis

### Scripts Using EPOOutline Plugin

The following scripts in the existing codebase currently utilize the EPOOutline plugin:

#### 1. Unit.cs
```csharp
// Current EPOOutline implementation
using EPOOutline;

public class Unit : MonoBehaviour
{
    // ... other code ...
    
    protected Outlinable MyOutline;
    
    protected virtual void Start()
    {
        // ... other initialization ...
        MyOutline = Mesh.GetComponent<Outlinable>();
        // ... additional setup ...
        MyOutline.OutlineParameters.Color = GameMng.GM.GetColorUnit(MyTeam, PlayerId);
    }
    
    // Recently added method to update outline color
    public void UpdateOutlineColor()
    {
        if (MyOutline != null && GameMng.GM != null)
        {
            Color outlineColor = GameMng.GM.GetColorUnit(MyTeam, PlayerId);
            MyOutline.OutlineParameters.Color = outlineColor;
        }
    }
}
```

#### 2. GameMng.cs
```csharp
// Color determination method for outlines
public Color GetColorUnit(Team team, int playerId)
{
    // Check if this is a player-controlled unit first
    if (P != null && team == P.MyTeam && playerId == P.ID)
    {
        // Return a distinct color for player's own units (green)
        return Color.green;
    }
    
    // Otherwise use team colors for enemies and allies not controlled by player
    return team == Team.Blue ? Color.blue : Color.red;
}
```

#### 3. DragUnitCtrl.cs
```csharp
// Outline usage for unit placement preview
using EPOOutline;

public class DragUnitCtrl : MonoBehaviour
{
    // ... other code ...
    public Outlinable Outline;
    
    // Update the outline color when dragging units
    public void SetMeshAndTexture(Mesh mesh, Material mat)
    {
        // ... other code ...
        Outline.OutlineParameters.Color = color;
    }
}
```

### Integration Requirements

To replace the existing EPOOutline implementation, we'll need to:

1. **Add Core Components**:
   - Create the `OutlineManager` as a singleton
   - Implement `OutlineRenderer` for mesh duplication
   - Create a new `Outlinable` component to replace EPOOutline.Outlinable

2. **Create the Outline Shader**:
   - Implement the outlined shader in your preferred render pipeline (built-in or URP)
   - Ensure it works with SkinnedMeshRenderer components

3. **Update Existing References**:
   - Modify `Unit.cs` to use the new outlining system
   - Update `DragUnitCtrl.cs` to utilize the new approach
   - Keep `GameMng.GetColorUnit()` as it's still useful for color determination

## Recommended Approach

For a modern Unity 6 implementation, we recommend a hybrid approach using **shader graph** (for URP/HDRP) or **stencil buffer** (for built-in pipeline) combined with object pooling for efficiency.

## Implementation Guide

### 1. Core Components

#### OutlineManager
Centralizes outline control and handles renderer pooling.

```csharp
using System.Collections.Generic;
using UnityEngine;

public class OutlineManager : MonoBehaviour
{
    private static OutlineManager instance;
    public static OutlineManager Instance => instance;

    [Header("Outline Settings")]
    public Material outlineMaterial;
    public float outlineWidth = 1.05f;
    
    // Color presets
    public Color playerTeamColor = Color.green;
    public Color allyTeamColor = Color.blue;
    public Color enemyTeamColor = Color.red;
    
    // Pool of outline renderers
    private Dictionary<Renderer, OutlineRenderer> activeOutlines = new Dictionary<Renderer, OutlineRenderer>();
    private Queue<OutlineRenderer> outlinePool = new Queue<OutlineRenderer>();
    
    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);
    }
    
    public void AddOutline(Renderer renderer, Color color)
    {
        if (activeOutlines.ContainsKey(renderer))
        {
            // Update existing outline
            activeOutlines[renderer].SetColor(color);
            return;
        }
        
        // Get or create outline renderer
        OutlineRenderer outlineRenderer = GetOutlineRenderer();
        outlineRenderer.SetTarget(renderer, outlineWidth, color);
        activeOutlines.Add(renderer, outlineRenderer);
    }
    
    public void RemoveOutline(Renderer renderer)
    {
        if (activeOutlines.TryGetValue(renderer, out OutlineRenderer outlineRenderer))
        {
            // Return to pool
            outlineRenderer.ClearTarget();
            outlinePool.Enqueue(outlineRenderer);
            activeOutlines.Remove(renderer);
        }
    }
    
    private OutlineRenderer GetOutlineRenderer()
    {
        if (outlinePool.Count > 0)
            return outlinePool.Dequeue();
            
        // Create new outline renderer
        GameObject obj = new GameObject("OutlineRenderer");
        obj.transform.SetParent(transform);
        OutlineRenderer renderer = obj.AddComponent<OutlineRenderer>();
        renderer.Initialize(outlineMaterial);
        return renderer;
    }
    
    // Helper method to get color based on team
    public Color GetTeamColor(int team, int playerId, int playerTeamId)
    {
        if (team == playerTeamId && playerId == 1)
            return playerTeamColor;
        else if (team == playerTeamId)
            return allyTeamColor;
        else
            return enemyTeamColor;
    }
}
```

#### OutlineRenderer
Handles the actual mesh rendering with outline effect.

```csharp
using UnityEngine;

public class OutlineRenderer : MonoBehaviour
{
    private Material outlineMaterial;
    private Renderer targetRenderer;
    private Mesh meshCopy;
    private MeshFilter meshFilter;
    private SkinnedMeshRenderer skinnedMeshRenderer;
    private MeshRenderer meshRenderer;
    
    public void Initialize(Material material)
    {
        outlineMaterial = new Material(material);
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.material = outlineMaterial;
        meshRenderer.enabled = false;
    }
    
    public void SetTarget(Renderer renderer, float width, Color color)
    {
        targetRenderer = renderer;
        
        // Configure the outline material
        outlineMaterial.SetColor("_OutlineColor", color);
        outlineMaterial.SetFloat("_OutlineWidth", width);
        
        // Copy the mesh
        if (targetRenderer is SkinnedMeshRenderer)
        {
            skinnedMeshRenderer = targetRenderer as SkinnedMeshRenderer;
            meshCopy = new Mesh();
            skinnedMeshRenderer.BakeMesh(meshCopy);
            meshFilter.mesh = meshCopy;
        }
        else if (targetRenderer is MeshRenderer)
        {
            MeshFilter targetMeshFilter = targetRenderer.GetComponent<MeshFilter>();
            if (targetMeshFilter != null && targetMeshFilter.sharedMesh != null)
            {
                meshFilter.mesh = targetMeshFilter.sharedMesh;
            }
        }
        
        // Enable rendering and update transform
        UpdateTransform();
        meshRenderer.enabled = true;
    }
    
    public void ClearTarget()
    {
        meshRenderer.enabled = false;
        targetRenderer = null;
        if (meshCopy != null)
        {
            Destroy(meshCopy);
            meshCopy = null;
        }
    }
    
    public void SetColor(Color color)
    {
        outlineMaterial.SetColor("_OutlineColor", color);
    }
    
    private void LateUpdate()
    {
        if (targetRenderer == null || !targetRenderer.gameObject.activeInHierarchy)
        {
            meshRenderer.enabled = false;
            return;
        }
        
        // Update transform to match target
        UpdateTransform();
        
        // For skinned mesh, update the mesh every frame to match animations
        if (skinnedMeshRenderer != null && meshCopy != null)
        {
            skinnedMeshRenderer.BakeMesh(meshCopy);
            meshFilter.mesh = meshCopy;
        }
    }
    
    private void UpdateTransform()
    {
        transform.position = targetRenderer.transform.position;
        transform.rotation = targetRenderer.transform.rotation;
        transform.localScale = targetRenderer.transform.lossyScale;
    }
}
```

#### Outlinable
Component to add to objects that need outlines.

```csharp
using UnityEngine;

public class Outlinable : MonoBehaviour
{
    [Header("Outline Settings")]
    public bool outlineEnabled = true;
    public Color outlineColor = Color.yellow;
    
    [Header("Team Settings")]
    public int teamId = 0;  // 0 = blue, 1 = red
    public int playerId = 1;
    
    private Renderer[] targetRenderers;
    
    private void Start()
    {
        // Find all renderers to outline
        targetRenderers = GetComponentsInChildren<Renderer>();
        UpdateOutline();
    }
    
    private void OnEnable()
    {
        UpdateOutline();
    }
    
    private void OnDisable()
    {
        if (OutlineManager.Instance != null)
        {
            foreach (Renderer renderer in targetRenderers)
            {
                if (renderer != null)
                    OutlineManager.Instance.RemoveOutline(renderer);
            }
        }
    }
    
    public void SetTeam(int team, int player)
    {
        teamId = team;
        playerId = player;
        UpdateOutline();
    }
    
    public void UpdateOutline()
    {
        if (!outlineEnabled || OutlineManager.Instance == null)
            return;
            
        foreach (Renderer renderer in targetRenderers)
        {
            if (renderer != null)
            {
                // Use the OutlineManager to determine color based on team
                Color color = OutlineManager.Instance.GetTeamColor(teamId, playerId, 0); // Assuming 0 is player team
                OutlineManager.Instance.AddOutline(renderer, color);
            }
        }
    }
    
    private void OnDestroy()
    {
        if (OutlineManager.Instance != null)
        {
            foreach (Renderer renderer in targetRenderers)
            {
                if (renderer != null)
                    OutlineManager.Instance.RemoveOutline(renderer);
            }
        }
    }
}
```

### 2. Shader Implementation

#### Outline Shader for URP (Shader Graph)

Create a shader graph with the following structure:

1. **Inputs**:
   - Base Color
   - Outline Color
   - Outline Width

2. **Node Structure**:
   - Calculate vertex normal in object space
   - Multiply normal by Outline Width
   - Add to vertex position
   - Convert to clip space
   - Apply outline color

3. **Settings**:
   - Rendering: Transparent
   - Culling: Front (to prevent Z-fighting with the original mesh)
   - ZTest: Less Equal (or customize based on needs)

#### Outline Shader for Built-In Pipeline

```
Shader "Custom/Outline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineWidth ("Outline Width", Range(1.0, 1.1)) = 1.05
    }
    
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        
        Pass
        {
            Name "OUTLINE"
            
            Cull Front
            ZWrite On
            ZTest Less
            Blend SrcAlpha OneMinusSrcAlpha
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
            };
            
            float _OutlineWidth;
            fixed4 _OutlineColor;
            
            v2f vert(appdata v)
            {
                v2f o;
                v.vertex.xyz *= _OutlineWidth;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }
    }
}
```

## Migration Guide: From EPOOutline to New System

### 1. Unit.cs Migration

```csharp
// Changes needed in Unit.cs
// FROM:
using EPOOutline;
// TO:
// Remove EPOOutline using statement

public class Unit : MonoBehaviour
{
    // FROM:
    protected Outlinable MyOutline;
    
    // TO:
    protected Cosmicrafts.Outlinable myOutlinable;
    
    protected virtual void Start()
    {
        // Other initialization...
        
        // FROM:
        MyOutline = Mesh.GetComponent<Outlinable>();
        // ... other code ...
        MyOutline.OutlineParameters.Color = GameMng.GM.GetColorUnit(MyTeam, PlayerId);
        
        // TO:
        myOutlinable = Mesh.GetComponent<Cosmicrafts.Outlinable>();
        if (myOutlinable == null)
        {
            myOutlinable = Mesh.AddComponent<Cosmicrafts.Outlinable>();
        }
        myOutlinable.SetTeam((int)MyTeam, PlayerId);
        
        // ...Other initialization remains the same
    }
    
    // Replace UpdateOutlineColor method
    public void UpdateOutlineColor()
    {
        // FROM:
        if (MyOutline != null && GameMng.GM != null)
        {
            Color outlineColor = GameMng.GM.GetColorUnit(MyTeam, PlayerId);
            MyOutline.OutlineParameters.Color = outlineColor;
        }
        
        // TO:
        if (myOutlinable != null)
        {
            myOutlinable.SetTeam((int)MyTeam, PlayerId);
            myOutlinable.UpdateOutline();
        }
    }
}
```

### 2. DragUnitCtrl.cs Migration

```csharp
// Changes needed in DragUnitCtrl.cs
// FROM:
using EPOOutline;
// TO:
// Remove EPOOutline using statement

public class DragUnitCtrl : MonoBehaviour
{
    // FROM:
    public Outlinable Outline;
    
    // TO:
    private Renderer previewRenderer;
    
    private void Start()
    {
        // Initialize
        previewRenderer = GetComponentInChildren<Renderer>();
    }
    
    // Update the SetMeshAndTexture method
    public void SetMeshAndTexture(Mesh mesh, Material mat)
    {
        // FROM:
        // ...existing code...
        Outline.OutlineParameters.Color = color;
        
        // TO:
        // ...existing code...
        if (previewRenderer != null && OutlineManager.Instance != null)
        {
            OutlineManager.Instance.AddOutline(previewRenderer, color);
        }
    }
    
    // Add cleanup on destroy
    private void OnDestroy()
    {
        if (previewRenderer != null && OutlineManager.Instance != null)
        {
            OutlineManager.Instance.RemoveOutline(previewRenderer);
        }
    }
}
```

### 3. GameManager Integration

Add an OutlineManager to your GameManager scene:

```csharp
// In GameMng.Awake() or similar initialization method
if (FindObjectOfType<OutlineManager>() == null)
{
    GameObject outlineManagerObj = new GameObject("OutlineManager");
    OutlineManager outlineManager = outlineManagerObj.AddComponent<OutlineManager>();
    
    // Configure the manager
    outlineManager.outlineMaterial = Resources.Load<Material>("Materials/OutlineMaterial");
    outlineManager.playerTeamColor = Color.green;
    outlineManager.allyTeamColor = Color.blue;
    outlineManager.enemyTeamColor = Color.red;
}
```

## Performance Optimizations

1. **Mesh Pooling**: Reuse mesh instances instead of creating new ones for each outline.

2. **LOD-Based Outlines**: Use simpler outline meshes for distant objects.

3. **Culling**: Only render outlines for objects in the camera view.

4. **Batching**: Use GPU instancing for similar outlines.

5. **Texture-Based Outlines**: For very complex meshes, consider pre-baking outlines to textures.

## Best Practices

1. **Initialization**: Set up the OutlineManager as a singleton in your scene.

2. **Layer Management**: Use layers to control which objects can be outlined.

3. **Render Queues**: Ensure proper render queue settings to prevent Z-fighting.

4. **Material Property Blocks**: Use MaterialPropertyBlocks for efficient property updates.

5. **Shader Variants**: Minimize shader variants to reduce build size and compile time.

## Shader Optimization Tips

1. **Early Outs**: Use shader LOD or early exits for pixels that don't need outline processing.

2. **Tessellation Alternatives**: Consider using normal extrusion instead of tessellation for better performance.

3. **Mobile Considerations**: Use simpler algorithms on mobile platforms.

4. **SIMD Instructions**: Structure shader code to take advantage of SIMD processing.

## Debugging

1. **Visual Debugging**: Add debug visualization modes to show outline coverage.

2. **Performance Monitoring**: Use the Frame Debugger to profile outline rendering costs.

3. **Mesh Analysis**: Visualize polygon count and complexity to identify optimization targets.

## Conclusion

This modern outline system provides a performant, flexible replacement for the deprecated EPOOutline plugin. By leveraging object pooling, efficient shader techniques, and careful resource management, it achieves high-quality outlines with minimal performance impact.

The modular design allows for easy integration with existing systems and can be extended with additional features such as outline pulsing, color transitions, or selective parts outlining as needed. 