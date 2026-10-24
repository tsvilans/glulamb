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
   them, a few times over. `InitialCentreline.Arc` is the Python version's arc through three
   section centres.
4. Fit: L-BFGS over the spline's points. The error is the distance from each of 30 samples
   (10–90% along) to the centre of the section hull at that sample. A second pass fits the two end
   points on their own and is kept if the error over all samples goes down.
5. Sections at 10 samples (10–90%) give the frames, the width and the height (the largest, measured
   both ways from the centreline). The centreline is run out to the ends of the mesh.

The section's Y-axis (height) is its direction of largest spread. Twist is taken from the sections
between 10% and 90%; past them it stays as it is at the last section.

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
straight, curved and twisted beams from their meshes, and from their Breps with joints cut in, then
compares the results with the beams they came from. It writes a `.3dm` of each case to `--out`.
