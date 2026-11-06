using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino;

namespace GluLamb
{
    public static class Globals
    {
        // Model settings. GluLamb doesn't read the Rhino document itself: these default to a
        // millimetre model, and a host (a Grasshopper assembly or Rhino plug-in) sets them from
        // its document with SetModel, or keeps them in step with the active one with TrackDocument.

        /// <summary>
        /// Units of the model. GluLamb's millimetre defaults (segment lengths, overlap tolerance) are scaled into them.
        /// </summary>
        public static UnitSystem Units { get; private set; } = UnitSystem.Millimeters;

        /// <summary>
        /// Absolute tolerance, in model units.
        /// </summary>
        public static double Tolerance = 0.001;

        /// <summary>
        /// The model's angle tolerance, in radians (for fillets and the like; AngleTolerance is GluLamb's own).
        /// </summary>
        public static double ModelAngleTolerance = RhinoMath.DefaultAngleTolerance;

        /// <summary>
        /// Smallest overlap or length that counts: 1 mm, in model units.
        /// </summary>
        public static double OverlapTolerance => FromMillimeters(1.0);

        /// <summary>
        /// A length in millimetres, in model units.
        /// </summary>
        public static double FromMillimeters(double length) => length * RhinoMath.UnitScale(UnitSystem.Millimeters, Units);

        public static void SetModel(double tolerance, double angleTolerance, UnitSystem units)
        {
            if (tolerance > 0) Tolerance = tolerance;
            if (angleTolerance > 0) ModelAngleTolerance = angleTolerance;
            Units = units;
        }

        /// <summary>
        /// Take the model settings from the active Rhino document now, and again whenever the
        /// active document or its properties change. For hosts to call once, when they load.
        /// </summary>
        public static void TrackDocument()
        {
            if (m_tracking) return;
            m_tracking = true;

            RhinoDoc.ActiveDocumentChanged += (s, e) => SetFromDocument(RhinoDoc.ActiveDoc);
            RhinoDoc.EndOpenDocument += (s, e) => SetFromDocument(RhinoDoc.ActiveDoc);
            RhinoDoc.NewDocument += (s, e) => SetFromDocument(RhinoDoc.ActiveDoc);
            RhinoDoc.DocumentPropertiesChanged += (s, e) => SetFromDocument(RhinoDoc.ActiveDoc);
            SetFromDocument(RhinoDoc.ActiveDoc);
        }

        static bool m_tracking = false;

        static void SetFromDocument(RhinoDoc doc)
        {
            if (doc != null)
                SetModel(doc.ModelAbsoluteTolerance, doc.ModelAngleToleranceRadians, doc.ModelUnitSystem);
        }

        public static double AngleTolerance = Rhino.RhinoMath.ToRadians(2.5);
        public static double CosineTolerance = 1e-6;

        public static double RadiusMultiplier = 200.0;  // This is the Eurocode 5 formula: lamella thickness cannot exceed 1/200th of the curvature radius.
        public static int CurvatureSamples = 100;       // Number of samples to samples curvature at.
        public static double RadiusTolerance = 0.00001; // For curvature calculations: curvature radius and lamella thickness cannot exceed this
        public static double MininumSegmentLength = 30.0; // Minimum length of discretized segment when creating glulam geometry (mm).
        public static int MinimumNumSegments = 25;
    }
}
