using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;

namespace GluLamb.Joints
{
    [Serializable]
    public class VBeamJoint : Joint3<BeamElement>
    {
        public VBeamJoint(List<Element> elements, Factory.JointCondition jc) : base()
        {
            if (jc.Parts.Count != Parts.Length) throw new Exception("VBeamJoint needs 3 elements.");
            var c = jc.Parts[0].Case | (jc.Parts[1].Case << 1) | (jc.Parts[2].Case << 2);
            int[] indices;
            switch (c)
            {
                case (1):
                    indices = new int[] { 1, 2, 0 };
                    break;
                case (2):
                    indices = new int[] { 0, 2, 1 };
                    break;
                case (4):
                    indices = new int[] { 0, 1, 2 };
                    break;
                default:
                    indices = new int[] { 0, 1, 2 };
                    break;
            }

            for (int i = 0; i < Parts.Length; ++i)
            {
                Parts[i] = new JointPart(elements, jc.Parts[indices[i]], this);
            }
        }
        /// <summary>
        /// Creates a joint between three beam elements.
        /// </summary>
        /// <param name="elements">Array of three beam elements.</param>
        public VBeamJoint(Element[] elements) : base()
        {
            if (elements.Length != Parts.Length) throw new Exception("VBeamJoint needs 3 elements.");
            for (int i = 0; i < Parts.Length; ++i)
            {
                Parts[i] = new JointPart(elements[i] as BeamElement, this, i);
            }
        }
        /// <summary>
        /// Creates a joint between three beam elements.
        /// </summary>
        /// <param name="v0">First beam element in V.</param>
        /// <param name="v1">Second beam element in V.</param>
        /// <param name="floor">Floor plate beam element.</param>
        public VBeamJoint(Element v0, Element v1, Element floor) : base()
        {
            Parts[0] = new JointPart(v0 as BeamElement, this, 0);
            Parts[1] = new JointPart(v1 as BeamElement, this, 1);
            Parts[2] = new JointPart(floor as BeamElement, this, 2);
        }

        public JointPart V0 { get { return Parts[0]; } }
        public JointPart V1 { get { return Parts[1]; } }
        public JointPart Beam { get { return Parts[2]; } }
        public override string ToString()
        {
            return "VBeamJoint";
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
            //var breps = new DataTree<Brep>();

            // Get beam for trimming the other ones
            var bPart = Beam;
            var beam = (bPart.Element as BeamElement).Beam;
            var v0beam = (V0.Element as BeamElement).Beam;
            var v1beam = (V1.Element as BeamElement).Beam;

            var bplane = beam.GetPlane(bPart.Parameter);
            int sign = 1;

            var v0Crv = (V0.Element as BeamElement).Beam.Centreline;
            if ((bplane.Origin - v0Crv.PointAt(v0Crv.Domain.Mid)) * bplane.XAxis > 0)
            {
                sign = -1;
            }

            // Get point and normal of intersection
            var points = new Point3d[3];
            var vectors = new Vector3d[3];

            var average = Point3d.Origin;

            for (int i = 0; i < Parts.Length; ++i)
            {
                var crv = (Parts[i].Element as BeamElement).Beam.Centreline;
                points[i] = crv.PointAt(Parts[i].Parameter);
                vectors[i] = crv.TangentAt(Parts[i].Parameter);

                var midpt = crv.PointAt(crv.Domain.Mid);

                if ((points[i] - midpt) * vectors[i] < 0)
                    vectors[i] = -vectors[i];

                average = average + points[i];
            }

            average = average / 3;

            // Create dividing surface for V-beams
            var tangent = (vectors[0] + vectors[1]) / 2;
            tangent.Unitize();

            var normal = Vector3d.CrossProduct(vectors[0], vectors[1]);
            var binormal = Vector3d.CrossProduct(tangent, normal);

            Point3d vpt0, vpt1;
            var res = v0beam.Centreline.ClosestPoints(v1beam.Centreline, out vpt0, out vpt1);


            var vx = (vpt0 + vpt1) / 2;

            var vv0 = JointUtil.GetEndConnectionVector(v0beam, vx);
            var vv1 = JointUtil.GetEndConnectionVector(v1beam, vx);

            var yaxis = (v0beam.GetPlane(V0.Parameter).YAxis + v1beam.GetPlane(V1.Parameter).YAxis) / 2;
            var v0plane = v0beam.GetPlane(vx);
            var v1plane = v1beam.GetPlane(vx);

            var xaxis0 = v0plane.XAxis;
            var xaxis1 = v1plane.XAxis;

            if (xaxis1 * xaxis0 < 0)
                xaxis1 = -xaxis1;

            yaxis = Vector3d.CrossProduct(xaxis0, xaxis1);

            var divPlane = new Plane(vx, (vv0 + vv1) / 2, yaxis);

            var divider = Brep.CreatePlanarBreps(new Curve[]{
      new Rectangle3d(divPlane, new Interval(-300, 300), new Interval(-300, 300)).ToNurbsCurve()}, 0.01);

            V0.Geometry.AddRange(divider);
            V1.Geometry.AddRange(divider);

            // Create temporary plate
            var plane = new Plane(average, binormal, tangent);
            var cyl = new Cylinder(new Circle(plane, 100), 20).ToBrep(true, true);

            for (int i = 0; i < 3; ++i)
            {
                Parts[i].Geometry.Add(cyl);
            }

            var trimPlane = new Plane(bplane.Origin + bplane.XAxis * beam.Width * 0.5 * sign, bplane.ZAxis, bplane.YAxis);
            var trimmers = Brep.CreatePlanarBreps(new Curve[] { new Rectangle3d(trimPlane, new Interval(-300, 300), new Interval(-300, 300)).ToNurbsCurve() }, 0.01);

            V0.Geometry.AddRange(trimmers);
            V1.Geometry.AddRange(trimmers);

            //return breps;

            return true;
        }
    }

}
