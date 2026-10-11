# Joint system and G2P: open items

Kept up to date as items are done or added. Last updated 2026-10-11.

## G2P (Rhino plug-in)

- Joint dowels and screws as G2P fixings (fixings are plain cylinders without metadata for now).
- Single-component joints (end cut, plate pocket, drilling) in JoinComponents; needs point picking.
- Curved beams: a ComponentToBeam variant that also takes a centreline curve.
- Detailing, phase 2: a panel with the geometry buttons, a joint library with a parameter grid and Join, and selection info.
- Detailing, phase 3: edit and rebuild joints in place. Phase 4 (maybe): flag detailed geometry that is out of date.
- Cutting connectors on components that share a name (instances of one mark) is untested.
- D2P's `Objects.ObjectIDsByLayer` finds members by name, so same-named instances get each other's members. G2P works around it (`Utility.GetMemberIDs` goes by group); fix it upstream in D2P_Core.
- ExtendForJoints: joints made before 2026-10-11 have no recorded extensions and need joining again.
- Edge profiles: more types after TongueAndGroove (shiplap, rebate).

## Joints

- LapDepth (absolute depth in the lower beam) on t-housed, t-dovetail and anything else that assumes a midpoint plane; check the backcut cross laps' existing depth control.
- Mitre / butt splice: an option to line up a chosen face when the two sections differ (the cut is on the centrelines' bisector).
- Birdsmouth: a rafter whose centreline is below the plate's centre is untested; the seat may go on the wrong face.
- K tenon: nothing holds the second arm.
- Four-way split: generate the plate itself (the original only cut the slots); Grasshopper inputs for the inner and outer surfaces.
- K plate joist fails in the gallery's layout (the joist overlaps the sill); check how the joist should be arranged.
- Plates: how plate edges and corners meet the milled slot ends. Plates round only their interior corners now; the original K-joint project modelled plates with their own routine and production marking.
- Control the order of beams within a joint from the components, so Flip and "first part" are predictable.

## Gallery and catalogue

- Some catalogue views are unclear; review them.
- Regenerate the catalogue for the newer types (cross-birdsmouth, corner-mitre, splice-butt, corner-butt).

## Cleanup

- Retire the legacy joint classes (`Joints/Defaults`, the old `*X` classes) once the ports are confirmed.
- Rethink or retire the remaining Element-based Grasshopper components.
- BTLx export moves to a separate extension that combines the BTLx wrapper with GluLamb types; `Btlx/BtlxWriter.cs` is only a proof of concept.
- Icons for the new components and commands.
