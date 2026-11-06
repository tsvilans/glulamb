# GluLamb.ClusterTest

Checks for Cluster Beams (`GluLamb/Blanks/BeamClustering.cs`). Families of beams (arcs, 3D
splines, a helix, straight beams) are copied with random rigid transforms, reversal, a half-turn
roll and small perturbations, and must cluster as expected:

- full-length matches only, and with a length tolerance (shorter beams cut from a longer blank),
- length bands: beams around 2000 +-200 and 3000 +-100 with a length tolerance of 500 give blanks
  of 2200 and 3100, and evenly spread lengths split into bands rather than chaining,
- sections: sections within the tolerance share a blank, others don't,
- a mirrored double-curved beam never clusters with the original,
- each blank, moved by a member's transform, lands on that member (extensions included).

It ends with timings for 1000 beams. The exit code is 1 if any check fails.

## Running

Needs Rhino 8 installed (it runs Rhino headless through Rhino.Inside). From the repo:

```
dotnet run -c Release --project GluLamb.ClusterTest
```

The project builds GluLamb into its own `obj/GluLamb` folder, not the shared `bin` plug-in folder.
It isn't in the solution.
