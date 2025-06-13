# Destructible Structure System Manual

**Version**: 1.0.0
**Compatibility**: Unity 2021.3 LTS – 2022.3

---

## Overview

Destructible Structure System lets you build gameplay‑ready buildings that can splinter, crumble and collapse in real time. The core workflow happens inside the **Structure Manager** editor window, which exposes a set of “Build Modes” for quickly laying out connections, beams, columns and walls. Runtime components handle stress propagation, debris pooling and event dispatch.

### Top‑level Folders

| Folder                              | Purpose                                                                               |
| ----------------------------------- | ------------------------------------------------------------------------------------- |
| `Runtime/`                          | All MonoBehaviours that run in play mode (destruction logic, stress solver, pooling). |
| `Editor/`                           | Custom windows & scene tools (`ManagerWindow`, `StructureBuildTool`, inspectors).     |
| `Materials/` `Textures/` `Effects/` | Demo assets for quick prototyping.                                                    |
| `Samples/`                          | A mini‑scene that demonstrates every build mode.                                      |

---

## Feature Highlights

* **Build Modes Toolbar** – eight discrete modes: *Create Structure,* *Structural Member Build,* *Grounded Toggle,* *Wall Build,* *Wall Edit,* *Apply Design,* *Apply Material,* *Delete*.
* **Parametric Members** – beams and columns with user‑defined length, thickness, texture scale and support capacity.
* **Wall System** – grid‑based walls (cubes, windows, triangular cut‑outs) with per‑cell health.
* **Design Presets** – save any wall layout as a `WallDesign` ScriptableObject, then re‑apply with one click.
* **Stress Solver** – voxel members propagate damage with configurable min/max delay.
* **Gib Manager** – pools debris; lifetime, pool size and spawn‑rate are tweakable from the Manager window.
* **Mesh Cache Utility** – persist generated meshes between play sessions to cut import time.
* **Undo‑Friendly Workflow** – every spawn, delete or property change is wrapped in a single Unity Undo group.

---

## Installation

1. Download the `.unitypackage` from the Asset Store.
2. In Unity select **Assets ▸ Import Package ▸ Custom Package…** and open the file.
3. Press **Import** when the package dialog appears.

---

## Quick Start (90 seconds)

1. Open the sample scene **`Samples/DemoScene`** (or an empty scene of your own).
2. Choose **Tools ▸ Structure Manager** to open the main window.
3. In the **Build Settings Asset** field press **Create BuildSettings Asset** and save it anywhere inside *Assets/*.
4. Set *Build Mode* to **Create Structure** and click once in the Scene view – a cyan cube appears (the first connection).
5. Switch to **Structural Member Build**. Hover over the connection; cyan ghost beams appear. Click a direction to spawn a beam and an end‑connection.
6. Now choose **Wall Build**. Hover a beam to preview a magenta ghost wall, then click to create it.
7. Select **Wall Edit** ➜ sub‑mode **Add Window**, and click any wall cell to punch a window. Use **Add Triangle** for corner cut‑outs.
8. Hit **Play**. Shoot or collide with the structure and watch pieces detach, crumble and spawn pooled debris gibs.

ℹ️  *The first play‑run after edits may take a moment while procedural meshes are baked. Enable ****Mesh Cache ▸ Enable Mesh Persistence**** to speed up subsequent sessions.*

---

## Building Structures in Detail

The toolbar (top of **Structure Manager**) controls editing context. Each mode has unique scene gizmos:

| Icon¹ | Mode                        | Scene action                                                                                                                                       |
| ----- | --------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| 🏗️   | **Create Structure**        | Place a root **Structural Connection** (start of a building).                                                                                      |
| 🔧    | **Structural Member Build** | Click a connection, then click any cyan ghost to spawn a beam/column. Automatic end‑connection creation & adjacency wiring.                        |
| ⏭️    | **Grounded Toggle**         | Click members to mark them green (immovable) or yellow (floating).                                                                                 |
| 🧱    | **Wall Build**              | Hover a member to preview a wall. Click to spawn with default design.                                                                              |
| ✏️    | **Wall Edit**               | Requires a wall selected. Sub‑modes:• **Delete Piece** (red)• **Add Piece** (green)• **Add Window** (blue)• **Add Triangle** (cyan corner buttons) |
| 🎨    | **Apply Material**          | Pick a material in the Inspector, then click any member, connection or wall.                                                                       |
| 🎴    | **Apply Design**            | Assign a `WallDesign` asset, then click a wall to replace its grid.                                                                                |
| ❌     | **Delete**                  | Click a member, connection or wall to remove it (red gizmo).                                                                                       |

¹ *Exact icons are loaded from **`Editor/Icons/*.png`**; names match those in **`modeIcons`**.*

### Saving Wall Designs

While in **Wall Edit** you can capture the current wall to a reusable asset:

1. Press **Select…** beside *Current Folder* to choose a directory inside *Assets/* for design files.
2. Enter a unique *Design Name* (warning message turns green when valid).
3. Hit **Save Design** – a `.asset` file is created containing cell types, rows, columns and materials.

---

## Scene Setup Tips

* **GibManager**: open **Structure Manager ▸ Runtime Debris Settings** and click **Create GibManager in Scene** if none exists. Tweak pool sizes and lifetimes here.
* **Physics**: keep *Fixed Timestep* at **0.02 s** or lower for stable stress propagation.
* **Mesh Cache**: enable in **Mesh Cache** fold‑out. Press **Clean Unused Cached Meshes** after large refactors.
* Disable **Auto Sync Transforms** (Project Settings ▸ Physics) for heavy debris scenes.
* When baking lightmaps, mark procedural pieces as *Static = false* to avoid long bake times.

---

## Scripting API (Runtime)

```csharp
using Mayuns.DSB;

public class ExplosionTrigger : MonoBehaviour
{
    public void OnEnable()
    {
        Destructible.onCrumble += HandleCrumble;
        WallPiece.onWindowShatter += ShowGlassParticles;
    }

    void HandleCrumble(Destructible d)
    {
        // award points, play UI shake, etc.
    }

    void ShowGlassParticles(WallPiece w)
    {
        ParticleSystemManager.Spawn("GlassShards", w.transform.position);
    }
}
```

---

## FAQ

**Q:** *Why are my walls pink?*
**A:** Assign a material in **Build Settings ▸ Wall Material** or use **Apply Material** mode.

**Q:** *Pieces disappear when they hit the ground.*
**A:** Increase *Max Active Gibs* in the GibManager – chunks beyond this limit are pooled immediately.

**Q:** *Undo doesn’t restore everything.*
**A:** Make sure you performed each action through the **Structure Manager**. Manual hierarchy edits bypass the tool’s grouped Undo.

---

## Support

Email **[support@yourdomain.com](mailto:support@yourdomain.com)**
Unity Forum Thread – *Destructible Structure System*
Issue Tracker – GitHub *Mayuns / DestructibleStructure*

---

## Changelog

### 1.0.0 – 2025‑06‑13

* Initial release: build‑mode toolbar, parametric voxel members, wall grid authoring, design presets, stress solver and debris pooling.

---

## License

Distributed under the standard Unity Asset Store EULA; see `LICENSE.md`.

© 2025 Mayuns Technologies. All rights reserved.
