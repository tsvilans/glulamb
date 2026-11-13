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
    /// The thresholds of reconstruction. Fractions are of the section's smaller side, unless
    /// stated. Lengths are in model units.
    /// </summary>
    public class ReconstructionTolerances
    {
        // Typical sections

        /// <summary>Fit samples with a lower weight are removed from the fit.</summary>
        public double MinFitWeight = 0.25;

        /// <summary>Sections with at least this weight are typical. They set the frames, width,
        /// height and the ends of the fitted part.</summary>
        public double TypicalWeight = 0.5;

        /// <summary>Range (0 to 1) of the sections that give the typical section.</summary>
        public double ReferenceStart = 0.1, ReferenceEnd = 0.9;

        // Fit

        /// <summary>Smallest spacing of the fitted points, as a fraction of the section depth.
        /// Closer points add freedom but no information.</summary>
        public double MinSpacingFactor = 0.3;

        /// <summary>Fraction of the beam at each end that the initial walk stays away from.</summary>
        public double WalkMargin = 0.1;

        /// <summary>Fit error per millimetre from a section centre, and per sample without a section.</summary>
        public double ErrorPerMillimetre = 100;
        public double MissingSectionError = 1000;

        /// <summary>LeastSquares: stop when no point moves more than this.</summary>
        public double LeastSquaresConvergence = Globals.FromMillimeters(0.01);

        /// <summary>LeastSquares: how strongly each point is held in place, relative to the pull of
        /// its sections. Keeps points without sections in place.</summary>
        public double LeastSquaresDamping = 1e-3;

        // Edges

        /// <summary>Allowed residual of the edge fit: this fraction of the section plus Residual.</summary>
        public double ResidualFraction = 0.02;
        public double Residual = Globals.FromMillimeters(1);

        /// <summary>A tracking step or correction that moves the centreline more than this
        /// fraction of the section is an error.</summary>
        public double JumpFraction = 0.25;

        /// <summary>Spacing of the stations where the edges are cut. The count is clamped to
        /// MinStations and MaxStations.</summary>
        public double StationSpacing = Globals.FromMillimeters(100);
        public int MinStations = 10, MaxStations = 200;

        /// <summary>Tracking step: this fraction of the section, clamped to MinStep and MaxStep.</summary>
        public double StepFraction = 0.25;
        public double MinStep = Globals.FromMillimeters(10), MaxStep = Globals.FromMillimeters(100);

        /// <summary>Maximum tracking distance past the fitted part, as a fraction of its length.</summary>
        public double MaxTrackFraction = 0.5;

        /// <summary>Largest turn (radians) that aligns a section frame to the edges.</summary>
        public double AlignAngle = RhinoMath.ToRadians(10);

        /// <summary>Edges within this angle (radians) of a rectangle side are along or square to it.</summary>
        public double RectangleAngle = RhinoMath.ToRadians(2);

        // Ends

        /// <summary>Extension of a curve past its ends, as a fraction of its length, before it is
        /// trimmed to the mesh.</summary>
        public double RunOutFraction = 0.25;

        /// <summary>Extension of the probe curve for a Brep's envelope, as a fraction of its length.</summary>
        public double EnvelopeRunOutFraction = 0.1;

        /// <summary>Brep mesh: maximum edge length. Also at most the Brep's diagonal / MeshDivisions.</summary>
        public double MeshEdgeLength = Globals.FromMillimeters(150);
        public double MeshDivisions = 50;

        public double ResidualTolerance(double section) => ResidualFraction * section + Residual;

        public double TrackingStep(double section) => Math.Clamp(StepFraction * section, MinStep, MaxStep);

        public int StationCount(double length) => Math.Clamp((int)(length / StationSpacing) + 1, MinStations, MaxStations);
    }
}
