using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb
{
    public static class Globals
    {
        public static double Tolerance = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
        public static double OverlapTolerance = 0.001 * Rhino.RhinoMath.UnitScale(Rhino.RhinoDoc.ActiveDoc.ModelUnitSystem, Rhino.UnitSystem.Meters);
        public static double AngleTolerance = Rhino.RhinoMath.ToRadians(2.5);
        public static double CosineTolerance = 1e-6;

        public static double RadiusMultiplier = 200.0;  // This is the Eurocode 5 formula: lamella thickness cannot exceed 1/200th of the curvature radius.
        public static int CurvatureSamples = 100;       // Number of samples to samples curvature at.
        public static double RadiusTolerance = 0.00001; // For curvature calculations: curvature radius and lamella thickness cannot exceed this
        public static double MininumSegmentLength = 30.0; // Minimum length of discretized segment when creating glulam geometry (mm).
        public static int MinimumNumSegments = 25;
    }
}
