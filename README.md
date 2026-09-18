# Stratum

[![Unity Version](https://img.shields.io/badge/Unity-6000.4.4f1%20(Unity%206)-blue.svg?logo=unity)](https://unity.com/)
[![Render Pipeline](https://img.shields.io/badge/Render%20Pipeline-Universal%20RP%2017.4-informational.svg)](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.4/)
[![XR Platform](https://img.shields.io/badge/XR-Meta%20Quest%20%7C%20OpenXR-8A2BE2.svg?logo=oculus)](https://developer.oculus.com/)
[![Interaction SDK](https://img.shields.io/badge/Meta%20XR%20SDK-v205.0.0-blueviolet.svg)](https://developer.oculus.com/documentation/unity/unity-isdk-overview/)
[![License: GPL-3.0](https://img.shields.io/badge/License-GPL--3.0-blue.svg)](LICENSE)

**Stratum** is an immersive Virtual Reality geological excavation and mineral exploration simulation developed for **Meta Quest** headsets using **Unity 6 (URP)**.

> Developed as part of the **Augmented Reality & Metaverse** course @ University of Calabria (Academic Year 2025/2026).

Stationed inside a subterranean research laboratory outpost, players wield a physics-driven pickaxe to carve through dynamically deformable volumetric terrain. Uncover 17 authentic geological mineral specimens buried in procedural veins, physically extract them as they are exposed to the cavern air, and catalog your discoveries in an interactive holographic laboratory terminal.

<video src="stratum.mp4" controls width="100%"></video>

---

## Key Features

- **GPU Marching Cubes Volumetric Terrain**: Real-time compute shader ([MarchingCubes3D.compute](Assets/Scripts/ProceduralMeshGeneration/MarchingCubes3D.compute)) enabling 360° carving, tunneling, and synchronous concave collider generation managed by [TerrainManager.cs](Assets/Scripts/Managers/TerrainManager.cs) and [TerrainChunk.cs](Assets/Scripts/ProceduralMeshGeneration/TerrainChunk.cs).
- **Procedural Strata Triplanar Shader**: Custom URP shader ([StratifiedTerrainTriplanar.shader](Assets/Resources/Materials/Terrain/StratifiedTerrainTriplanar.shader)) featuring domain-warped sediment bands (silt, sandstone, shale, limestone, and deep bedrock) with world-space triplanar mapping.
- **17 Authentic Geological Minerals**: Data-driven specimens via [Mineral.cs](Assets/Scripts/Mineral/Mineral.cs) with realistic rarity tiers, vein clustering, 3D crystal meshes, and educational descriptions:
  - *Apatite*, *Biotite*, *Calcite*, *Dolomite*, *Epidote*, *Flint*, *Fluorite*, *Limonite*, *Magnetite*, *Olivine*, *Pyrite*, *Quartz*, *Sphalerite*, *Sulfur*, *Tourmaline*, *Tremolite*, and *Wollastonite*.
- **Physical Raycast Extraction**: [MineralBehaviour.cs](Assets/Scripts/Mineral/MineralBehaviour.cs) distributes raycasts around each specimen. Minerals remain locked while buried, unlock for hand-grabbing once partially exposed (≥ 75%), and detach under full gravity upon complete excavation (100%).
- **Diegetic VR User Interfaces**:
  - **Left Wrist Scanner HUD**: [LeftControllerMineralHUD.cs](Assets/Scripts/UI/LeftControllerMineralHUD.cs) shows proximity identification and analysis states in world space.
  - **Holographic Collection Terminal**: [CollectionUI.cs](Assets/Scripts/UI/CollectionUI.cs) features catalog progress, detailed specimen data, and a 3D hologram pedestal.
- **Tactile Haptics & Spatial Audio**: Controller vibration feedback and spatialized sound effects for pickaxe impacts and mineral drops.
- **Protected Zones**: [NonDiggableZone.cs](Assets/Scripts/ProceduralMeshGeneration/NonDiggableZone.cs) bounds laboratory walls and floors to prevent unwanted deformation.

---

## Controls Reference (Meta Quest)

| Hand / Controller | Action | Description |
| :--- | :--- | :--- |
| **Right Grip** | Grab Pickaxe | Pick up and hold the excavation pickaxe |
| **Right Swing** | Dig Terrain | Swing pickaxe with velocity (> 0.4 m/s) against rock |
| **Left Grip** | Grab Mineral | Grasp exposed mineral specimens from the rock face |
| **Left Index Trigger** | Collect Mineral | Deposit held mineral into the collection database |
| **Left Wrist** | Mineral Scanner | Glance at left wrist HUD for real-time sample diagnostics |
| **Left Thumbstick Click** | Toggle Crouch | Smoothly lower view height and adjust player collider |
| **Direct Touch / Ray** | Lab Terminal | Interact with catalog cards and holographic specimen spawner |

---

## Getting Started

### Prerequisites
- **Unity Editor**: `6000.4.4f1` (Unity 6) with **Android Build Support** (OpenXR, NDK, SDK)
- **Render Pipeline**: Universal Render Pipeline (URP 17.4)
- **VR Headset**: Meta Quest 2, 3, or Pro (via Quest Link, AirLink, or standalone APK)

### Installation & Launch
1. **Clone the repository**:
   ```bash
   git clone git@github.com:ryuk4real/stratum.git
   cd stratum
   ```
2. **Open in Unity Hub**:
   - Add the project folder and ensure the editor version is set to **Unity 6 (6000.4.4f1)**.
   - Wait for Unity Package Manager to resolve packages declared in [Packages/manifest.json](Packages/manifest.json).
3. **Open the Main Scene**:
   - Open `Assets/Scenes/MainScene.unity` and enter **Play Mode** (with Quest Link connected).

### Standalone Quest Build (APK)
- In Unity, switch the build target to **Android** (**File** -> **Build Profiles**).
- Verify that **OpenXR** with **Meta Quest Support** is enabled under **Project Settings** -> **XR Plug-in Management**.
- Click **Build and Run**, or deploy the prebuilt APK located at [Build/stratum.apk](Build/stratum.apk):
  ```bash
  adb install -r Build/stratum.apk
  ```

---

## Credits & Third-Party Assets

- **Laboratory & Office Prefabs**: [Free Sci-Fi Office Pack](https://assetstore.unity.com/packages/3d/environments/sci-fi/free-sci-fi-office-pack-195067) by Crebot
- **3D Mineral Models & Descriptions**: [EDUROCK - Aalto University](https://sketchfab.com/EDUROCK_AALTO/collections/minerals-65ee2970da65445eb55590005c3727ea) on Sketchfab
- **Terrain Textures**: [Realistic Terrain Textures Free](https://assetstore.unity.com/packages/2d/textures-materials/floors/realistic-terrain-textures-free-279940) by Hedgehog Team
- **Pickaxe Model**: [Basic Pickaxe](https://assetstore.unity.com/packages/3d/props/tools/basic-pickaxe-33718) by Starlight Arts

---

## License

This project is licensed under the **GNU General Public License v3.0**. See the [LICENSE](LICENSE) file for details.
