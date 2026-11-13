/*
 * GluLamb
 * A constrained glulam modelling toolkit.
 * Copyright 2026 Tom Svilans
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 */

using Rhino;

namespace GluLamb.Reconstruction
{
    /// <summary>
    /// The thresholds that decide how reconstruction behaves, in one place. Fractions are of the
    /// beam's section (its smaller side) unless they say otherwise; lengths are in model units.
    /// </summary>
    public class ReconstructionTolerances
    {
        // Typical sections

        /// <summary>A fit sample whose section weighs less than this (by how typical it is) is
        /// left out of the fit altogether.</summary>
        public double MinFitWeight = 0.25;

        /// <summary>A section whose weight is at least this counts as typical: it sets the frames,
        /// width and height, and the ends of the fitted part.</summary>
        public double TypicalWeight = 0.5;

        /// <summary>Range (0 to 1 along a curve) of the sections that give the typical section.</summary>
        public double ReferenceStart = 0.1, ReferenceEnd = 0.9;

        // Fit

        /// <summary>Smallest spacing of the fitted points, as a fraction of the section's depth.
        /// Closer points see the same stretch of edges, so they add freedom but no information.</summary>
        public double MinSpacingFactor = 0.3;

        /// <summary>Fraction of the beam at each end that the initial walk keeps away from.</summary>
        public double WalkMargin = 0.1;

        /// <summary>Fit error per unit of distance from a section's centre, and per sample that has
        /// no section.</summary>
        public double ErrorPerMillimetre = 100;
        public double MissingSectionError = 1000;

        /// <summary>LeastSquares fit: stop when no point moves by more than this.</summary>
        public double LeastSquaresConvergence = Globals.FromMillimeters(0.01);

        /// <summary>LeastSquares fit: how strongly each point is held where it was, relative to
        /// the pull of its sections. Keeps points without sections in place.</summary>
        public double LeastSquaresDamping = 1e-3;

        // Edges

        /// <summary>Residual allowed when fitting edge crossings to their usual places: this
        /// fraction of the section plus Residual.</summary>
        public double ResidualFraction = 0.02;
        public double Residual = Globals.FromMillimeters(1);

        /// <summary>A tracking step, or a correction, that moves the centreline by more than this
        /// fraction of the section is a mistake, not a move along the beam.</summary>
        public double JumpFraction = 0.25;

        /// <summary>Spacing of the stations where the edges are cut (clamped to MinStations and
        /// MaxStations).</summary>
        public double StationSpacing = Globals.FromMillimeters(100);
        public int MinStations = 10, MaxStations = 200;

        /// <summary>Tracking step along the edges: this fraction of the section, clamped.</summary>
        public double StepFraction = 0.25;
        public double MinStep = Globals.FromMillimeters(10), MaxStep = Globals.FromMillimeters(100);

        /// <summary>How far edge tracking may go past the fitted part, as a fraction of its length.</summary>
        public double MaxTrackFraction = 0.5;

        /// <summary>Largest turn (radians) of a section's frame to line it up with the edges.</summary>
        public double AlignAngle = RhinoMath.ToRadians(10);

        /// <summary>Edges within this angle (radians) count as along or square to a rectangle's
        /// side when lining a rectangle up with a section's sides.</summary>
        public double RectangleAngle = RhinoMath.ToRadians(2);

        // Ends

        /// <summary>How far a curve is run out past its ends, as a fraction of its length, before
        /// it is trimmed to the extent of the mesh.</summary>
        public double RunOutFraction = 0.25;

        /// <summary>The extended probe curve for a Brep's envelope, as a fraction of its length.</summary>
        public double EnvelopeRunOutFraction = 0.1;

        /// <summary>Meshing of a Brep for its centreline and envelope: longest mesh edge, also at
        /// most the Brep's diagonal over MeshDivisions.</summary>
        public double MeshEdgeLength = Globals.FromMillimeters(150);
        public double MeshDivisions = 50;

        public double ResidualTolerance(double section) => ResidualFraction * section + Residual;

        public double TrackingStep(double section) => Math.Clamp(StepFraction * section, MinStep, MaxStep);

        public int StationCount(double length) => Math.Clamp((int)(length / StationSpacing) + 1, MinStations, MaxStations);
    }
}
