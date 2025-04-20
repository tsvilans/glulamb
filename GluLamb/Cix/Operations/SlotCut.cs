using GluLamb.Projects.HHDAC22;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace GluLamb.Cix.Operations
{
    /// <summary>
    /// Operation for machining a slot from the side, with
    /// a slot-cutting tool (thick saw blade).
    /// </summary>
    public class SlotCut : Operation
    {
        public Line Path
        {
            get { return _path; }
            set
            {
                var yaxis = -Vector3d.ZAxis;
                yaxis.Transform(Rhino.Geometry.Transform.Rotation(_angle, value.Direction, value.From));
                _plane = new Plane(value.From, value.Direction, yaxis);
                _path = value;
            }
        }
        public double Depth;
        public string OperationName = "SLOT_CUT";
        public double Angle 
        {
            get { return _angle; } 
            set
            {
                var yaxis = -Vector3d.ZAxis;
                yaxis.Transform(Rhino.Geometry.Transform.Rotation(value, Path.Direction, Path.From));
                _plane = new Plane(Path.From, Path.Direction, yaxis);
                _angle = value;
            } 
        }

        public Plane Plane
        {
            get { return _plane; }
        }

        private Line _path;
        private double _angle;
        private Plane _plane;

        public SlotCut(string name = "SlotCut")
        {
            Name = name;
            Path = Line.Unset;
            _plane = Plane.Unset;
        }

        public override object Clone()
        {
            return new SlotCut(Name)
            {
                Path = Path,
                Depth = Depth,
                OperationName = OperationName
            };
        }

        public override List<object> GetObjects()
        {
            return new List<object> { Path };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            cix.Add(string.Format("{0}{1}_{2}={3}", prefix, OperationName, Id, Enabled ? 1 : 0));
            if (!Enabled) return;

            cix.Add(string.Format("{0}{1}_{2}_PKT_1_X={3:0.###}", prefix, OperationName, Id, Path.From.X));
            cix.Add(string.Format("{0}{1}_{2}_PKT_1_Y={3:0.###}", prefix, OperationName, Id, Path.From.Y));
            cix.Add(string.Format("{0}{1}_{2}_PKT_1_Z={3:0.###}", prefix, OperationName, Id, -Path.From.Z));

            cix.Add(string.Format("{0}{1}_{2}_PKT_2_X={3:0.###}", prefix, OperationName, Id, Path.To.X));
            cix.Add(string.Format("{0}{1}_{2}_PKT_2_Y={3:0.###}", prefix, OperationName, Id, Path.To.Y));
            cix.Add(string.Format("{0}{1}_{2}_PKT_2_Z={3:0.###}", prefix, OperationName, Id, -Path.To.Z));

            cix.Add(string.Format("{0}{1}_{2}_DYBDE={3:0.###}", prefix, OperationName, Id, Depth));
            cix.Add(string.Format("{0}{1}_{2}_ALPHA={3:0.###}", prefix, OperationName, Id, Angle));
        }

        public override void Transform(Transform xform)
        {
            Path.Transform(xform);
        }

        public override bool SimilarTo(Operation op, double epsilon)
        {
            if (op is SlotCut other)
            {
                return true;
            }
            return false;
        }

        public static SlotCut FromCix(Dictionary<string, double> cix, string prefix = "", string id = "")
        {
            var name = $"{prefix}SLOT_CUT_{id}";

            if (!cix.ContainsKey(name) || cix[name] < 1)
                return null;

            var slotcut = new SlotCut(name);

            slotcut.Path = new Line(
                cix[$"{name}_PKT_1_X"],
                cix[$"{name}_PKT_1_Y"],
                -cix[$"{name}_PKT_1_Z"],
                cix[$"{name}_PKT_2_X"],
                cix[$"{name}_PKT_2_Y"],
                -cix[$"{name}_PKT_2_Z"]
                );

            slotcut.Depth = cix[$"{name}_DYBDE"];

            return slotcut;
        }

        public override BoundingBox Extents(Plane plane)
        {
            throw new NotImplementedException();
        }
    }
}
