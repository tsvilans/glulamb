using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;

namespace GluLamb.Features
{
    /// <summary>
    /// Slot or pocket for a plate or tenon, described by its cutting volume. Plane is the slot
    /// plane (e.g. the plate plane). BTLx: Slot.
    /// </summary>
    public class Slot : Feature
    {
        public override string ProcessingName => "Slot";

        public double Thickness = double.NaN;
        public double Depth = double.NaN;

        /// <summary>
        /// Cutting geometry, normally a closed brep.
        /// </summary>
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
            var slot = CopyBaseTo(new Slot());
            slot.Thickness = Thickness;
            slot.Depth = Depth;
            slot.Cutters = Cutters.Select(x => x.DuplicateBrep()).ToList();
            return slot;
        }
    }
}
