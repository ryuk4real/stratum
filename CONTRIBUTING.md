# Contributing to Stratum

Thank you for your interest in contributing to **Stratum**! We welcome bug reports, feature suggestions, documentation improvements, and pull requests.

---

## Code of Conduct

Please be respectful, collaborative, and constructive when opening issues and participating in pull request discussions.

---

## Development Workflow

### 1. Requirements & Environment
- **Unity Version**: Ensure you use the exact Unity Editor version defined in `ProjectSettings/ProjectVersion.txt` (`6000.4.4f1` / Unity 6).
- **Target Platform Modules**: Android Build Support (OpenXR, NDK, SDK) for Quest standalone testing, or Windows/Linux standalone for PCVR.
- **VR Hardware**: Meta Quest 2 / 3 / Pro.

### 2. Forking and Branching
1. Fork the repository on GitHub: `https://github.com/ryuk4real/stratum`.
2. Clone your fork locally:
   ```bash
   git clone git@github.com:<your-username>/stratum.git
   cd stratum
   ```
3. Create a feature or bugfix branch with a descriptive name:
   ```bash
   git checkout -b feat/new-mineral-type
   # or
   git checkout -b fix/pickaxe-sweep-collision
   ```

### 3. Unity & Git Best Practices
- **Meta Files (`.meta`)**: Always commit corresponding `.meta` files when adding, moving, renaming, or deleting assets and scripts. Missing `.meta` files cause broken GUID references.
- **Clean Commits**: Avoid committing temporary Unity directories (`Library/`, `Temp/`, `Logs/`, `UserSettings/`, `.utmp/`). Check `.gitignore` before staging.
- **Scene Merges**: Avoid simultaneous multi-developer edits to `Assets/Scenes/MainScene.unity` where possible; utilize prefabs to isolate component changes.
- **Compute Shaders**: Test GPU compute shaders (`Assets/Scripts/ProceduralMeshGeneration/MarchingCubes3D.compute`) for mobile GPU compatibility (avoid unaligned structured buffer strides and unbounded loops).

### 4. Coding Standards
- Write clean, documented C# code following Microsoft and Unity naming conventions:
  - `PascalCase` for public methods, properties, and class names.
  - `camelCase` with private/protected fields (optionally prefix serialized private fields with meaningful names).
  - Use `[SerializeField]` with private fields instead of public fields where Inspector exposure is required.
  - Use XML documentation comments (`/// <summary>`) for classes and non-trivial public methods.

---

## Submitting a Pull Request

1. Push your branch to your remote fork:
   ```bash
   git push origin feat/new-mineral-type
   ```
2. Open a Pull Request against the `main` branch of `ryuk4real/stratum`.
3. Provide a clear summary of your changes in the PR description:
   - What problem does this solve or what feature does it add?
   - How was it tested (VR hardware / Desktop simulator)?
   - Any inspector settings or prefabs modified.
