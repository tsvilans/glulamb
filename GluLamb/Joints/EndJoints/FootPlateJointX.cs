using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Joints
{
    public class FootJointX : JointX
    {
        public double Added = 10.0;
        public double Depth = 10;
        public double Width = 10;
        public double Length = 10;


        public double ScrewEdgeOffsetX = 15;
        public double ScrewEdgeOffsetY = 20;
        public double ScrewInclination = 0;

        public List<object> debug = new List<object>();

        public FootJointX(JointX parent)
        {
            if (parent.Parts.Count != 1)
                throw new ArgumentException($"{GetType().Name} requires a 1-part joint.");

            Parts = parent.Parts;
            Position = parent.Position;
        }

        public override void Configure(Dictionary<string, double> values)
        {
            if (values.TryGetValue("Added", out double _added)) Added = _added;
            if (values.TryGetValue("Depth", out double _depth)) Depth = _depth;
            if (values.TryGetValue("Width", out double _width)) Width = _width;
            if (values.TryGetValue("Length", out double _length)) Length = _length;

            if (values.TryGetValue("ScrewEdgeOffsetX", out double _screwedgeoffsetx)) ScrewEdgeOffsetX = _screwedgeoffsetx;
            if (values.TryGetValue("ScrewEdgeOffsetY", out double _screwedgeoffsety)) ScrewEdgeOffsetY = _screwedgeoffsety;
            if (values.TryGetValue("ScrewInclination", out double _screwinclination)) ScrewInclination = _screwinclination;
        }

        public override List<object> GetDebugList()
        {
            return debug;
        }

        public override int Construct(Dictionary<int, Beam> beams)
        {
            for (int i = 0; i < Parts.Count; ++i)
            {
                Parts[i].Geometry.Clear();
            }

            var beam = beams[Parts[0].ElementIndex];

            var BeamPlane = beam.GetPlane(Parts[0].Parameter);
            // debug.Add(BeamPlane);

            var Beam2Foot = Position - BeamPlane.Origin;

            var axis = GluLamb.Utility.ClosestAxis(BeamPlane, Beam2Foot);
            axis.Unitize();

            double beamWidth = beam.Width, beamHeight = beam.Height;

            if (GluLamb.Utility.ClosestDimension2D(BeamPlane, Beam2Foot) != 0)
            {
                beamWidth = beam.Height;
                beamHeight = beam.Width;
            }

            var plane = new Plane(BeamPlane.Origin + axis * beamWidth * 0.5, BeamPlane.ZAxis, Vector3d.CrossProduct(BeamPlane.ZAxis, axis));
            // debug.Add(plane);

            var points = new Point3d[]{
                plane.PointAt(-Length * 0.5, -Width * 0.5, Depth),
                plane.PointAt(Length * 0.5, -Width * 0.5, Depth),
                plane.PointAt(Length * 0.5, Width * 0.5, Depth),
                plane.PointAt(-Length * 0.5, Width * 0.5, Depth),
            };

            var lowPoints = points.Select(x => x - plane.ZAxis * (Depth + Added)).ToArray();

            var slotFaces = new Brep[5];
            slotFaces[0] = Brep.CreateFromCornerPoints(points[0], points[1], lowPoints[1], lowPoints[0], 0.001);
            slotFaces[1] = Brep.CreateFromCornerPoints(points[1], points[2], lowPoints[2], lowPoints[1], 0.001);
            slotFaces[2] = Brep.CreateFromCornerPoints(points[2], points[3], lowPoints[3], lowPoints[2], 0.001);
            slotFaces[3] = Brep.CreateFromCornerPoints(points[3], points[0], lowPoints[0], lowPoints[3], 0.001);
            slotFaces[4] = Brep.CreateFromCornerPoints(points[0], points[1], points[2], points[3], 0.001);

            var slotJoined = Brep.JoinBreps(slotFaces, 0.001);
            if (slotJoined == null) throw new Exception($"{GetType().Name}: slotJoined failed.");

            Parts[0].Geometry.AddRange(slotJoined);

            var boltPocket = new Cylinder(
                new Circle(
                    plane,
                    22
                ),
                12 + Depth
            ).ToBrep(false, true);

            Parts[0].Geometry.Add(boltPocket);

            // Screws
            double ScrewInclinationOffset = Math.Tan(ScrewInclination) * (Added + Depth);
            for (int i = 0; i < 2; ++i)
            {
                for (int j = 0; j < 2; ++j)
                {
                    var sign = j > 0 ? 1 : -1;

                    var screwPlane = new Plane(plane.PointAt(
                        -Length * 0.5 + ScrewEdgeOffsetX + ScrewInclinationOffset + j * (Length - ScrewEdgeOffsetX * 2 - ScrewInclinationOffset * 2),
                        -Width * 0.5 + ScrewEdgeOffsetY + i * (Width - ScrewEdgeOffsetY * 2),
                        -Added
                        ),
                        plane.XAxis,
                        plane.YAxis);

                    screwPlane.Transform(Transform.Rotation(ScrewInclination * sign, screwPlane.YAxis, screwPlane.Origin));

                    var screw = new Cylinder(
                        new Circle(
                            screwPlane,
                            3
                        ),
                        50 + Depth + Added
                    ).ToBrep(true, true);

                    Parts[0].Geometry.Add(screw);
                    debug.Add(screw);
                }
            }

            return 0;
        }
    }
}
