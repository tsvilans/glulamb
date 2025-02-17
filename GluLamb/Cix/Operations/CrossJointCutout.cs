using GluLamb.Projects.HHDAC22;
using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace GluLamb.Cix.Operations
{
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

        public override object Clone()
        {
            return new CrossJointCutout(Name)
            {
                Outline = Outline.Duplicate(),
                Plane = Plane,
                SideLines = new Line[] { SideLines[0], SideLines[1], SideLines[2], SideLines[3] },
                Depth = Depth,
                Alpha = Alpha,
                MaxSpan = MaxSpan,
            };
        }

        public override List<object> GetObjects()
        {
            return new List<object> { Plane, Outline, MaxSpan, SideLines[0], SideLines[1], SideLines[2], SideLines[3] };
        }

        public override void ToCix(List<string> cix, string prefix = "")
        {
            // Turn on joint
            cix.Add(string.Format("{0}HAK_{1}={2}", prefix, Id, Enabled ? 1 : 0));

            if (!Enabled) return;

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

        public static CrossJointCutout FromCix(Dictionary<string, double> cix, string prefix = "", string id = "")
        {
            var name = $"{prefix}HAK_{id}";

            if (!cix.ContainsKey(name) || cix[name] < 1)
                return null;

            var cutout = new CrossJointCutout(name);

            cutout.MaxSpan = new Line(
                new Point3d(
                    cix[$"{name}_PL_PKT_1_X"],
                    cix[$"{name}_PL_PKT_1_Y"],
                    0
                    ),
                new Point3d(
                    cix[$"{name}_PL_PKT_2_X"],
                    cix[$"{name}_PL_PKT_2_Y"],
                    0
                    )
                );

            cutout.Alpha = RhinoMath.ToRadians(cix[$"{name}_ALFA"]);
            cutout.Depth = cix[$"{name}_DYBDE"];

            var yaxis = -Vector3d.ZAxis;
            yaxis.Transform(Rhino.Geometry.Transform.Rotation(-cutout.Alpha, cutout.MaxSpan.Direction, cutout.MaxSpan.From));

            cutout.Plane = new Plane(cutout.MaxSpan.From, cutout.MaxSpan.Direction, yaxis);

            cutout.Outline = new Polyline();
            for (int i = 1; i <= 7; ++i)
            {
                cutout.Outline.Add(
                    new Point3d(
                        cix[$"{name}_PKT_{i}_X"],
                        cix[$"{name}_PKT_{i}_Y"],
                        0
                        ));
            }

            for (int i = 0; i < 2; ++i)
            {
                cutout.SideLines[i] = new Line(
                    cutout.Plane.PointAt(
                        cix[$"{name}_LINE_{i+1}_PKT_1_X"],
                        cix[$"{name}_LINE_{i + 1}_PKT_1_Y"]
                        ),
                    cutout.Plane.PointAt(
                        cix[$"{name}_LINE_{i + 1}_PKT_2_X"],
                        cix[$"{name}_LINE_{i + 1}_PKT_2_Y"]
                        )
                    );
            }

            for (int i = 2; i < 4; ++i)
            {
                cutout.SideLines[i] = new Line(
                    new Point3d(
                        cix[$"{name}_LINE_{i + 1}_PKT_1_X"],
                        cix[$"{name}_LINE_{i + 1}_PKT_1_Y"],
                        0
                        ),
                    new Point3d(
                        cix[$"{name}_LINE_{i + 1}_PKT_2_X"],
                        cix[$"{name}_LINE_{i + 1}_PKT_2_Y"],
                        0
                        )
                    );
            }


            return cutout;
        }
    }
}
