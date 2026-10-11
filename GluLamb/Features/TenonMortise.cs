using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;

namespace GluLamb.Features
{
    /// <summary>
    /// Tenon on the end of a beam, described by its cutting surface: the shoulder with the tenon
    /// standing out of it. Brep.Cut splits the beam with it and keeps the largest piece. Plane is
    /// the shoulder plane, with Z towards the tenon. BTLx: Tenon.
    /// </summary>
    public class Tenon : Feature
    {
        public override string ProcessingName => "Tenon";

        public double Length = double.NaN;
        public double Width = double.NaN;
        public double Thickness = double.NaN;

        public List<Brep> Cutters = new List<Brep>();

        public override IList<Brep> GetCutters(Beam beam, double tolerance) => Cutters.Select(PrepareCutter).ToList();

        public override void Transform(Transform xform)
        {
            base.Transform(xform);
            foreach (var cutter in Cutters)
                cutter.Transform(xform);
        }

        public override Feature Duplicate()
        {
            var tenon = CopyBaseTo(new Tenon());
            tenon.Length = Length;
            tenon.Width = Width;
            tenon.Thickness = Thickness;
            tenon.Cutters = Cutters.Select(x => x.DuplicateBrep()).ToList();
            return tenon;
        }
    }

    /// <summary>
    /// Mortise in the side of a beam, described by its (closed) cutting volume. Plane is on the
    /// beam face, with Z out of the beam. BTLx: Mortise.
    /// </summary>
    public class Mortise : Feature
    {
        public override string ProcessingName => "Mortise";

        public double Depth = double.NaN;
        public double Width = double.NaN;
        public double Thickness = double.NaN;

        public List<Brep> Cutters = new List<Brep>();

        public override IList<Brep> GetCutters(Beam beam, double tolerance) => Cutters.Select(PrepareCutter).ToList();

        public override void Transform(Transform xform)
        {
            base.Transform(xform);
            foreach (var cutter in Cutters)
                cutter.Transform(xform);
        }

        public override Feature Duplicate()
        {
            var mortise = CopyBaseTo(new Mortise());
            mortise.Depth = Depth;
            mortise.Width = Width;
            mortise.Thickness = Thickness;
            mortise.Cutters = Cutters.Select(x => x.DuplicateBrep()).ToList();
            return mortise;
        }
    }
}
