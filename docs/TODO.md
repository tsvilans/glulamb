# Joint system and G2P: open items

Kept up to date as items are done or added. Last updated 2026-11-12.

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
- Divide For Length: check whether splice-lap trims a splice overlap that is longer than its lap.
- Three-beam nodes: TT (two arms from opposite sides) is split into two T joints; consider a dedicated double-T joint, and finer classes for the remaining 3J nodes.
- Birdsmouth: a rafter whose centreline is below the plate's centre is untested; the seat may go on the wrong face.
- K tenon: nothing holds the second arm.
- Four-way split: generate the plate itself (the original only cut the slots); Grasshopper inputs for the inner and outer surfaces.
- K plate joist fails in the gallery's layout (the joist overlaps the sill); check how the joist should be arranged.
- Plates: how plate edges and corners meet the milled slot ends. Plates round only their interior corners now; the original K-joint project modelled plates with their own routine and production marking.
- Control the order of beams within a joint from the components, so Flip and "first part" are predictable.

## Blanks

- Cluster Beams: sections only match as given (width with width); a beam whose width and height are swapped (a quarter-turn roll) isn't matched, and neither are square sections a quarter turn apart.
- Cluster Beams: with a very large Length Tolerance, sliding beams along each other is the slow part (about 3.5 s for 1000 beams, against 0.5 s at 500); prune pairs before sliding if it matters.
- Cluster Beams: length bands are greedy from the longest beam down; fixed standard blank lengths (round up to increments) aren't supported.

- Blank types: unify them later. `CixBlank` only holds CIX-related variables and derives from `SegmentedBlank`. `SegmentedBlank` is a glulam whose "lamellae" are short overlapping segments cut from straight boards and laid in layers like bricks: single-curved, constrained, and its lamellae aren't continuous, but it shares a lot with `Glulam`.

## Two-step lamination (prototype)

- Springback, at both steps: G1 springs back when it comes off its form, and the free-form blank (G3) when it comes out of the press. Model form and released geometry for both (see docs/blank-model.md) and overbend accordingly.
- Glue lines in both directions in G3 (G1's board joints run along each G3 lamella): implications for strength class and for where joints may cut.
- Which axis to stack along can depend on the beam's geometry, so the blank's section orientation may differ from the beam's; choose it per beam.
- Twist strain uses the larger of the edge stretch of a twisted strip, (w/2)^2 tau^2 / 2, and half the face shear, t tau / 2, against the bending limit 1 / (2 x radius ratio). Check that rule against how lamellas actually take twist.
- Analyze Lamella Bending and Estimate Curvature are superseded by Analyze Glulam and Visualize Lamella; retire them once those are confirmed.
- True shape: the lamellas are lined up by best fit over their length; add lining up at a mark (locators), as the press does it.
- Constraints (docs/constraints.md): move `GlulamData.Compute` and `Glulam.RadiusMultiplier` to rule sets and remove the `Standards` classes; check the rule values against the standards; add quantities for lengths, press and machine limits.
- `Utility.GetTrueGlulamBlank` / `GetGlulamBlankSurfaces` (Unroller and Sporph) are superseded by `TwoStepLamination`; retire them once it is confirmed.

## Reconstruction

- Dual contouring (`DualCon`, the Remesh component): the code moved over from GluLamb.Raw as it was. It needs the native `dualcon.dll` next to `GluLamb.Reconstruction.dll`, which isn't in the repo yet. Later: package or build the library, give `DualCon` a managed output buffer instead of its instance field (and drop the leftover handle code and console output), and test it on a real scan before Beam From Mesh.
- `sketches/machining/RawUtility.cs` (`GetPlanarTCP`, a robot tool-centre-point fit sketch from GluLamb.Raw): move it to the machining project.

- Beam From Mesh on reconstruction-example-02 (the beam with the bevelled, grooved profile): the centreline still does odd things at one end. Investigate.

## Solution clean-up

- RawLam (`GluLamb/RawLam`: boards, logs, lamella performance): kept in core for now; explore later whether it belongs with the glulam material code or in its own project.
- Consolidation steps 5 to 7 (`Projects/` out of core, test apps in the solution, net7.0 everywhere): see the solution review.
- GluLamb.Rhino: the model panel is empty and the beam properties page only shows the beam's GUID. Neither was fleshed out; design and build both.
- GluLamb.Rhino: the old Rhino 7 plug-in's object properties page (`GluLambObjectPropertiesPage`, removed with it, in git history) had more in it than `BeamPropertiesPage`; check whether anything should come back.

## Gallery and catalogue

- Some catalogue views are unclear; review them.
- Regenerate the catalogue for the newer types (cross-birdsmouth, corner-mitre, splice-butt, corner-butt).

## Cleanup

- Retire the legacy joint classes (`Joints/Defaults`, the old `*X` classes) once the ports are confirmed.
- Rethink or retire the remaining Element-based Grasshopper components.
- BTLx export moves to a separate extension that combines the BTLx wrapper with GluLamb types; `Btlx/BtlxWriter.cs` is only a proof of concept.
- Icons for the new components and commands. Divide For Length borrows the Split Beam icon.
