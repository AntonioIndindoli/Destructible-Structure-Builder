# Destructible-Structure-Builder

## Event Hooks

`MemberPiece` and `WallPiece` expose `onDestroyed` events and `Destructible` exposes `onCrumble`.
`WallPiece` also exposes `onWindowShatter` for window pieces.
`StructuralGroupManager` now plays additional effects automatically:

- **MemberStress** clips trigger when overloaded members are damaged over time.
- **LargeCollapse** clips trigger when a detached group with more than four members is created.
- **WindowShatter** clips trigger when a window piece is destroyed.

Audio clips are loaded from `Resources/SoundEffects` when no custom clips are assigned.
