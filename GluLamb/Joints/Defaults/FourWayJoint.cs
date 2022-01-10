using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;

namespace GluLamb.Joints
{
    public class FourWayJoint : Joint4<BeamElement>
    {
        public FourWayJoint(List<Element> elements, Factory.JointCondition jc)
        {
            if (jc.Parts.Count != Parts.Length) throw new Exception("FourWayJoint needs 4 elements.");

            // Sort elements around the joint normal
            var vectors = new List<Vector3d>();
            var normal = Vector3d.Zero;

            for (int i = 0; i < jc.Parts.Count; ++i)
            {
                var tan = GluLamb.Joints.JointUtil.GetEndConnectionVector((elements[jc.Parts[i].Index] as BeamElement).Beam, jc.Position);
                vectors.Add(tan);
            }
            for (int i = 0; i < vectors.Count; ++i)
            {
                int ii = (i + 1).Modulus(4);

                normal += Vector3d.CrossProduct(vectors[i], vectors[ii]);
            }

            normal /= vectors.Count;

            List<int> indices;
            Utility.SortVectorsAroundPoint(vectors, jc.Position, normal, out indices);

            for (int i = 0; i < Parts.Length; ++i)
            {
                Parts[i] = new JointPart(elements, jc.Parts[indices[i]], this);
            }

            var xaxis = Vector3d.CrossProduct(normal, Vector3d.ZAxis);
            if (xaxis.IsTiny(0.001)) xaxis = Vector3d.XAxis;
            var yaxis = Vector3d.CrossProduct(xaxis, normal);
            this.Plane = new Plane(jc.Position, xaxis, yaxis);
        }
        /// <summary>
        /// Creates a joint between four beam elements.
        /// </summary>
        /// <param name="elements">Array of 4 beam elements.</param>
        public FourWayJoint(Element[] elements) : base()
        {

            if (elements.Length != Parts.Length) throw new Exception("FourWayJoint needs 4 elements.");
            for (int i = 0; i < Parts.Length; ++i)
            {
                Parts[i] = new JointPart(elements[i] as BeamElement, this, i);
            }
        }
        public JointPart TopLeft { get { return Parts[0]; } }
        public JointPart TopRight { get { return Parts[1]; } }
        public JointPart BottomLeft { get { return Parts[2]; } }
        public JointPart BottomRight { get { return Parts[3]; } }
        public override string ToString()
        {
            return "FourWayJoint";
        }

        public override bool Construct(bool append = false)
        {
            // Get point and normal of intersection
            var points = new Point3d[4];
            var vectors = new Vector3d[4];

            var average = Plane.Origin;

            for (int i = 0; i < Parts.Length; ++i)
            {
                var crv = (Parts[i].Element as BeamElement).Beam.Centreline;
                points[i] = crv.PointAt(Parts[i].Parameter);
                vectors[i] = crv.TangentAt(Parts[i].Parameter);

                var midpt = crv.PointAt(crv.Domain.Mid);

                if ((points[i] - midpt) * vectors[i] < 0)
                    vectors[i] = -vectors[i];

                //average = average + points[i];
            }

            //average = average / 3;

            for (int i = 0; i < vectors.Length; ++i)
            {
                int ii = (i + 1).Modulus(4);

                var v0beam = (Parts[i].Element as BeamElement).Beam;
                var v1beam = (Parts[ii].Element as BeamElement).Beam;

                var tangent = (vectors[i] + vectors[ii]) / 2;
                tangent.Unitize();

                var normal = Vector3d.CrossProduct(vectors[i], vectors[ii]);
                //var normal = fj.Plane.ZAxis;
                var binormal = Vector3d.CrossProduct(tangent, normal);

                Point3d vpt0, vpt1;
                var res = v0beam.Centreline.ClosestPoints(v1beam.Centreline, out vpt0, out vpt1);

                var vx = (vpt0 + vpt1) / 2;

                var vv0 = GluLamb.Joints.JointUtil.GetEndConnectionVector(v0beam, vx);
                var vv1 = GluLamb.Joints.JointUtil.GetEndConnectionVector(v1beam, vx);

                var yaxis = (v0beam.GetPlane(Parts[i].Parameter).YAxis + v1beam.GetPlane(Parts[ii].Parameter).YAxis) / 2;
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

                Parts[i].Geometry.AddRange(divider);
                Parts[ii].Geometry.AddRange(divider);
            }

            return true;
        }

    }

}
