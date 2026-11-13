# Two-step lamination (prototype)

`GluLamb/Blanks/TwoStepLamination.cs` and `LeastTwist.cs`; Grasshopper: Two-Step Blank and Least
Twist Orientation, under "4. Blank". Written 2026-11-12.

## Components

- **Lamination Options**: stacking axis, thicknesses, allowances, rules and material.
- **Two-Step Blank**: a beam and options in; G1, G3 and an analysis out.
- **Analyze Glulam**: opens the analysis (`GlulamAnalysis`): layers, flat lamellas, slice, true
  shape, boards, thicknesses, strains, curvatures, twist, edge margin, the straight-lamella check.
- **Named Frame**: gets a named plane of a beam or blank. The plane "press" orients a blank for
  the press (X along its chord, Y towards its bulge). G1 already lies along the world X-axis.

## The process

1. Flat boards (G0) are bent into a single-curved glulam (G1).
2. G1 is sliced with a horizontal bandsaw into curved lamellas (G2), parallel to the plane it is
   curved in.
3. The lamellas are pressed out of plane into the free-form blank (G3).

## How the geometry is found

Each layer of G3 is a strip along the beam: its normal N is the stacking axis (the beam's Y-axis
by default), B = N x T runs across it. The curvature of the layer's centre curve splits into:

- in-plane (geodesic) curvature, kg = k . B, made in step 1;
- out-of-plane (normal) curvature, kn = k . N, made in step 3;
- twist (geodesic torsion), tg = dN/ds . B, also made in step 3.

A layer is developed by turning a flat curve by kg along its length (the in-plane turning angle at
each sample), and its edges are offset either side. That is exact for the centre of the strip and,
where tg is zero, for the whole strip: a strip without twist is developable. No surface is
unrolled and no mesh is flattened.

All the developed layers are lined up with the middle one (they are all cut from one G1), and G1
is the single-curved glulam around them: its centreline follows the middle layer's development,
moved to the middle of all the outlines; its height (in the plane) holds them all; its width is
the stack of slices, layers x (thickness + planing) + (layers - 1) x kerf.

## The true shape of the blank

Every lamella is the same flat slice of G1 (`GlulamAnalysis.SliceOutline`), but each layer needs a
slightly different flat shape, so a slice lands a little differently on each layer and the pressed
blank is not a clean swept rectangle. Each point of the slice's outline is found in a layer's
development (how far along the beam, how far across the layer) and put at that place on the layer
in the blank, at the lamella's bottom and top faces:

- `DevelopedLayer.TrueMesh` / `TrueEdges`: each lamella where it really sits. Together they show
  the steps between neighbours and the staggered ends (the high-resolution shape).
- `GlulamAnalysis.TrueShape`: the outside through the lamellas' edges, sides skewed, steps smoothed
  over.
- `EdgeMargin`: the least material left outside the beam's section.

The lamellas are assumed to be laid up the way their developments were lined up: best fit over
their whole length. A press that lines them up at one mark would shift the steps.

For the reasoning, see [bending-twist-developability.md](bending-twist-developability.md).

## Limits

The limits come from a rule set (`TwoStepOptions.Rules`, see [constraints.md](constraints.md)),
asked with the material and the step. The allowed strain is the bending strain of a lamella at
its radius ratio limit, 1 / (2 x ratio) (Eurocode: 1 / 400). The rules set:

- the board thickness of G1 from its tightest inner radius (step 1);
- the layer thickness from the tightest out-of-plane radius over all layers (step 3);
- the twist a layer may take: the edge stretch of a twisted strip, (w/2)^2 tg^2 / 2 (which is also
  how far it is from developable), and half the face shear, t tg / 2, must both stay within it.

Thicknesses are capped at 45 mm, and rounded so that whole layers or boards fill the depth.

## Least twist

A layer whose normal follows the curve's rotation-minimizing frame has no twist. Measured from
that frame, an orientation's twist is the rate its angle changes along the curve, so:

- `None` keeps the frame at the one angle closest to the design: every layer developable;
- `Even` keeps the design's end orientations and turns evenly between them;
- `Limited` is the orientation closest to the design (smallest largest turn) whose twist stays
  within the allowed twist.

Each reports how far it turns the section from the design, which is the cost to the designer.

## Not yet

Springback, glue lines in two directions, choosing the stacking axis per beam: see docs/TODO.md.
