using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;

namespace GluLamb.Joints
{
    public class SpliceJoint : Joint2
    {
        public SpliceJoint(List<Element> elements, Factory.JointCondition jc)
        {
            if (jc.Parts.Count != Parts.Length) throw new Exception("SpliceJoint needs 2 elements.");
            for (int i = 0; i < Parts.Length; ++i)
            {
                Parts[i] = new JointPart(elements, jc.Parts[i], this);
            }
        }
        /// <summary>
        /// Creates a splice joint between two beam elements.
        /// </summary>
        /// <param name="elements">Array of two beam elements.</param>
        public SpliceJoint(Element[] elements) : base()
        {
            if (elements.Length != Parts.Length) throw new Exception("SpliceJoint needs 2 elements.");
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
        public SpliceJoint(Element eA, double parameterA, Element eB, double parameterB) : base()
        {
            Parts[0] = new JointPart(eA as BeamElement, this, 0, parameterA);
            Parts[1] = new JointPart(eB as BeamElement, this, 1, parameterB);
        }
        public JointPart FirstHalf { get { return Parts[0]; } }
        public JointPart SecondHalf { get { return Parts[1]; } }
        public override string ToString()
        {
            return "SpliceJoint";
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
            var tbeam = (FirstHalf.Element as BeamElement).Beam;
            var mbeam = (SecondHalf.Element as BeamElement).Beam;


            var tplane = tbeam.GetPlane(FirstHalf.Parameter);
            var mplane = mbeam.GetPlane(SecondHalf.Parameter);

            var splicePlane = new Plane((tplane.Origin + mplane.Origin) / 2, tplane.XAxis, tplane.YAxis);

            var spliceCutter = Brep.CreatePlanarBreps(new Curve[]{
                new Rectangle3d(splicePlane, new Interval(-300, 300), new Interval(-300, 300)).ToNurbsCurve()}, 0.01);

            FirstHalf.Geometry.AddRange(spliceCutter);
            SecondHalf.Geometry.AddRange(spliceCutter);

            return true;
        }
    }

}
