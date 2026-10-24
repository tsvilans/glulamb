# Beams, blanks and placements

A design note on how GluLamb models what a timber component is designed as, and what it is made
from. Written 2026-10-24; steps 3 and 4 below are proposals, not decided.

## Three things, not two

- **Beam**: the design object. Final geometry (centreline, orientation, section and its offsets),
  topology and features (joints, cuts, drillings). Lives in design space.
- **Blank**: the manufactured body a beam is made from: a glulam, a segmented blank, later other
  kinds. Material and process data (strength class, species, grades, layup, machining allowances)
  and its geometry as made, in its own space. `IBlank` in `GluLamb/Blanks`.
- **Placement**: where a beam sits in a blank: blank id, beam id, the transform from the blank to the
  beam, and for a beam cut from a longer blank, where along it the beam starts. `Placement` in
  `GluLamb/Blanks`; Cluster Beams makes them.

A beam and its blank usually travel as a pair, but they are separate objects with their own ids:
one blank (a blank design) can serve several beams, which is what Cluster Beams finds, and a beam
doesn't have to be the same shape as the blank it is cut from.

`Glulam` derives from `Beam`, because it started as a beam with material properties. That is kept
for now (see the migration below), but new code should treat a glulam as a blank: `Glulam` has its
own id, no section offsets (`Glulam.CreateGlulam(beam)` builds it on the beam's section centreline),
and implements `IBlank`.

## A blank is layers on a form

A pressed blank is a stack of layers laid on a form; its outer shape follows from the layers.

- A layer is a lamella (glulam) or a course of short overlapping segments cut from straight boards
  and laid like bricks (`SegmentedBlankX`).
- Each layer has a thickness, a species and grade, and a start and end along the form
  (`BlankLayer`).
- Straight, single- and double-curved glulams are the same kind of layup on different forms; a
  constant-section glulam is the case where every layer runs the full length.
- **Stepped (haunched) layups**, e.g. a glulam arch thicker at the knee than at the ends, with two
  flat outer faces and a curved inside, are layers that don't all run the full length. The section
  then changes along the blank, so `IBlank` evaluates it along the blank (`GetSection(t)`) rather
  than having one width and height.
- Whether a beam fits a blank (with its allowances) is then a check of the beam's body, moved by its
  placement, against the blank's shape, rather than "the same constant section within a tolerance".

## Form and released geometry

Pressing changes the geometry: a blank pressed on a form springs back when it is released, and can
skew. So a pressed blank has two shapes:

- its **form geometry**, the shape it is pressed to (the press bed), and
- its **released geometry**, the shape it is expected to take afterwards.

The placement maps the beam into the released shape; the form geometry is what the press is set to
(the Overbend component already works out a form from a target shape). Later, a placement could
carry a deformation as well as a rigid transform.

## Migration

1. **Done:** `IBlank`, `BlankMaterial`, `BlankLayer` and `Placement`. `Glulam` and `SegmentedBlankX`
   implement `IBlank`; Cluster Beams outputs placements. Nothing else changed.
2. **Done:** `SegmentedBlankX` has an id, material, section, envelope Brep, volume and layers; the
   unused older `SegmentedBlank` is marked obsolete.
3. **Proposal:** layer extents in `GlulamData` (a start and end per layer) for stepped layups, with
   the glulam's shape built from its layers.
4. **Proposal:** form and released geometry on pressed blanks, tied to Overbend.
5. **Later:** `Glulam` stops deriving from `Beam` and holds its own centreline and orientation; a
   beam refers to its blank through a placement. This touches the Grasshopper goo and the older
   Element-based code, so it can go gradually: new code (Cluster Beams, G2P, BTLx) works with
   `IBlank` and `Placement` first.
