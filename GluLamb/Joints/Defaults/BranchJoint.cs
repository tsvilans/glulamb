using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;

namespace GluLamb.Joints
{
    public class BranchJoint : Joint2
    {
        public BranchJoint(List<Element> elements, Factory.JointCondition jc)
        {
            if (jc.Parts.Count != Parts.Length) throw new Exception("BranchJoint needs 2 elements.");
            for (int i = 0; i < Parts.Length; ++i)
            {
                Parts[i] = new JointPart(elements, jc.Parts[i], this);
            }
        }
        /// <summary>
        /// Creates a splice joint between two beam elements.
        /// </summary>
        /// <param name="elements">Array of two beam elements.</param>
        public BranchJoint(Element[] elements) : base()
        {
            if (elements.Length != Parts.Length) throw new Exception("BranchJoint needs 2 elements.");
            for (int i = 0; i < Parts.Length; ++i)
            {
                Parts[i] = new JointPart(elements[i] as BeamElement, this, i);
            }
        }

        /// <summary>
        /// Creates a splice joint between two beam elements.
        /// </summary>
        /// <param name="eA">First beam element.</param>
        /// <param name="eB">Second beam element.</param>
        public BranchJoint(Element eA, double parameterA, Element eB, double parameterB) : base()
        {
            Parts[0] = new JointPart(eA as BeamElement, this, 0, parameterA);
            Parts[1] = new JointPart(eB as BeamElement, this, 1, parameterB);
        }
        public JointPart FirstHalf { get { return Parts[0]; } }
        public JointPart SecondHalf { get { return Parts[1]; } }
        public override string ToString()
        {
            return "BranchJoint";
        }
        public void Flip()
        {
            var temp = Parts[1];
            Parts[1] = Parts[0];
            Parts[0] = temp;
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
            var part0 = Parts[0];
            var part1 = Parts[1];
            var beam0 = (part0.Element as BeamElement).Beam;
            var beam1 = (part1.Element as BeamElement).Beam;

            var plane0 = beam0.GetPlane(part0.Parameter);
            var plane1 = beam1.GetPlane(part1.Parameter);

            var origin = (plane0.Origin + plane1.Origin) / 2;

            int sign0 = 1;
            int sign1 = -1;

            var v0Crv = (part0.Element as BeamElement).Beam.Centreline;
            var v1Crv = (part1.Element as BeamElement).Beam.Centreline;

            var vv0 = GluLamb.Joints.JointUtil.GetEndConnectionVector(beam0, origin);
            var vv1 = GluLamb.Joints.JointUtil.GetEndConnectionVector(beam1, origin);

            if (vv1 * plane0.XAxis > 0)
                sign0 = -sign0;

            if (vv0 * plane1.XAxis > 0)
                sign1 = -sign1;

            var trimPlane = new Plane(plane0.Origin + plane0.XAxis * beam0.Width * 0.5 * sign0, plane0.ZAxis, plane0.YAxis);
            var trimmers = Brep.CreatePlanarBreps(new Curve[] { new Rectangle3d(trimPlane, new Interval(-300, 300), new Interval(-300, 300)).ToNurbsCurve() }, 0.01);
            part1.Geometry.AddRange(trimmers);

            trimPlane = new Plane(plane1.Origin + plane1.XAxis * beam1.Width * 0.5 * sign1, plane1.ZAxis, plane1.YAxis);
            trimmers = Brep.CreatePlanarBreps(new Curve[] { new Rectangle3d(trimPlane, new Interval(-300, 300), new Interval(-300, 300)).ToNurbsCurve() }, 0.01);
            part0.Geometry.AddRange(trimmers);

            return true;
        }
    }

}
