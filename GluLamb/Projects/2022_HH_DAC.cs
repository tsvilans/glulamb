using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino;
using Rhino.Geometry;

namespace GluLamb.Projects.HHDAC22
{
    public static class HHDAC22_CONSTANTS
    {
        public static double PlateThickness = 20.85;
        public static double DowelDiameter = 16;
        public static double ToolDiameter = 16;
        public static double ToolRadius = ToolDiameter / 2;
    }
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

    /// <summary>
    /// This is a little bit complicated.
    /// </summary>
    public class SlotMachining : Operation
    {
        public Line XLine;
        public double Angle;
        public bool OverridePlane = false;
        public Plane Plane;
        public Polyline Outline;
        public double Radius;
        public double Depth;
        public double Depth0;

        public bool Rough = false;

        public SlotMachining(string name = "SlotMachining", bool rough= false)
        {
            Name = name;
            Rough = rough;
        }

        public override List<object> GetObjects()
        {
            return new List<object> { Plane, Outline };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            string postfix = Rough ? "_GROV" : "";
            cix.Add(string.Format("{0}SLIDS_LODRET_{1}{2}=1", prefix, Id, postfix));
            // Sort out plane transformation here

            Point3d Origin = XLine.From;
            Point3d XPoint = XLine.To;
            double angle = Angle;

            if (!OverridePlane)
            {
                var xaxis = Plane.XAxis;
                Origin = Plane.Origin;
                XPoint = Origin + xaxis * 100;

                Plane plane;
                GluLamb.Utility.AlignedPlane(Origin, Plane.ZAxis, out plane, out angle);
            }


            cix.Add(string.Format("{0}SLIDS_LODRET_{1}{2}_PL_PKT_1_X={3:0.###}", prefix, Id, postfix, Origin.X));
            cix.Add(string.Format("{0}SLIDS_LODRET_{1}{2}_PL_PKT_1_Y={3:0.###}", prefix, Id, postfix, Origin.Y));
            cix.Add(string.Format("{0}SLIDS_LODRET_{1}{2}_PL_PKT_1_Z={3:0.###}", prefix, Id, postfix, -Origin.Z));

            cix.Add(string.Format("{0}SLIDS_LODRET_{1}{2}_PL_PKT_2_X={3:0.###}", prefix, Id, postfix, XPoint.X));
            cix.Add(string.Format("{0}SLIDS_LODRET_{1}{2}_PL_PKT_2_Y={3:0.###}", prefix, Id, postfix, XPoint.Y));
            cix.Add(string.Format("{0}SLIDS_LODRET_{1}{2}_PL_PKT_2_Z={3:0.###}", prefix, Id, postfix, -XPoint.Z));
            cix.Add(string.Format("{0}SLIDS_LODRET_{1}{2}_PL_ALFA={3:0.###}", prefix, Id, postfix, RhinoMath.ToDegrees(angle)));

            int N = Rough ? 5 : 9;

            if (Outline.Count != N)
            {
                throw new Exception(string.Format("Incorrect number of points for slot machining. Rough={0}, requires {1} points.", Rough, N));
            }

            if (Outline != null)
            {
                Point3d temp;
                for (int i = 0; i < Outline.Count; ++i)
                {
                    Plane.RemapToPlaneSpace(Outline[i], out temp);
                    cix.Add(string.Format("{0}SLIDS_LODRET_{1}{2}_PKT_{3}_X={4:0.###}", prefix, Id, postfix, i + 1, temp.X));
                    cix.Add(string.Format("{0}SLIDS_LODRET_{1}{2}_PKT_{3}_Y={4:0.###}", prefix, Id, postfix, i + 1, temp.Y));
                }

                if (Rough)
                {
                    cix.Add(string.Format("{0}SLIDS_LODRET_{1}{2}_B={3:0.###}", prefix, Id, postfix, Outline[1].DistanceTo(Outline[2])));
                    cix.Add(string.Format("{0}SLIDS_LODRET_{1}{2}_L={3:0.###}", prefix, Id, postfix, Outline[2].DistanceTo(Outline[3])));
                }
                else
                {
                    cix.Add(string.Format("{0}SLIDS_LODRET_{1}{2}_B={3:0.###}", prefix, Id, postfix, Outline[3].DistanceTo(Outline[6])));
                    cix.Add(string.Format("{0}SLIDS_LODRET_{1}{2}_L={3:0.###}", prefix, Id, postfix, Outline[5].DistanceTo(Outline[8])));
                }
            }

            if (!Rough)
                cix.Add(string.Format("{0}SLIDS_LODRET_{1}_R={2:0.###}", prefix, Id, Radius));

            cix.Add(string.Format("{0}SLIDS_LODRET_{1}{2}_DYBDE={3:0.###}", prefix, Id, postfix, Depth));
            cix.Add(string.Format("{0}SLIDS_LODRET_{1}{2}_DYBDE_0={3:0.###}", prefix, Id, postfix, Depth0));
        }

        public override void Transform(Transform xform)
        {
            Plane.Transform(xform);
            Outline.Transform(xform);
        }
    }

    /// <summary>
    /// A simplified version of DrillGroup which is for machining the dowel holes
    /// for the plate connectors. These should be aligned on the beam's YZ plane, so 
    /// should be perpendicular to the beam sides. This means that the plane defining
    /// the drillings is perpendicular to the blank sides, so we don't need the Z-value
    /// or the alpha angle.
    /// 
    /// There should only be one drilling in the Drillings list.
    /// </summary>
    public class SideDrillGroup : Operation
    {
        public Plane Plane;
        public List<Drill2d> Drillings;

        public SideDrillGroup(string name = "SideDrillGroup")
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

            var xaxis = Plane.XAxis;
            var origin = Plane.Origin;
            var xpoint = origin + xaxis * 100;

            Plane plane;
            double angle;
            GluLamb.Utility.AlignedPlane(origin, Plane.ZAxis, out plane, out angle);


            cix.Add(string.Format("{0}HUL_{1}_PL_PKT_1_X={2:0.###}", prefix, Id, origin.X));
            cix.Add(string.Format("{0}HUL_{1}_PL_PKT_1_Y={2:0.###}", prefix, Id, origin.Y));
            //cix.Add(string.Format("{0}HUL_{1}_PL_PKT_1_Z={2:0.###}", prefix, Id, origin.Z));

            cix.Add(string.Format("{0}HUL_{1}_PL_PKT_2_X={2:0.###}", prefix, Id, xpoint.X));
            cix.Add(string.Format("{0}HUL_{1}_PL_PKT_2_Y={2:0.###}", prefix, Id, xpoint.Y));
            //cix.Add(string.Format("{0}HUL_{1}_PL_PKT_2_Z={2:0.###}", prefix, Id, xpoint.Z));
            //cix.Add(string.Format("{0}HUL_{1}_PL_ALFA={2:0.###}", prefix, Id, RhinoMath.ToDegrees(angle)));

            cix.Add(string.Format("{0}HUL_{1}_N={2}", prefix, Id, Drillings.Count));

            for (int i = 0; i < Drillings.Count; ++i)
            {
                var d = Drillings[i];
                Point3d pp;
                plane.RemapToPlaneSpace(d.Position, out pp);
                cix.Add(string.Format("\t(PlateDowel_{0}_{1})", Id, i + 1));
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

            var xaxis = Plane.XAxis;
            var origin = Plane.Origin;
            var xpoint = origin + xaxis * 100;

            Plane plane;
            double angle;
            GluLamb.Utility.AlignedPlane(origin, Plane.ZAxis, out plane, out angle);


            cix.Add(string.Format("{0}HUL_{1}_PL_PKT_1_X={2:0.###}", prefix, Id, origin.X));
            cix.Add(string.Format("{0}HUL_{1}_PL_PKT_1_Y={2:0.###}", prefix, Id, origin.Y));
            cix.Add(string.Format("{0}HUL_{1}_PL_PKT_1_Z={2:0.###}", prefix, Id, origin.Z));

            cix.Add(string.Format("{0}HUL_{1}_PL_PKT_2_X={2:0.###}", prefix, Id, xpoint.X));
            cix.Add(string.Format("{0}HUL_{1}_PL_PKT_2_Y={2:0.###}", prefix, Id, xpoint.Y));
            cix.Add(string.Format("{0}HUL_{1}_PL_PKT_2_Z={2:0.###}", prefix, Id, xpoint.Z));
            //cix.Add(string.Format("{0}HUL_{1}_PL_ALFA={2:0.###}", prefix, Id, RhinoMath.ToDegrees(angle)));

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
            double angle = Vector3d.VectorAngle(-Vector3d.ZAxis, Plane.YAxis) * sign;

            Plane plane;
            Utility.AlignedPlane(Plane.Origin, Plane.ZAxis, out plane, out angle);

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
            // Turn on joint
            cix.Add(string.Format("{0}HAK_{1}=1", prefix, Id));

            // Write outline
            for (int i = 0; i < Outline.Count; ++i)
            {
                cix.Add(string.Format("{0}HAK_{1}_PKT_{2}_X={3:0.###}", prefix, Id, i + 1, Outline[i].X));
                cix.Add(string.Format("{0}HAK_{1}_PKT_{2}_Y={3:0.###}", prefix, Id, i + 1, Outline[i].Y));
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
                cix.Add(string.Format("{0}HAK_{1}_LINE_{2}_PKT_1_X={3:0.###}", prefix, Id, i + 1, SideLines[i].From.X));
                cix.Add(string.Format("{0}HAK_{1}_LINE_{2}_PKT_1_Y={3:0.###}", prefix, Id, i + 1, SideLines[i].From.Y));
                cix.Add(string.Format("{0}HAK_{1}_LINE_{2}_PKT_2_X={3:0.###}", prefix, Id, i + 1, SideLines[i].To.X));
                cix.Add(string.Format("{0}HAK_{1}_LINE_{2}_PKT_2_Y={3:0.###}", prefix, Id, i + 1, SideLines[i].To.Y));
            }

            cix.Add(string.Format("{0}HAK_{1}_DYBDE={2:0.###}", prefix, Id, Depth));
            cix.Add(string.Format("{0}HAK_{1}_ALFA={2:0.###}", prefix, Id, Alpha));
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
