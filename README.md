# Destructible-Structure-Builder

## Event Hooks

`MemberPiece` and `WallPiece` expose `onDestroyed` events and `Destructible` exposes `onCrumble`.
`StructuralGroupManager` now plays additional effects automatically:

- **MemberStress** clips trigger when overloaded members are damaged over time.
- **LargeCollapse** clips trigger when a detached group with more than four members is created.

Audio clips are loaded from `Resources/SoundEffects` when no custom clips are assigned.
