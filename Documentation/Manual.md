Overview

This Unity package contains a complete system for building and simulating destructible structures. Major directories:

Runtime/ – C\# scripts for gameplay.

Editor/ – custom editor tools and inspectors.

Materials/, Textures/, Effects/ – assets (materials, textures, particle effects, and audio).

Samples/ – sample scene.

Documentation/manual.pdf – user manual (not viewed here).

Key Features & Systems

Destruction Framework

Destructible is the base class for objects that can crumble into debris. It stores pre‑generated mesh fragments and invokes an onCrumble event when destroyed. Debris pieces are spawned via a global GibManager. 

Wall Pieces

WallPiece represents a single cell in a wall grid. It keeps references to its WallManager, attached structural members, and exposes onDestroyed and onWindowShatter events. When damage exceeds a threshold it plays effects via the owning group manager and spawns debris. 

Structural Members and Connections

StructuralMember builds voxel pieces to form beams or columns. It manages splitting, detaching, and adjacency after pieces break.

StructuralConnection links members and can be damaged or destroyed. Slots identify relative directions (top, bottom, etc.) with helper lookups.

Both types derive from Destructible.

Group Manager

StructuralGroupManager orchestrates all members, connections, and walls in a structure. It handles load propagation, validates integrity, spawns effects (crumble, member stress, large collapse, window shatter) and maintains cooldowns for sounds and particle effects. 

Wall Manager

Builds walls out of WallPiece cells, including windows or triangles. It detaches chunks when damaged and recombines pieces into chunks for performance.

Chunks & Gibs

Chunk acts as a proxy for combined meshes.

GibManager pools debris pieces, tracks active gib counts, and can apply random explosion forces. 

Utilities

VoxelBuildingUtility creates irregular cubes, windows, and triangular wall pieces procedurally.

MeshCombinerUtility merges meshes by material to produce combined chunks. 

MeshCacheUtility (editor-only) caches generated meshes to disk.

GibBuildingUtility slices meshes into fragments for debris generation.

Scriptable Objects

StructureBuildSettings defines configurable parameters for building structures and walls (member length/thickness, materials, wall dimensions, voxel health/mass, etc.). 

WallDesign stores custom wall templates.

Editor Tools

StructureBuildTool provides scene view modes for creating structures, building members/walls, editing walls, applying materials, and deleting elements. It responds to mouse input and draws gizmos.

Custom inspectors (MemberPieceEditor, StructuralGroupManagerEditor) expose debug info and settings in the Inspector.

StructuralMemberStressGizmo visualizes member stress levels in the Scene view.

Samples and Assets

Example scene under Samples/DemoScene demonstrates usage.

Built‑in and URP materials, textures, default sound clips, and particle prefabs ship with the package for immediate use.  
