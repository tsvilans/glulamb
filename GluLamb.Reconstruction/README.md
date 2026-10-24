# GluLamb.Reconstruction

Beams and blanks reconstructed from other geometry. It is kept apart from GluLamb so that the core
library doesn't carry it. Its Grasshopper components are in `GluLamb.Reconstruction.GH`, under
"6. Reconstruction" on the GluLamb tab.

## Beam from a mesh (`MeshBeam`)

This is for meshes from IFC, OBJ or STL. Port of `MeshBeamConverter` in
`extract_beam_from_mesh_rhino.py`.

1. Sharp edges: weld the mesh, keep edges whose faces meet at more than 60°, and join edges that
   meet within 15° into polylines.
2. Main axes: area-weighted PCA. Of the two minor axes, Y is the one the beam bends along.
3. Start: centres of sections across the main axis, then of sections normal to the spline through
   them, a few times over. Atypical sections are left out (see below), and the ends are run out
   smoothly from the rest. `InitialCentreline.Arc` is the Python version's arc through three
   section centres.
4. Typical section: the median size of the sections along the middle of the start curve. A
   section counts as typical if its extents are within `TypicalTolerance` (5%) of it. Tapers,
   joints and end cuts make sections atypical.
5. Fit: L-BFGS over the spline's points. The error is, over 40 samples (2–98% along), each
   sample's weight times its distance to the centre of the section hull there. A sample's weight is
   how typical its section is, so atypical samples count little or not at all. The error also has a
   penalty on changes of curvature (`Smoothness`), which is zero for straight lines and arcs. Near
   an end, where there is little to fit to, this carries the rest of the beam's shape through. The
   weights are found again after each of three rounds.
6. Typical sections at 20 samples give the frames, the width and the height (the largest, measured
   both ways from the centreline). The centreline is run out to the ends of the mesh.
7. Edge correction: the longest run of parallel sharp edges (as for Breps) usually runs through the
   ends untouched by joints. The centreline is moved by where those edges sit in its sections
   (`EdgeCorrection`, `Refinements`), measured in frames lined up with the edges themselves.

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
