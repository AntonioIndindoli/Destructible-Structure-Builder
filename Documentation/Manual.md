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

## Getting Started

1. Open the window **Tools ▸ Structure Build Tool**.
2. 
3. 
4. 

![First Run](Screenshots/getting-started.gif)

## Building Structures

The build window has multiple modes:

1. **Wall Mode** – click and drag to draw walls on the selected plane. Choose a `WallDesign` preset to set thickness and materials.
2. **Member Mode** – place individual beams or columns to reinforce your structure.
3. **Material Mode** – quickly apply different materials to selected pieces.
4. 

### Scene Setup Tips

- 
- 
- 

## Scripting API

The system exposes several useful events:

- `WallPiece.onWindowShatter` – invoked specifically for window cells.
- `Destructible.onCrumble` – triggered when a destructible voxel crumbles.

Subscribe to these events to spawn additional effects or drive gameplay logic.

## FAQ


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

© 2025 Mayuns Technologies. All rights reserved.
