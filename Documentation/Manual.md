Destructible Structure System

Version: <v1.0.0>Author / Publisher: Unity Compatibility: 2021.3 LTS – 2022.3

🚀 Overview

This Unity package contains a complete system for building and simulating destructible structures.

Major directories:

Runtime/ – C# scripts for gameplay.

Editor/ – custom editor tools and inspectors.

Materials/, Textures/, Effects/ – assets (materials, textures, particle effects, and audio).

Samples/ – sample scene.

Documentation/manual.pdf – user manual (not viewed here).



✨ Features

Destruction Framework – Base Destructible class for objects that crumble, spawn debris, and trigger onCrumble events via GibManager.

Wall Pieces – Grid-based wall cells that manage structural state and trigger effects on destruction.

Structural Members & Connections – Voxel-based beams and columns that manage splitting, detachment, and support connections.

Group Manager – Coordinates structural behavior, effects, load integrity, and collapse logic.

Wall Manager – Builds, detaches, and optimizes wall pieces.

Chunks & Gibs – Chunk objects combine meshes; GibManager handles pooling and random force application.

Utilities – Includes voxel generators, mesh combiner, cache, and debris slicers.

Scriptable Objects – StructureBuildSettings and WallDesign for customizable wall and structure parameters.

Editor Tools – Scene view tools for building/editing structures, material assignment, and stress visualization.

Samples & Assets – Example scene and URP-compatible materials, textures, sounds, and particles.

📦 Installation

Import the .unitypackage (double-click or Assets ▸ Import Package ▸ Custom Package…).

Or install via UPM Git URL:

https://github.com/YourOrg/YourRepo.git?path=/Packages/com.yourorg.destructiblestructure

Dependencies:

TextMeshPro (included with Unity)

URP 14 + (optional for advanced shaders)

🚀 Getting Started

Open the window: Tools ▸ Structure Build Tool.

Select a GameObject in the scene.

Use scene view modes to build and modify structural elements.

Screenshot/GIF goes here:
![First Run](Screenshots/getting-started.gif)

🛠️ Usage Guide

Section

What it does

Structure Build Tool

Enables building and editing walls, beams, and materials

StructuralGroupManager

Manages structural integrity and effects

WallPiece

Handles per-cell damage and debris spawning

GibManager

Pools and spawns debris with optional explosion forces

Stress Gizmo

Visualizes member stress levels in Scene view

<Add more sub-sections, code snippets for API calls, best-practice tips, etc.>

❓ FAQ & Troubleshooting

The window is blank

Make sure you are in the Scene view and a valid GameObject is selected.

Undo isn’t working

Confirm Edit ▸ Preferences ▸ Undo is set to at least 99 steps.

🗒️ Changelog

### v1.0.0 – 2025-06-13
- Initial release with destruction system, editor tools, and sample assets

🧑‍💻 Support

Email: support@yourdomain.com

Forum Thread: https://forum.unity.com/threads/destructible-structure-system

Issue Tracker / Feature Requests: https://github.com/YourOrg/DestructibleStructure/issues

📄 License

This asset is distributed under the Unity Asset Store End-User License.See LICENSE.md for full terms.

© 2025 <Your Name / Studio>. All rights reserved.