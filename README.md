# Spatial Reconstruction System

## What Is This?
A Unity tool that captures your 3D scene as training data for AI/ML models. Each capture generates:
- **JSON file** with 6DoF poses of all objects (position, rotation, scale)
- **PNG depth map** showing distance from camera
- **Camera pose data** with projection matrices for 3D reconstruction

**Available as Unity Package**: This entire folder can be imported into any Unity project as a `.unitypackage`.

---

## Installation & Setup

### Step 1: Import the System
1. Copy the entire `_Roam Systems` folder into your Unity project's `Assets/` directory
2. Unity will automatically import all scripts and assets

### Step 2: Add URP Renderer Feature (REQUIRED)
The depth capture requires a special renderer feature. Here's how to add it:

#### Method A: Automatic (Recommended)
1. Open Unity menu: **Roam > Spatial Reconstruction > Frame Analyzer**
2. Look for the "URP Renderer Pipeline" section
3. If the button is **RED**, click "Add to Renderer Pipeline"
4. Follow the dialog instructions

#### Method B: Manual Setup
1. Go to **Project Settings > Graphics**
2. Find your **URP Renderer** asset (usually called "UniversalRenderer" or similar)
3. Click on it to open in Inspector
4. Click **"Add Renderer Feature"**
5. Select **"Depth Texture"** from the dropdown
6. The feature should now appear in the list - make sure it's enabled

### Step 3: Assign Volume Profile
1. In the Frame Analyzer window, look for "Depth Visualization" section
2. If the field is empty, assign the **"Depth Volume Profile.asset"** from:
   ```
   Assets/_Roam Systems/Spatial Reconstruction/Depth Effect/Depth Volume Profile.asset
   ```
3. This profile contains the precise depth rendering settings optimized for data capture

### Step 4: Test the Setup
1. Click the big blue **"CAPTURE FRAME"** button
2. Check if files are created in the output folder
3. If the depth PNG is all black, revisit Steps 2 & 3

---

## Quick Start Guide

### 1. Open the Tool
In Unity menu: **Roam > Spatial Reconstruction > Frame Analyzer**

### 2. Single Frame Capture
Click the big blue **CAPTURE FRAME** button. Done!

### 3. Batch Mode (Multiple Frames)
1. Toggle **Batch Processing** on
2. Set **Interval** (0.1s to 20s)
3. Click **Start Batch Process**
4. Let it run while you play/test your scene
5. Click **Stop** when done

---

## What Data You Get

### Output Files
Every capture creates 2 files in `Assets/_Roam Systems/Spatial Reconstruction/Captures/`:

**1. JSON File** - Complete scene data
```
frame_20251006_143022.json
```

**2. Depth PNG** - Visual depth map
```
depth_20251006_143022.png
```

### JSON Structure Explained

```json
{
  "timestamp": "2025-10-06 14:30:22",
  "objectCount": 42,
  
  "camera": {
    "position": [0, 5, -10],           // Camera XYZ in world space
    "rotation": [0.3, 0, 0, 0.95],     // Quaternion (x,y,z,w)
    "eulerAngles": [30, 0, 0],         // Rotation in degrees
    "fov": 60,                         // Field of view
    "nearClip": 0.3,
    "farClip": 1000,
    "aspectRatio": 1.778,
    "pixelWidth": 1920,
    "pixelHeight": 1080,
    "projectionMatrix": {...},        // 4x4 camera projection matrix
    "worldToCameraMatrix": {...}      // 4x4 world-to-camera transform
  },
  
  "objects": [
    {
      "name": "Player",
      "path": "GameRoot/Player",       // Full hierarchy path
      "position": [0, 0, 0],           // World space XYZ
      "rotation": [0, 0, 0, 1],        // Quaternion (x,y,z,w)
      "scale": [1, 1, 1],              // Local scale
      "isVisible": true,               // In camera frustum?
      "layer": "Default",
      "tag": "Player"
    },
    // ... all other objects in scene
  ],
  
  "screenshotPath": "Assets/.../depth_20251006_143022.png"
}
```

### Depth Map (PNG)
- Grayscale 8-bit image where brightness = distance
- **White (255)** = nearest objects (0.01m default)
- **Black (0)** = farthest objects (80m default)
- Resolution matches your capture settings

**Visual Mapping:**
```
Scene Depth        Pixel Value    Setting
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
0.01m (closest) →  255 (white) ← Near Distance
40m   (middle)  →  128 (gray)
80m   (farthest)→  0   (black) ← Far Distance
```
These settings are in: `Depth Volume Profile.asset` → "Depth Texture" component

**Quick Reference - Depth Conversion:**
```
real_depth_meters = (pixel_value / 255.0) * 79.99 + 0.01
```

**Full Depth Conversion Formula:**
```
real_depth_meters = (pixel_value / 255.0) * (farDistance - nearDistance) + nearDistance
```

**Default Parameters:**
These are set in `Depth Volume Profile.asset` under the **"Depth Texture"** volume component:
- `Near Distance` = 0.01m (objects closer appear white/255)
- `Far Distance` = 80m (objects farther appear black/0)

**How to Change:**
1. In Unity, select `Assets/_Roam Systems/Spatial Reconstruction/Depth Effect/Depth Volume Profile.asset`
2. In the Inspector, find the **"Depth Texture"** section
3. Adjust `Near Distance` and `Far Distance` sliders
4. These values directly control the depth mapping

**Example Conversion** (with defaults):
```
pixel_value = 255 → real_depth ≈ 0.01m (near/white)
pixel_value = 128 → real_depth ≈ 40m (middle gray)
pixel_value = 0   → real_depth ≈ 80m (far/black)
```

**Note:** The `Depth Curve` parameter in the same volume component can modify this linear mapping. Default curve is linear (0→0, 1→1)


## Common Use Cases

- **3D Object Detection**: Train models with labeled 3D bounding boxes
- **Depth Estimation**: Ground truth depth for training depth prediction networks
- **Pose Estimation**: Object poses for 6DoF tracking models
- **Scene Understanding**: Semantic scene graphs with spatial relationships
- **3D Reconstruction**: Depth + camera pose pairs

---

## Usage Tips

### Single Frame vs Batch
- **Single**: Manual control, capture specific moments (e.g., right before collision)
- **Batch**: Automated dataset collection (e.g., capture every 0.5s during gameplay)

### When to Capture
- **Play Mode**: Capture during gameplay/interactions
- **Edit Mode**: Capture static scene arrangements

### Data Organization
Each capture has matching filenames:
```
frame_20251006_143022.json  ← Scene data
depth_20251006_143022.png   ← Matching depth map
```
Parse JSON, load PNG using same timestamp to pair them.

### Performance
- GPU-accelerated depth rendering (fast)
- Captures in background (non-intrusive)
- Batch mode auto-cleans up between captures

---

## Configuration Options

Click the **Configuration** section in Frame Analyzer to customize:

**Output Settings**
- `Output Folder Path`: Where to save captures
- `Resolution Multiplier`: 1x = screen resolution, 2x = 2x sharper depth maps

**Filtering**
- `Layer Mask`: Only capture objects on specific layers
- `Include Inactive Objects`: Capture hidden/disabled objects
- `Frustum Culling`: Only capture visible objects (faster)

**Depth Visualization** (Advanced)
To customize depth range:
1. Select `Assets/_Roam Systems/Spatial Reconstruction/Depth Effect/Depth Volume Profile.asset`
2. In Inspector, expand the **"Depth Texture"** volume component
3. Modify these parameters:
   - `Near Distance`: Closest depth in meters (default: 0.01m) - objects closer appear white
   - `Far Distance`: Farthest depth in meters (default: 80m) - objects farther appear black
   - `Depth Curve`: Custom falloff curve (default: linear 0→1)
   - `Shadow Influence`: How much shadows affect depth visualization (default: 0.215)

**Location:** These are post-processing volume settings, NOT camera settings. The `Depth Texture` component inside the volume profile controls all depth mapping parameters.

---

## Technical Details

**Coordinate System**: Unity left-handed (Y-up)
- Position: World space coordinates
- Rotation: Quaternion (x, y, z, w)
- Matrices: Row-major order

**Depth Rendering**: URP-based shader with post-processing
- Shader: `Roam/DepthTexture` (HLSL)
- Captures scene depth from GPU depth buffer
- Converts to linear eye space depth using `LinearEyeDepth()`
- Remaps to custom near/far range: `(eyeDepth - nearDistance) / (farDistance - nearDistance)`
- Applies custom curve LUT for non-linear falloff
- Blends with shadow information for better visualization
- GPU-accelerated, non-destructive (doesn't affect main camera)

**File Format**: JSON + PNG
- JSON: UTF-8 encoded, human-readable
- PNG: 8-bit grayscale, depth encoded as linear mapping from near to far distance

---

## Package Installation

To use in another Unity project:
1. Import the `.unitypackage` file OR copy the `_Roam Systems` folder
2. Ensure URP is installed in your project (Package Manager > URP)
3. Complete the Installation & Setup steps above
4. Open **Roam > Spatial Reconstruction > Frame Analyzer**

---

## Troubleshooting

### Common Issues

**🔴 "Red button showing in Frame Analyzer"**
- **Problem**: URP Renderer Feature not added
- **Solution**: Click the red button OR follow Step 2 in Installation & Setup

**⚫ "Depth PNG is all black"**
- **Problem**: Volume Profile not assigned or renderer feature missing
- **Solution**: 
  1. Assign the included `Depth Volume Profile.asset` (Step 3 above)
  2. Ensure renderer feature is added (Step 2 above)
  3. Make sure your project uses URP, not Built-in Render Pipeline

**📁 "No files generated"**
- **Problem**: Output folder doesn't exist or no write permissions
- **Solution**: Check Configuration settings, ensure output folder path is valid

**🔍 "Objects missing from JSON"**
- **Problem**: Layer filtering or frustum culling is too restrictive
- **Solution**: Check Layer Mask and "Only Visible Objects" settings in Configuration

**📏 "Depth values seem wrong"**
- **Problem**: Near/Far distance settings don't match your scene scale
- **Solution**: 
  1. Open `Depth Volume Profile.asset`
  2. Adjust `Near Distance` and `Far Distance` in the "Depth Texture" component
  3. Rule of thumb: Set Far Distance to match the farthest visible object in your scene
  4. Recapture after changing these values

**⚠️ "Script compilation errors"**
- **Problem**: Missing URP package or wrong Unity version
- **Solution**: Install URP via Package Manager, use Unity 2022.3 LTS or newer

### For Non-Unity Users

**"I don't know what URP is"**
- URP = Universal Render Pipeline, Unity's modern rendering system
- When creating a new Unity project, select "3D (URP)" template
- If you have an existing project, install URP via Package Manager

**"Where is the Package Manager?"**
- Unity top menu: **Window > Package Manager**
- Search for "Universal RP" and click Install

**"I can't find the Frame Analyzer window"**
- Unity top menu: **Roam > Spatial Reconstruction > Frame Analyzer**
- If "Roam" menu is missing, the scripts didn't compile properly

---

## File Locations & Assets

### Key Files in This Package
```
_Roam Systems/Spatial Reconstruction/
├── Depth Effect/
│   ├── Depth Volume Profile.asset     ← ASSIGN THIS in Frame Analyzer
│   ├── DepthTexture.cs               ← URP Renderer Feature script
│   ├── DepthTexture.shader           ← Custom depth shader
│   └── DepthTextureSettings.cs       ← Volume component
├── Editor/
│   ├── SpatialReconstructionWindow.cs ← Main UI window
│   └── DepthCaptureAdvanced.cs       ← Advanced capture techniques
├── Scripts/
│   ├── FrameData.cs                  ← Data structures for JSON output
│   └── SpatialReconstructionConfig.cs ← Configuration asset
└── README.md                         ← This file
```

### Generated Output Files
Default location: `Assets/_Roam Systems/Spatial Reconstruction/Output/`
- `frame_YYYYMMDD_HHMMSS.json` - Scene data
- `depth_YYYYMMDD_HHMMSS.png` - Depth visualization

---

## Support & Contact
For issues or questions:
1. Check the configuration asset: `SpatialReconstructionConfig.asset` 
2. Verify all setup steps above
3. Ensure you're using URP (not Built-in Render Pipeline)
4. Check Unity Console for error messages
