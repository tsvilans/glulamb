# GluLamb
A toolkit for the 3d modelling of free-form timber structures.

![GluLamb](https://tomsvilans.com/glulamb_banner.png)

# TLDR
This is a software library / plug-in that enables fast modelling of curvy and twisty glulam beams in Rhino3D. It is developed in C# using the RhinoCommon API and exposed as a set of Grasshopper components.

# Description
A constrained glulam blank model and associated models for the modelling of free-form glulam components. The focus is on providing a lightweight but informative method for quickly modelling complex glulam structures while respecting fabrication constraints such as lamella sizing in relation to curvature. The model helps calculate material specifications, creates driving geometry for cutting joints, offers different types of glulam blanks (straight, single-curved, and double-curved), and several options for how the glulam cross-section is oriented along its centreline curve. Additional models link individual glulam components with graph-based methods of managing entire structures and connections.

# Projects
| Project | What it is |
| --- | --- |
| `GluLamb` | The core library: beams, glulams and blanks, joints, structure, CIX and BTLx output, and shared numerics (`GluLamb.Numerics`). |
| `GluLamb.GH` | The main Grasshopper plug-in. |
| `GluLamb.Reconstruction`, `GluLamb.Reconstruction.GH` | Beams and blanks reconstructed from meshes and Breps (scans, IFC, detailed models), remeshing, principal axes. See its README. |
| `GluLamb.Rhino` | The Rhino plug-in (beam objects, display, panels). |
| `Cix.GH` | CIX file preview. |
| `G2PComponents` | Design-to-production commands. Needs `D2P_Core.dll` (from the D2P repository) in `bin/`. |
| `GluLamb.Projects` | Code written for specific projects, kept out of the core. Built on demand. |
| `GluLamb.Reconstruction.Test`, `GluLamb.ClusterTest`, `GluLamb.Gallery` | Headless test apps (Rhino.Inside), under "tests" in the solution. |

# Building
Everything targets .NET 7 (Rhino 8). `dotnet build GluLamb.sln` builds the libraries and plug-ins into `bin/`; close Rhino first, as it locks the files there. The test apps are left out of the solution build, because each builds its own copy of GluLamb (so they can run while Rhino is open): run them with `./run-tests.ps1` (or `./run-tests.ps1 reconstruction` for one).

# Acknowledgements
This software has been developed at the [Centre for IT and Architecture (CITA)](https://kadk.dk/en/CITA) in Copenhagen, Denmark, as part of the EU-funded [Innochain training network](http://innochain.net/). 

This project has received funding from the European Union’s Horizon 2020 research and innovation programme under the Marie Sklodowska-Curie grant agreement No 642877.

# Credits
GluLamb is written and maintained by [Tom Svilans](http://tomsvilans.com).
