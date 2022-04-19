using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino;
using Rhino.Geometry;

namespace GluLamb.Projects.HHDAC22
{
    public interface ITransformable
    {
        void Transform(Transform xform);
    }

    public interface IHasGeometry
    {
        List<object> GetObjects();
    }

    public interface ICix
    {
        void ToCix(List<string> cix, string prefix = "");
    }

    public class Drill2d : ITransformable, IHasGeometry
    {
        public Point3d Position;
        public double Diameter;
        public double Depth;

        public Drill2d(Point3d position, double diameter=0, double depth=0)
        {
            Position = position;
            Diameter = diameter;
            Depth = depth;
        }

        public List<object> GetObjects()
        {
            return new List<object> { Position };
        }

        public void Transform(Transform xform)
        {
            Position.Transform(xform);
        }
    }

    public abstract class Operation : ITransformable, ICix, IHasGeometry
    {
        public string Name = "Operation";
        public int Id = 0;

        public abstract void ToCix(List<string> cix, string prefix="");
        public abstract void Transform(Transform xform);

        public abstract List<object> GetObjects();
    }

    public class DrillGroup : Operation
    {
        public Plane Plane;
        public List<Drill2d> Drillings;

        public DrillGroup(string name="DrillGroup")
        {
            Name = name;
            Drillings = new List<Drill2d>();
            Plane = Plane.Unset;
        }

        public override List<object> GetObjects()
        {
            var things = new List<object> { Plane };
            for (int i = 0; i < Drillings.Count; ++i)
            {
                things.AddRange(Drillings[i].GetObjects());
            }
            return things;
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            cix.Add(string.Format("{0}HUL_{1}=1", prefix, Id));

            // Sort out plane transformation here
            var normal = Plane.ZAxis;
            var xaxis = Vector3d.CrossProduct(normal, Vector3d.ZAxis);
            var yaxis = Vector3d.CrossProduct(normal, xaxis);
            var origin = Plane.Origin;
            var xpoint = origin + xaxis * 100;

            var sign = Vector3d.ZAxis * Plane.ZAxis > 0 ? 1 : -1;
            //var angle = Vector3d.VectorAngle(-Vector3d.ZAxis, Plane.YAxis) * sign;
            var angle = Vector3d.VectorAngle(-Vector3d.ZAxis, yaxis) * sign;

            var plane = new Plane(origin, xaxis, yaxis);

            cix.Add(string.Format("{0}HUL_{1}_PL_PKT_1_X={2:0.###}", prefix, Id, origin.X));
            cix.Add(string.Format("{0}HUL_{1}_PL_PKT_1_Y={2:0.###}", prefix, Id, origin.Y));
            cix.Add(string.Format("{0}HUL_{1}_PL_PKT_1_Z={2:0.###}", prefix, Id, origin.Z));

            cix.Add(string.Format("{0}HUL_{1}_PL_PKT_2_X={2:0.###}", prefix, Id, xpoint.X));
            cix.Add(string.Format("{0}HUL_{1}_PL_PKT_2_Y={2:0.###}", prefix, Id, xpoint.Y));
            cix.Add(string.Format("{0}HUL_{1}_PL_PKT_2_Z={2:0.###}", prefix, Id, xpoint.Z));
            cix.Add(string.Format("{0}HUL_{1}_PL_ALFA={2:0.###}", prefix, Id, RhinoMath.ToDegrees(angle)));

            cix.Add(string.Format("{0}HUL_{1}_N={2}", prefix, Id, Drillings.Count));

            for (int i = 0; i < Drillings.Count; ++i)
            {
                var d = Drillings[i];
                Point3d pp;
                plane.RemapToPlaneSpace(d.Position, out pp);
                cix.Add(string.Format("\t(Drill_{0}_{1})", Id, i + 1));
                cix.Add(string.Format("{0}HUL_{1}_{2}_X={3:0.###}", prefix, Id, i + 1, pp.X));
                cix.Add(string.Format("{0}HUL_{1}_{2}_Y={3:0.###}", prefix, Id, i + 1, pp.Y));
                cix.Add(string.Format("{0}HUL_{1}_{2}_DIA={3:0.###}", prefix, Id, i + 1, d.Diameter));
                cix.Add(string.Format("{0}HUL_{1}_{2}_DYBDE={3:0.###}", prefix, Id, i + 1, d.Depth));
            }

        }

        public override void Transform(Transform xform)
        {
            Plane.Transform(xform);
            for (int i = 0; i < Drillings.Count; ++i)
                Drillings[i].Transform(xform);
        }
    }

    public class EndCut : Operation
    {
        public Plane Plane;
        public Line CutLine;
        public EndCut(string name = "EndCut")
        {
            Name = name;
            Plane = Plane.Unset;
            CutLine = Line.Unset;
        }

        public override List<object> GetObjects()
        {
            return new List<object> { Plane, CutLine };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            cix.Add(string.Format("{0}CUT_{1}=1", prefix, Id));

            // Sort out plane transformation here
            // var normal = Plane.ZAxis;
            //var xaxis = Vector3d.CrossProduct(normal, Vector3d.ZAxis);
            //var yaxis = Vector3d.CrossProduct(normal, xaxis);
            //var origin = Plane.Origin;
            //var xpoint = origin + xaxis * 100;

            var sign = Vector3d.ZAxis * Plane.ZAxis > 0 ? 1 : -1;
            var angle = Vector3d.VectorAngle(Vector3d.ZAxis, Plane.YAxis) * sign;

            //var plane = new Plane(origin, xaxis, yaxis);

            cix.Add(string.Format("{0}CUT_{1}_LINE_PKT_1_X={2:0.###}", prefix, Id, CutLine.From.X));
            cix.Add(string.Format("{0}CUT_{1}_LINE_PKT_1_Y={2:0.###}", prefix, Id, CutLine.From.Y));
            cix.Add(string.Format("{0}CUT_{1}_LINE_PKT_1_Z={2:0.###}", prefix, Id, CutLine.From.Z));

            cix.Add(string.Format("{0}CUT_{1}_LINE_PKT_2_X={2:0.###}", prefix, Id, CutLine.To.X));
            cix.Add(string.Format("{0}CUT_{1}_LINE_PKT_2_Y={2:0.###}", prefix, Id, CutLine.To.Y));
            cix.Add(string.Format("{0}CUT_{1}_LINE_PKT_2_Z={2:0.###}", prefix, Id, CutLine.To.Z));

            cix.Add(string.Format("{0}CUT_{1}_ALFA={2:0.###}", prefix, Id, RhinoMath.ToDegrees(angle)));
        }

        public override void Transform(Transform xform)
        {
            Plane.Transform(xform);
            CutLine.Transform(xform);
        }
    }

    public class CrossJointCutout : Operation
    {
        public Polyline Outline;
        public Plane Plane;
        public Line[] SideLines;
        public double Depth;
        public double Alpha;
        public Line MaxSpan;

        public CrossJointCutout(string name = "CrossCutout")
        {
            Name = name;
            SideLines = new Line[4];
            Plane = Plane.Unset;
            Outline = new Polyline();
            MaxSpan = Line.Unset;
        }

        public override List<object> GetObjects()
        {
            return new List<object> { Plane, Outline, MaxSpan, SideLines[0], SideLines[1] };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            // Write outline
            for (int i = 0; i < Outline.Count; ++i)
            {
                cix.Add(string.Format("{0}_HAK_{1}_PKT_{2}_X={3:0.###}", prefix, Id, i + 1, Outline[i].X));
                cix.Add(string.Format("{0}_HAK_{1}_PKT_{2}_Y={3:0.###}", prefix, Id, i + 1, Outline[i].Y));
            }

            // Write plane
            cix.Add(string.Format("{0}HAK_{1}_PL_PKT_1_X={2:0.###}", prefix, Id, MaxSpan.From.X));
            cix.Add(string.Format("{0}HAK_{1}_PL_PKT_1_Y={2:0.###}", prefix, Id, MaxSpan.From.Y));
            //cix.Add(string.Format("{0}HAK_{1}_PL_PKT_1_Z={2:0.###}", prefix, Id, MaxSpan.From.Z));

            cix.Add(string.Format("{0}HAK_{1}_PL_PKT_2_X={2:0.###}", prefix, Id, MaxSpan.To.X));
            cix.Add(string.Format("{0}HAK_{1}_PL_PKT_2_Y={2:0.###}", prefix, Id, MaxSpan.To.Y));
            //cix.Add(string.Format("{0}HAK_{1}_PL_PKT_2_Z={2:0.###}", prefix, Id, MaxSpan.To.Z));

            for (int i = 0; i < SideLines.Length; ++i)
            {
                cix.Add(string.Format("{0}HAK_LINE_{1}_PKT_1_X={2:0.###}", prefix, i + 1, SideLines[i].From.X));
                cix.Add(string.Format("{0}HAK_LINE_{1}_PKT_1_Y={2:0.###}", prefix, i + 1, SideLines[i].From.Y));
                cix.Add(string.Format("{0}HAK_LINE_{1}_PKT_2_X={2:0.###}", prefix, i + 1, SideLines[i].To.X));
                cix.Add(string.Format("{0}HAK_LINE_{1}_PKT_2_Y={2:0.###}", prefix, i + 1, SideLines[i].To.Y));
            }

            cix.Add(string.Format("{0}HAK_DYBDE={1:0.###}", prefix, Depth));
            cix.Add(string.Format("{0}HAK_ALFA={1:0.###}", prefix, Alpha));
        }

        public override void Transform(Transform xform)
        {
            Plane.Transform(xform);
            Outline.Transform(xform);
            if (MaxSpan.IsValid)
                MaxSpan.Transform(xform);

            for (int i = 2; i < SideLines.Length; ++i) // Skip first 2 lines because they are in local space
            {
                SideLines[i].Transform(xform);
            }
        }
    }

    public class CleanCut : Operation
    {
        public Line CutLine;

        public CleanCut(string name="CleanCut")
        {
            Name = name;
            CutLine = Line.Unset;

        }

        public override List<object> GetObjects()
        {
            return new List<object> { CutLine };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            cix.Add(string.Format("{0}RENSKAER_PKT_1_X={1:0.###}", prefix, CutLine.From.X));
            cix.Add(string.Format("{0}RENSKAER_PKT_1_Y={1:0.###}", prefix, CutLine.From.Y));

            cix.Add(string.Format("{0}RENSKAER_PKT_2_X={1:0.###}", prefix, CutLine.To.X));
            cix.Add(string.Format("{0}RENSKAER_PKT_2_Y={1:0.###}", prefix, CutLine.To.Y));
        }

        public override void Transform(Transform xform)
        {
            CutLine.Transform(xform);
        }
    }


    public enum BeamSideType
    {
        End1,
        End2,
        Inside,
        Outside,
        Top,
        Bottom
    }

    public class BeamSide : ITransformable, ICix
    {
        public List<Operation> Operations;
        public BeamSideType SideType;

        public BeamSide(BeamSideType sideType)
        {
            SideType = sideType;
            Operations = new List<Operation>();
        }

        public void ToCix(List<string> cix, string prefix="")
        {
            switch(SideType)
            {
                case (BeamSideType.Bottom):
                    break;
                case (BeamSideType.Top):
                    prefix = prefix + "TOP_";
                    break;
                case (BeamSideType.End1):
                    prefix = prefix + "E_1_";
                    break;
                case (BeamSideType.End2):
                    prefix = prefix + "E_2_";
                    break;
                case (BeamSideType.Inside):
                    prefix = prefix + "IN_";
                    break;
                case (BeamSideType.Outside):
                    prefix = prefix + "OUT_";
                    break;
            }

            for (int i = 0; i < Operations.Count; ++i)
            {
                cix.Add(string.Format("({0} ({1}))", Operations[i].Name, Operations[i].Id));
                Operations[i].ToCix(cix, prefix);
            }
        }

        public void Transform(Transform xform)
        {
            for (int i = 0; i < Operations.Count; ++i)
                Operations[i].Transform(xform);
        }
    }

    public class Workpiece : ITransformable, ICix
    {
        //public List<BeamSide> Sides;
        public string Name;

        public BeamSide[] Sides;

        public BeamSide E1 { get { return Sides[0]; } }
        public BeamSide E2 { get { return Sides[1]; } }
        public BeamSide Top { get { return Sides[2]; } }
        public BeamSide Bottom { get { return Sides[3]; } }
        public BeamSide Inside { get { return Sides[4]; } }
        public BeamSide Outside { get { return Sides[5]; } }

        public Workpiece(string name = "Workpiece")
        {
            Name = name;
            Sides = new BeamSide[]
            {
                new BeamSide(BeamSideType.End1),
                new BeamSide(BeamSideType.End2),
                new BeamSide(BeamSideType.Top),
                new BeamSide(BeamSideType.Bottom),
                new BeamSide(BeamSideType.Inside),
                new BeamSide(BeamSideType.Outside)
            };
        }
        public void ToCix(List<string> cix, string prefix = "")
        {
            for (int i = 0; i < Sides.Length; ++i)
                Sides[i].ToCix(cix, prefix);
        }

        public void Transform(Transform xform)
        {
            for (int i = 0; i < Sides.Length; ++i)
                Sides[i].Transform(xform);
        }

        public void FlipSides()
        {
            var temp_sides = new BeamSide[6];
            Array.Copy(Sides, temp_sides, 6);

            Sides[0] = temp_sides[1];
            Sides[1] = temp_sides[0];

            Sides[2] = temp_sides[3];
            Sides[3] = temp_sides[2];

            Sides[0].SideType = BeamSideType.End1;
            Sides[1].SideType = BeamSideType.End2;
            Sides[2].SideType = BeamSideType.Top;
            Sides[3].SideType = BeamSideType.Bottom;
        }

        public List<Operation> GetAllOperations()
        {
            return Sides.SelectMany(x => x.Operations).ToList();
        }
    }




}
