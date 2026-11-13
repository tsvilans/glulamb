# Bending, twist and developability of a free-form blank

A guide to how the shape of a free-form beam splits into what each step of a lamination has to
make. It is the reasoning behind `TwoStepLamination` and `LeastTwist` (see
[two-step-lamination.md](two-step-lamination.md)), but it holds for any blank made of bent
lamellas. Written 2026-11-13.

## The short version

- A lamella is a flat strip. It bends easily about its thickness, hardly at all in its own plane,
  and it resists twist.
- At every point of the beam, the centreline's curvature splits into a part **out of the
  lamella's plane** (pressed in), and a part **in its plane** (it has to be cut, or laminated,
  into the lamella beforehand). How it splits depends only on which way the lamellas face.
- **Twist** is how fast the lamellas' facing direction turns about the centreline, measured
  against a frame that doesn't turn at all (the rotation-minimizing frame).
- A lamella layer is **developable**, i.e. it can be pressed from a flat lamella without
  stretching, exactly when it has no twist.
- If the centreline is not planar, you cannot have both a fixed split and no twist. Its
  **torsion** has to go somewhere: either into twist of the blank, or into a split between
  in-plane and out-of-plane bending that changes along the length.

## The frame

Take one lamella layer of the blank and its centre curve. At each point there are three
directions:

| Symbol | Direction | In GluLamb |
| --- | --- | --- |
| T | along the curve | the section plane's Z-axis |
| N | square to the lamella's face: the stacking direction | the section's Y-axis (stack axis 1) or X-axis (0) |
| B = N x T | across the lamella's width | the other section axis |

Moving along the curve by a length ds, this frame turns in three ways, and those three rates are
all there is to the layer's shape:

| Rate | Name | The frame turns about | What makes it |
| --- | --- | --- | --- |
| kn | normal curvature (out of plane) | B | pressing the lamella over a form: step 3 |
| kg | geodesic curvature (in plane) | N | the flat shape of the lamella: step 1, kept through slicing |
| tg | geodesic torsion (twist) | T | twisting the lamella in the press: step 3 |

In formulas: T' = kn N + kg B, N' = -kn T + tg B, B' = -kg T - tg N.

## In-plane and out-of-plane bending

The centreline has one curvature k, pointing along its principal normal n. If the lamella's
normal N makes the angle phi with n (about T), then

    kn = k cos(phi)        kg = k sin(phi)        k^2 = kn^2 + kg^2

So the two bendings are the same curvature seen from the lamella: turn the lamellas to face the
bend (phi = 0) and it is all out of plane, which is an ordinary curved glulam; turn them edge-on
(phi = 90 degrees) and it is all in plane, which a flat lamella can only take if it was made with
that curve. Anything between is a mix. A double-curved beam is one where no single facing
direction keeps kg at zero all along.

Choosing the other stacking axis swaps the two: what was in-plane becomes out-of-plane.

What each costs:

| | Limited by | Strain |
| --- | --- | --- |
| kn (step 3) | the layer thickness t2 | t2 kn / 2 |
| kg (step 1) | the board thickness t1 of the single-curved glulam, at its inner face | t1 kg / 2 |

Both are held to the allowed strain 1 / (2 x radius ratio), which is the usual rule "thickness at
most radius / 200" written as a strain (1 / 400).

## Twist

Twist is not the same as "the section turns in space". Along a space curve a section can turn a
great deal in space, carried round by the curve's bending, and have no twist; a straight beam
whose section turns has nothing but twist. What counts is the turning of N about T that the curve's own bending doesn't
account for.

The reference is the **rotation-minimizing frame** (RMF): the frame that is carried along the
curve turning only as much as the curve's bending forces it to, never about T. Measure the
lamella's facing direction as an angle theta from the RMF. Then

    tg = d(theta) / ds

The twist is the rate that angle changes. Constant theta: no twist. The total twist of a beam is
theta at one end minus theta at the other.

A twisted lamella of width w and thickness t is strained in two ways:

- its edges run along helices around its centre, so they are longer than the centre by
  (w/2)^2 tg^2 / 2: an **in-plane stretch** that grows with the square of the width;
- its faces are sheared against each other by t tg (torsion of the strip).

`TwoStepLamination` holds the larger of the stretch and half the shear to the allowed strain.
(That rule is a first assumption; see docs/TODO.md.)

## Developability

A lamella layer is a ruled surface: a straight line (B) swept along the centre curve. Straight
lines across it are not enough for it to unroll flat. It unrolls only if the surface's normal is
the same all along each of those lines. Twist is exactly what breaks that: where N turns about T,
the normal at one edge of the strip has turned further than at the other, the surface is
saddle-shaped (like a hypar), and flattening it means stretching.

So:

    developable  <=>  tg = 0 all along  <=>  the lamellas' facing follows the RMF at a fixed angle

- For a developable layer, the flat lamella is exact: its centre curve has the curvature kg(s),
  and its edges are that curve offset by half the width. This is what "unrolling" computes, and
  it is why the development needs no surface or mesh, only kg along the curve.
- For a layer with twist, the same flat shape is still the right one for the centre of the
  strip. The edges are off by the stretch above, which is the measure of how far the layer is
  from developable. Narrow lamellas forgive twist; wide ones don't.

## Torsion of the centreline and torsion of the blank

Two different things carry the name:

- the **torsion of the curve**, tau: how fast its plane of bending turns. Zero for any planar
  curve. It is a property of the centreline alone.
- the **torsion (twist) of the blank**, tg: how fast its section turns about the centreline
  against the RMF. It is a property of the orientation you give the section.

They are tied together by the angle phi between the lamella normal and the curve's principal
normal:

    tg = tau + d(phi) / ds

Read as a budget: the curve's torsion tau is given by the design. It has to be paid either as
twist of the blank (tg), or as a change of phi, which is a change of the split between in-plane
and out-of-plane bending along the beam.

| Choice | phi | tg | Consequence |
| --- | --- | --- | --- |
| Section follows the curve's bending (Frenet-like) | constant | tau | Fixed split, e.g. all out-of-plane; the blank twists as much as the curve's torsion. Lamellas not developable. |
| Section follows the RMF | changes by the integral of -tau | 0 | No twist, developable lamellas; the bending moves between in-plane and out-of-plane along the beam, so both steps must take their share. |
| Anything between | | between | Trade twist against turning the section away from the design. |

For a planar centreline tau is zero and there is no conflict: face the lamellas into the bend at
a fixed angle and there is neither twist nor in-plane curvature. That is a single-curved glulam.
Two-step lamination exists for the cases where tau is not zero or the design fixes the
orientation: step 1 pays for kg, step 3 for kn and whatever twist is left.

`LeastTwist` works in these terms. `None` takes the RMF at the one fixed angle closest to the
design (tg = 0). `Even` keeps the design's orientation at both ends and spreads the difference
evenly (constant tg, the smallest peak twist for those ends). `Limited` stays as close to the
design as the allowed twist permits. Each reports how far the section turns from the design: the
cost of the easier blank.

## From one layer to the whole blank

All layers share N, but each sits at its own offset y along it, and its centre curve is not the
centreline:

- its length differs by the factor (1 - y kn): layers on the inside of the bend are shorter;
- its curvatures are scaled by about 1 / (1 - y kn), so the inner layers bend tighter. The
  thicknesses are set from the worst layer;
- with twist, an offset layer's curve winds around the centreline, which adds a little curvature
  of its own (y tg^2).

In two-step lamination every lamella is the same flat slice of one single-curved glulam (G1), but
each layer needs a slightly different flat shape. Two things follow:

- G1 has to be wide and long enough to hold every layer's flat shape at once (that is how its
  section is sized);
- each slice lands a little differently on its layer, so the pressed blank is not a clean swept
  rectangle: its sides are **skewed** across the stack and **stepped** between neighbouring
  lamellas, and its ends are staggered. Even a plain arc bent in the height does this: the
  slices are equally long, the layers are not, so the ends stagger.

`TwoStepLamination` gives that true shape: `DevelopedLayer.TrueMesh` is each lamella where it
really sits (steps and all), `TwoStepResult.TrueShape` the outside through the lamellas' edges,
and `EdgeMargin` says how much material is left outside the beam's section at the worst place.

## Straight lamellas

`StraightLamella.Evaluate` tests whether a beam can be made from straight lamellas, for each
stacking axis. Every layer must pass. A layer passes in one of two ways:

- **Forced**: a straight lamella is bent sideways into place. Its edge strain, kg x width / 2,
  stays within the allowed strain.
- **From a straight board**: the flat layer bows from its chord by no more than the spare width
  on each side (the edge allowance), so a straight board covers it.

In both, the twist and the out-of-plane bending must stay within their limits.

## Choosing a blank

The blank's orientation does not have to be the beam's. `BlankOptions.Evaluate` (Grasshopper:
Blank Options) gives the smallest blank of each kind that encloses the beam:

| Kind | Blank | Work | Material |
| --- | --- | --- | --- |
| Straight | along the best-fit line, turned to the smallest section | none: straight lamination | most waste |
| SingleCurved | along the centreline projected to its best-fit plane | one bend | less |
| Untwisted | along the centreline, section at one angle to the RMF (the angle with the smallest section) | bends in two directions, no twist: developable lamellas | a little waste, where the beam turns in the blank |
| AsBeam | the beam's own section | bends in two directions and twists | none |

Each option reports its volume, the fraction that is waste, the number of lamellae that the
rules give it, and its twist. The beam is then machined out of the blank that is the best trade
for the job.

## Laying parts in the press

`PressAlignment.Handle` gives a plane for a curved part: X from its start to its end, so the part
runs along X on average, and Y towards its bulge. `ToPress` moves that plane to the world XY
plane. The flat outputs of `TwoStepLamination` (G1, the slice, the lamella outlines) are already
laid this way: G1 starts at the origin and ends on the +X axis.

## Quick reference

| Beam | kn | kg | tg | Lamellas | Blank |
| --- | --- | --- | --- | --- | --- |
| Straight | 0 | 0 | 0 | straight, developable | straight glulam |
| Planar arc, lamellas facing the bend | k | 0 | 0 | straight when flat, developable | single-curved glulam |
| Planar arc, lamellas edge-on | 0 | k | 0 | flat arcs, developable | cut or pre-laminated curve |
| Planar curve, section tilted at a fixed angle | k cos | k sin | 0 | flat curves, developable | two-step, no twist |
| Straight, section turning | 0 | 0 | not 0 | straight when flat, not developable | twisted glulam |
| Space curve, section on the RMF | varies | varies | 0 | flat curves, developable | two-step, no twist |
| Space curve, section fixed to the bend | k | 0 | tau | straight when flat, not developable | twisted single-step glulam |
| Space curve, any other orientation | varies | varies | not 0 | flat curves, not developable | two-step with twist |
