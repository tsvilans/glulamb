using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;

namespace GluLamb.Joints
{
    [Serializable]
    public class TenonJoint : Joint2
    {
        public TenonJoint(List<Element> elements, Factory.JointCondition jc)
        {
            if (jc.Parts.Count != Parts.Length) throw new Exception("TenonJoint needs 2 elements.");
            for (int i = 0; i < Parts.Length; ++i)
            {
                Parts[i] = new JointPart(elements, jc.Parts[i], this);
            }
        }
        /// <summary>
        /// Creates and mortise and tenon joint between two beam elements.
        /// </summary>
        /// <param name="tenon">Tenon</param>
        /// <param name="mortise">Mortise</param>
        public TenonJoint(Element tenon, double tenon_parameter, Element mortise, double mortise_parameter) : base()
        {
            Parts[0] = new JointPart(tenon as BeamElement, this, 0, tenon_parameter);
            Parts[1] = new JointPart(mortise as BeamElement, this, 1, mortise_parameter);
        }

        /// <summary>
        /// Creates and mortise and tenon joint between two beam elements.
        /// </summary>
        /// <param name="tenon">Tenon</param>
        /// <param name="mortise">Mortise</param>
        public TenonJoint(List<Element> elements, Factory.JointConditionPart tenon, Factory.JointConditionPart mortise) : base()
        {
            Parts[0] = new JointPart(elements[tenon.Index] as BeamElement, this, tenon.Index, tenon.Parameter);
            Parts[1] = new JointPart(elements[mortise.Index] as BeamElement, this, mortise.Index, mortise.Parameter);
        }

        public JointPart Tenon { get { return Parts[0]; } }
        public JointPart Mortise { get { return Parts[1]; } }
        public override string ToString()
        {
            return "TenonJoint";
        }

        public override bool Construct(bool append = false)
        {
            if (!append)
            {
                foreach (var part in Parts)
                {
                    part.Geometry.Clear();
                }
            }
            var tbeam = (Tenon.Element as BeamElement).Beam;
            var mbeam = (Mortise.Element as BeamElement).Beam;

            var tplane = tbeam.GetPlane(Tenon.Parameter);
            var mplane = mbeam.GetPlane(Mortise.Parameter);

            Transform xform = Transform.PlaneToPlane(Plane.WorldXY, tplane);

            // Align plane directions
            int sign = 1;
            var tz = Math.Abs(Tenon.Parameter - tbeam.Centreline.Domain.Min) >
              Math.Abs(Tenon.Parameter - tbeam.Centreline.Domain.Max) ?
              tplane.ZAxis : -tplane.ZAxis;

            Plane mSidePlane = Plane.Unset;

            sign = mplane.XAxis * tz > 0 ? -1 : 1;

            mSidePlane = new Plane(mplane.Origin + mplane.XAxis * mbeam.Width * 0.5 * sign,
              mplane.ZAxis, mplane.YAxis);

            var mSidePlane2 = new Plane(mplane.Origin - mplane.XAxis * mbeam.Width * 0.5 * sign,
              mplane.ZAxis, mplane.YAxis);


            // Create tenon cutting geometry

            double added = 10.0;
            {
                var proj0 = mSidePlane.ProjectAlongVector(tz);
                var proj1 = mSidePlane2.ProjectAlongVector(tz);

                var pt0 = new Point3d(-tbeam.Width * 0.5 - added, 0, 0);
                var pt1 = new Point3d(tbeam.Width * 0.5 + added, 0, 0);

                pt0.Transform(xform);
                pt1.Transform(xform);

                var pt2 = pt0 + tz * (mbeam.Width + added);
                var pt3 = pt1 + tz * (mbeam.Width + added);

                // Create projection along Tenon Z-axis onto the Mortise side plane

                pt0.Transform(proj0);
                pt1.Transform(proj0);

                var tsrf0 = Brep.CreateFromCornerPoints(pt0, pt1, pt2, pt3, 0.001);

                var pt4 = pt0 + tplane.YAxis * (tbeam.Height * 0.5 + added);
                var pt5 = pt1 + tplane.YAxis * (tbeam.Height * 0.5 + added);

                pt4.Transform(proj0);
                pt5.Transform(proj0);

                var tsrf1 = Brep.CreateFromCornerPoints(pt0, pt1, pt5, pt4, 0.001);

                var joined = Brep.JoinBreps(new Brep[] { tsrf0, tsrf1 }, 0.01);

                if (joined == null) joined = new Brep[] { tsrf0, tsrf1 };

                Tenon.Geometry.AddRange(joined);

                // Create trim cut surface
                var pt6 = new Point3d(-tbeam.Width * 0.5 - added, -tbeam.Height * 0.5 - added, 0);
                var pt7 = new Point3d(tbeam.Width * 0.5 + added, -tbeam.Height * 0.5 - added, 0);
                var pt8 = new Point3d(-tbeam.Width * 0.5 + added, tbeam.Height * 0.5 + added, 0);
                var pt9 = new Point3d(tbeam.Width * 0.5 - added, tbeam.Height * 0.5 + added, 0);

                pt6.Transform(xform);
                pt7.Transform(xform);
                pt8.Transform(xform);
                pt9.Transform(xform);

                pt6.Transform(proj1);
                pt7.Transform(proj1);
                pt8.Transform(proj1);
                pt9.Transform(proj1);

                var tsrf2 = Brep.CreateFromCornerPoints(pt6, pt7, pt8, pt9, 0.001);

                Tenon.Geometry.Add(tsrf2);

            }

            // Create mortise cutting geometry
            {
                var pt0 = new Point3d(-tbeam.Width * 0.5, 0, (mbeam.Width * 0.5 + added) * -sign);
                var pt1 = new Point3d(tbeam.Width * 0.5, 0, (mbeam.Width * 0.5 + added) * -sign);

                pt0.Transform(xform);
                pt1.Transform(xform);

                var mSidePlane0 = new Plane(mplane.Origin + mplane.XAxis * mbeam.Width * 0.5 * sign - tz * added,
                  mplane.ZAxis, mplane.YAxis);
                var mSidePlane1 = new Plane(mplane.Origin - mplane.XAxis * mbeam.Width * 0.5 * sign + tz * added,
                  mplane.ZAxis, mplane.YAxis);

                var proj0 = mSidePlane0.ProjectAlongVector(tz);
                var proj1 = mSidePlane1.ProjectAlongVector(tz);

                pt0.Transform(proj0);
                pt1.Transform(proj0);

                var pt2 = pt0; pt2.Transform(proj1);
                var pt3 = pt1; pt3.Transform(proj1);

                var msrf0 = Brep.CreateFromCornerPoints(pt0, pt1, pt3, pt2, 0.001);

                var pt4 = pt0 - tplane.YAxis * tbeam.Height;
                var pt5 = pt2 - tplane.YAxis * tbeam.Height;

                pt4.Transform(proj0);
                pt5.Transform(proj1);

                var msrf1 = Brep.CreateFromCornerPoints(pt0, pt2, pt5, pt4, 0.001);

                var pt6 = pt1 - tplane.YAxis * tbeam.Height;
                var pt7 = pt3 - tplane.YAxis * tbeam.Height;

                pt6.Transform(proj0);
                pt7.Transform(proj1);


                var msrf2 = Brep.CreateFromCornerPoints(pt1, pt3, pt7, pt6, 0.001);

                var joined = Brep.JoinBreps(new Brep[] { msrf0, msrf1, msrf2 }, 0.01);

                if (joined == null) joined = new Brep[] { msrf0, msrf1, msrf2 };

                Mortise.Geometry.AddRange(joined);

            }

            return true;
        }
    }

}
