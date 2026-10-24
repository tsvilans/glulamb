# GluLamb.Reconstruction

Beams and blanks reconstructed from other geometry. It is kept apart from GluLamb so that the core
library doesn't carry it. Its Grasshopper components are in `GluLamb.Reconstruction.GH`, under
"6. Reconstruction" on the GluLamb tab.

## Beam from a mesh (`MeshBeam`)

This is for meshes from IFC, OBJ or STL. Port of `MeshBeamConverter` in
`extract_beam_from_mesh_rhino.py`.

1. Sharp edges: weld the mesh, keep edges whose faces meet at more than 60Â°, and join edges that
   meet within 15Â° into polylines.
2. Main axes: area-weighted PCA. Of the two minor axes, Y is the one the beam bends along.
3. Start: centres of sections across the main axis, then of sections normal to the spline through
   them, a few times over. Atypical sections are left out (see below). The start curve is then run
   out to the ends of the mesh, so the fit sees the whole beam: on a strongly curved beam the
   principal axis is much shorter than the beam. `InitialCentreline.Arc` is the Python version's arc
   through three section centres.
4. Sections: each is the convex hull of where its plane crosses the sharp edges, centred on the
   smallest rectangle around the hull. The rectangle's centre and sides stay put when a section
   loses a corner, e.g. near a skew end cut. The typical section is the median section along the
   middle of the start curve. A section counts as typical if its sides are within
   `TypicalTolerance` (5%) of it. Tapers, joints and end cuts make sections atypical.
5. Fit: L-BFGS over the spline's points. The error is, over at least 40 samples (2–98% along, and
   at least three per point), each sample's weight times its distance to the section's centre
   there. A sample's weight is how typical its section is, so atypical samples count little or not
   at all. The weights are found again after each of three rounds. There is also an optional,
   small penalty on changes of curvature (`Smoothness`, default 0.01). Raising it holds the curve
   to an even curvature, but it also fights beams whose curvature really does change.
6. The centreline is trimmed to the first and last typical samples, where the fit has something
   to go by, and run out to the ends of the mesh (`End`). By default the run-out continues the
   circle through the last stretch of the curve (`EndFitLength`, default 15% of the length),
   along the circle's own tangent, so a curved beam stays curved. `Straight` and Rhino's `Smooth`
   extension are also available.
7. Typical sections at 20 samples give the frames, the width and the height (the largest, measured
   both ways from the centreline).
8. Edge correction: the longest run of parallel sharp edges (as for Breps) usually runs through the
   ends untouched by joints. Each of those edges keeps one place in the section. That place is
   measured where all of the edges are crossed, and at every station the centreline goes where the
   edges crossed there put it. Near a skew end, where one edge has ended, the rest still place the
   centreline (`EdgeCorrection`, `Refinements`). A correction that would move the centreline by
   more than a quarter of the section is skipped.

The fitted points are kept at least 0.3 Ã— the section's depth apart, whatever `SampleSpacing` asks
for. Closer points see the same stretch of edges in their sections, so they add freedom for the
curve to wander but no information. There are at least three fit samples per point.

The section's Y-axis (height) is its direction of largest spread. Twist is taken from the typical
sections; past the last one it stays as it is there.

## Blank from a Brep (`BrepBlank`)

This is for detailed beam Breps (joints, cuts, drillings). Port of `parallel.py`.

1. The Brep's long edges are compared for parallelness (mean angle plus spread of distances,
   either way round) and clustered with average linkage. The cluster with the most length gives
   the centreline as the mean of its edges.
2. Sections of the Brep normal to it give the orientation. Each frame is turned to line up with the
   cluster's edges where it crosses them.
3. The Brep's points are mapped into the beam's curved space. Their bounding box gives the width,
   height and length, and the centreline is moved to the box's centre.
4. Steps 2 and 3 are repeated from the centred curve, which is corrected by where the cluster's
   edges sit in its sections.

The result is a `Beam` (centred, enclosing the Brep), a `Glulam` blank made from it, and the
`Placement` of the beam in the blank.

## Numerics

`Numerics/` holds what numpy and scipy did in the Python:

- `InterpolatingSpline` is scipy's `splprep(s=0, k=3)` and converts exactly to a `NurbsCurve`.
- `Lbfgs` is `minimize(method="L-BFGS-B")` with finite-difference gradients, evaluated in parallel.
- `Hull2d`, `Pca` and `AverageLinkage` (UPGMA, cut at a distance).

## Tests

`GluLamb.Reconstruction.Test` runs headless through Rhino.Inside. It reconstructs synthetic
straight, curved and twisted beams from their meshes (plain, and with a scarf, laps and a notch)
and from their Breps with joints cut in, then compares the results with the beams they came from.
It writes a `.3dm` of each case to `--out`. Set `RECON_DEBUG=1` to print the fit's points, weights
and deviations along the length.
