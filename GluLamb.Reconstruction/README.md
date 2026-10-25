# GluLamb.Reconstruction

Beams and blanks reconstructed from other geometry. Its Grasshopper components are in `GluLamb.Reconstruction.GH`, under
"6. Reconstruction" on the GluLamb tab.

## Beam from a mesh (`MeshBeam`)

This is for meshes from IFC, OBJ or STL. Port of `MeshBeamConverter` in
`extract_beam_from_mesh_rhino.py`.

1. Sharp edges: weld the mesh, keep edges whose face normals differ by more than 20° (so bevels and
   chamfers count), and join edges that meet within 15° into polylines.
2. Main axes: area-weighted PCA. Of the two minor axes, Y is the one the beam bends along.
3. Start: centres of sections across the main axis, then of sections normal to the spline through
   them, a few times over. Atypical sections are left out (see below). The start curve is then run
   out to the ends of the mesh, so the fit sees the whole beam: on a strongly curved beam the
   principal axis is much shorter than the beam. `InitialCentreline.Arc` is the Python version's arc
   through three section centres.
4. Sections: each is the convex hull of where its plane crosses the sharp edges, centred on the
   rectangle around the hull that lines up with its straight sides. That is the direction along or
   square to which the most hull edge length runs, so a profile with bevels or a groove keeps its
   rectangle's direction, where the smallest rectangle could line up with a bevel. The rectangle's
   centre and sides stay put when a section loses a corner, e.g. near a skew end cut. The
   section's frames are lined up with the parallel edges only within 10°. The typical section is the median section along the
   middle of the start curve. A section counts as typical if its sides are within
   `TypicalTolerance` (5%) of it. Tapers, joints and end cuts make sections atypical.
5. Fit: L-BFGS over the spline's points. The error is, over at least 40 samples (2–98% along, and
   at least three per point), each sample's weight times its distance to the section's centre
   there. A sample's weight is how typical its section is, so atypical samples count little or not
   at all. The weights are found again after each of three rounds. An optional penalty on changes
   of curvature (`Smoothness`) is off by default. It holds the curve to an even curvature, so it
   only suits beams that have one.
6. The centreline is trimmed to the first and last typical samples, where the sections have
   something to say.
7. Edge tracking: the longest run of parallel sharp edges (as for Breps) is the main set. The other
   long edges that keep a steady place in the section (e.g. the edges a lap leaves along the beam)
   help. Each edge's place in the section is measured along the trimmed centreline. From a little
   inside each end, the centreline is then followed out one step at a time. At each step, the
   crossed edges are fitted to their places by a turn and a move (a 2D rigid fit, the main edges
   alone where two or more are there, an edge that doesn't fit dropped). The centreline goes where
   that fit puts it. This stops where fewer than two edges are left, or where they jump. Nothing is
   assumed about the curvature: the edges carry it right up to a skew end face.
8. The last stretch, past the last edge crossing, is run out to the ends of the mesh (`End`). By
   default it continues the curvature and its rate of change measured over the end of the curve
   (`EndFitLength`, default 15% of its length), with the change limited. `Arc` (constant curvature),
   `Straight` and Rhino's `Smooth` extension are also available.
9. Typical sections at 20 samples give the frames, the width and the height (the largest, measured
   both ways from the centreline).
10. Edge correction: at stations all along, the centreline goes where the crossed edges put it, in
    the same way (`EdgeCorrection`, `Refinements`). A correction that would move it by more than a
    quarter of the section is skipped.

The fitted points are kept at least 0.3 × the section's depth apart, whatever `SampleSpacing` asks
for. Closer points see the same stretch of edges in their sections, so they add freedom for the
curve to wander but no information. There are at least three fit samples per point.

The section's Y-axis (height) is its direction of largest spread. Twist is taken from the typical
sections; past the last one it stays as it is there.

## Blank from a Brep (`BrepBlank`)

This is for detailed beam Breps (joints, cuts, drillings, shaped profiles). It is based on
`parallel.py`.

1. The Brep's long edges are compared for parallelness (mean angle plus spread of distances,
   either way round) and clustered with average linkage. The cluster with the most length is the
   longest run of parallel edges.
2. Centreline: MeshBeam on a mesh of the Brep (`UseMeshCentreline`, the default), which follows
   the sections and tracks the edges out to the ends.
3. Sections of the Brep normal to that centreline give the orientation.
4. The Brep's points are mapped into the beam's curved space. Their bounding box gives the width,
   height and length, and the centreline is moved to the box's centre.

If the mesh centreline fails, or is turned off, the older route is used: the longest edge of the
cluster is the starting centreline, and steps 3 and 4 are repeated from the centred curve, which
is corrected by where the cluster's edges sit in its sections.

The result is a `Beam` (centred, enclosing the Brep), a `Glulam` blank made from it, and the
`Placement` of the beam in the blank.

## Remeshing (`DualCon`)

Dual contouring (Ju et al., 2002, as in Blender's remesher) turns noisy or non-manifold scan meshes into clean quad meshes, through the native `dualcon.dll`, which has to sit next to `GluLamb.Reconstruction.dll`. The Remesh component wraps it. Moved from GluLamb.Raw as it was; see docs/TODO.md.

## Grasshopper components

Beam From Mesh, Blank From Brep, Remesh, Find Plane and BasePlane, under "6. Reconstruction". Find Plane, BasePlane and Remesh keep the GUIDs they had in GluLamb.Raw.GH. Find Plane gives the principal axes and the oriented bounding box of any mesh. Beam From Mesh replaces Raw's Find Centreline (Centreline, Width, Height outputs).

## Numerics

The numerics are in the core library, in `GluLamb/Numerics` (namespace `GluLamb.Numerics`), where other parts of GluLamb use them too. They hold what numpy and scipy did in the Python:

- `InterpolatingSpline` is scipy's `splprep(s=0, k=3)` and converts exactly to a `NurbsCurve`.
- `Lbfgs` is `minimize(method="L-BFGS-B")` with finite-difference gradients, evaluated in parallel.
- `Hull2d`, `Pca` and `AverageLinkage` (UPGMA, cut at a distance).
- `SymmetricEigen` (cyclic Jacobi), also used by Cluster Beams, `Strain` and `VectorListOrientation`.

Closest-point queries on Rhino curves run one at a time: run in parallel on one curve, they gave
results that changed from run to run.

## Tests

`GluLamb.Reconstruction.Test` runs headless through Rhino.Inside. It reconstructs synthetic beams
and compares the results with the beams they came from:
- from their meshes: straight, arched, tightly curved, twisted and double-curved, an Euler spiral,
  and Tom's example curve, plain and with a scarf, laps, a notch and skew end cuts;
- from their Breps, with joints cut in.

It writes a `.3dm` of each case to `--out`. `--file model.3dm` reconstructs every mesh and Brep in a
file and compares the results with any curve in it. Set `RECON_DEBUG=1` to print the fit's
points, weights and deviations along the length.
