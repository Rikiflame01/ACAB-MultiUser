# Transparent Canvas Setup Guide

## Overview
The NetworkCanvas has been modified to support transparent painting, creating the illusion that users are painting directly on objects behind the canvas.

## Key Features
- ✅ Transparent canvas background
- ✅ Visible paint strokes
- ✅ Works in both offline and multiplayer modes
- ✅ Clear canvas functionality
- ✅ Multiple color testing

## Setup Instructions

### 1. Material Setup
The NetworkCanvas will automatically configure any assigned material for transparency, but for best results:

1. Create a new Material in Unity
2. Set the Shader to "Standard" 
3. Set Rendering Mode to "Transparent"
4. Assign this material to the `canvasMaterial` field in NetworkCanvas component

**Alternative:** Use the `TransparentCanvasMaterialSetup` script to automatically configure existing materials.

### 2. NetworkCanvas Component
- The canvas will automatically initialize with transparent pixels
- Paint strokes will be fully opaque and visible
- The canvas background remains transparent

### 3. Testing with OfflinePaintTest
Add the `OfflinePaintTest` component to any GameObject to test the functionality:

**Controls:**
- **Space**: Paint with default color at random coordinates
- **1**: Paint with red
- **2**: Paint with blue  
- **3**: Paint with green
- **4**: Paint with yellow
- **C**: Clear the entire canvas

### 4. Integration with Existing Code
Replace any direct calls to `ApplyPaintLocally()` or `PaintServerRpc()` with the new unified `Paint()` method:

```csharp
// Old way (multiplayer only):
networkCanvas.ApplyPaintLocally(uv, color, brushSize);
networkCanvas.PaintServerRpc(uv, color, brushSize, clientId);

// New way (works in both offline and online modes):
networkCanvas.Paint(uv, color, brushSize);
```

## Technical Details

### Transparency Implementation
- Canvas initializes with `Color.clear` (transparent) pixels
- Material is automatically configured for alpha blending
- Paint strokes use full alpha (opaque colors)
- PNG encoding preserves transparency for network sync

### Network Synchronization
- Transparency is maintained across all clients
- Clear canvas functionality syncs across network
- Original multiplayer painting functionality preserved

## Troubleshooting

### Canvas appears solid/opaque
- Check that the material has Rendering Mode set to "Transparent"
- Verify the material's Render Queue is set to "Transparent" (3000)
- Use the `TransparentCanvasMaterialSetup` script to auto-configure

### Paint strokes not visible
- Ensure paint colors have alpha = 1.0 (fully opaque)
- Check that the canvas texture is properly assigned to the material
- Verify the mesh renderer is using the correct material instance

### Network sync issues
- Transparent canvas sync works the same as before
- Clear canvas function includes network synchronization
- PNG encoding preserves alpha channel for network transmission
