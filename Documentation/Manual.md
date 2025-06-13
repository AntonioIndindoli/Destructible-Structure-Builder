# Destructible Structure System Manual

**Version**: 1.0.0  
**Compatibility**: Unity 2021.3 LTS – 2022.3

---

## Overview

Destructible Structure System provides everything you need to create interactive buildings that can crumble, shatter and collapse during play. The package ships with editor tools, runtime scripts and a complete sample scene to help you get started quickly.

**Folders**

- `Runtime/` – core C# scripts used at run time.
- `Editor/` – inspectors and scene tools.
- `Materials/`, `Textures/`, `Effects/` – demo assets such as materials, particles and audio clips.
- `Samples/` – example scene showcasing a small structure.

## Features

- **Destruction Framework** – base classes that spawn debris, apply forces and trigger events when pieces break.
- **Wall Pieces** – grid-based wall cells with per-cell health and window support.
- **Voxel Members** – beams and columns that split into separate groups and apply stress between neighbours.
- **Group Manager** – coordinates member detachment, collapse audio and particle effects.
- **GibManager** – pools debris chunks and applies random impulses.
- **Editor Tools** – dedicated window for building, stress visualisation and quick material assignment.
- **Scriptable Objects** – reusable presets for wall designs and structural settings.

## Installation

1. Download the `.unitypackage` file from the Asset Store.
2. In Unity choose **Assets ▸ Import Package ▸ Custom Package...** and select the downloaded file.
3. Alternatively add the package via UPM using the Git URL:
   ```
   https://github.com/YourOrg/YourRepo.git?path=/Packages/com.yourorg.destructiblestructure
   ```
4. The package depends on **TextMeshPro** (included with Unity) and optionally **URP 14+** for the provided materials.

## Getting Started

1. Open the window **Tools ▸ Structure Build Tool**.
2. Select or create a GameObject in your scene that will hold the structure.
3. Use the scene view buttons to place walls, beams and supports.
4. Press **Play** and interact with the structure using rigidbodies or scripted events.

![First Run](Screenshots/getting-started.gif)

## Building Structures

The build window has three modes:

1. **Wall Mode** – click and drag to draw walls on the selected plane. Choose a `WallDesign` preset to set thickness and materials.
2. **Member Mode** – place individual beams or columns to reinforce your structure.
3. **Material Mode** – quickly apply different materials to selected pieces.

Use the **Stress Visualizer** to preview which members carry the most load while in Edit mode.

### Scene Setup Tips

- Keep the centre of the structure at the origin so pooled debris reuse works correctly.
- Assign your own sounds and particle effects by creating a **StructuralEffects** asset and referencing it from the `StructuralGroupManager` component.
- To improve performance combine static meshes with the supplied **ChunkCombiner** utility.

## Scripting API

The system exposes several useful events:

- `MemberPiece.onDestroyed` – called when an individual member is broken.
- `WallPiece.onDestroyed` – fired when a wall cell crumbles.
- `WallPiece.onWindowShatter` – invoked specifically for window cells.
- `Destructible.onCrumble` – triggered when an entire destructible object collapses.

Subscribe to these events to spawn additional effects or drive gameplay logic.

```
public class ExplosionTrigger : MonoBehaviour
{
    public Destructible target;

    void Start()
    {
        target.onCrumble.AddListener(OnStructureCrumble);
    }

    void OnStructureCrumble()
    {
        // your custom behaviour here
    }
}
```

## FAQ

**The build window shows nothing.**  
Make sure you are in **Scene** view and a GameObject is selected.

**Undo is not restoring previous builds.**  
Check **Edit ▸ Preferences ▸ Undo** and set steps to at least 99.

**Pieces fall apart immediately.**  
Verify that colliders do not intersect each other when the simulation starts.

## Support

For assistance or to report bugs:

- Email: support@yourdomain.com
- Forum: [Unity Forum Thread](https://forum.unity.com/threads/destructible-structure-system)
- Issue Tracker: [GitHub Issues](https://github.com/YourOrg/DestructibleStructure/issues)

## Changelog

### 1.0.0 – 2025‑06‑13
- Initial release with full destruction framework, editor tools and sample assets.

## License

This asset is distributed under the Unity Asset Store End-User License. See `LICENSE.md` for full terms.

© 2025 Your Name / Studio. All rights reserved.
