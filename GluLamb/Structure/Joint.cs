using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;

namespace GluLamb
{
    public class JointPart
    {
        public double Parameter;
        public Element Element { get; }
        public Joint Joint { get; }
        public List<Brep> Geometry;
        public int Index { get; set; }

        public JointPart(Element element, Joint joint, int index = 0, double parameter = 0.0)
        {
            if (element == null) throw new Exception("JointPart got bad input.");

            Element = element;
            Joint = joint;
            Index = index;
            Parameter = parameter;
            Geometry = new List<Brep>();
        }

        public JointPart(List<Element> elements, Factory.JointConditionPart jcp, Joint jt)
        {
            Element = elements[jcp.Index];
            Index = jcp.Index;
            Parameter = jcp.Parameter;
            Geometry = new List<Brep>();
            Joint = jt;
        }

        public JointPart(JointPart jp)
        {
            Element = jp.Element;
            Joint = jp.Joint;
            Index = jp.Index;
            Geometry = jp.Geometry;
        }

        public override string ToString()
        {
            return "JointPart";
        }
    }

    public abstract class Joint
    {
        public Plane Plane;
        public JointPart[] Parts
        {
            get;
            protected set;
        }
        /*
        protected Joint(int numParts)
        {
            Parts = new JointPart<T>[numParts];
        }
        */

        public override string ToString()
        {
            return "Joint";
        }
    }

    public abstract class Joint2 : Joint
    {
        protected Joint2()
        {
            Parts = new JointPart[2];
        }
    }

    public abstract class Joint3<T> : Joint
    {
        protected Joint3()
        {
            Parts = new JointPart[3];
        }
    }

    public abstract class Joint4<T> : Joint
    {
        protected Joint4()
        {
            Parts = new JointPart[4];
        }
    }

    public class CrossJoint : Joint2
    {
        public CrossJoint(List<Element> elements, Factory.JointCondition jc)
        {
            if (jc.Parts.Count != Parts.Length) throw new Exception("CrossJoint needs 2 elements.");
            for (int i = 0; i < Parts.Length; ++i)
            {
                Parts[i] = new JointPart(elements, jc.Parts[i], this);
            }
        }
        /// <summary>
        /// Creates a crossing joint between two beam elements.
        /// </summary>
        /// <param name="beamA">Beam element that goes over.</param>
        /// <param name="beamB">Beam element that goes under.</param>
        public CrossJoint(Element beamA, double parameterA, Element beamB, double parameterB) : base()
        {
            Parts[0] = new JointPart(beamA as BeamElement, this, 0, parameterA);
            Parts[1] = new JointPart(beamB as BeamElement, this, 1, parameterB);
        }

        public JointPart Over { get { return Parts[0]; } }
        public JointPart Under { get { return Parts[1]; } }

        public override string ToString()
        {
            return "CrossJoint";
        }

        public void Flip()
        {
            var temp = Parts[1];
            Parts[1] = Parts[0];
            Parts[0] = temp;
        }

    }

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
    }

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

    }

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
    }

    public class CornerJoint : Joint2
    {
        public CornerJoint(List<Element> elements, Factory.JointCondition jc)
        {
            if (jc.Parts.Count != Parts.Length) throw new Exception("CornerJoint needs 2 elements.");
            for (int i = 0; i < Parts.Length; ++i)
            {
                Parts[i] = new JointPart(elements, jc.Parts[i], this);
            }
        }
        /// <summary>
        /// Creates a splice joint between two beam elements.
        /// </summary>
        /// <param name="elements">Array of two beam elements.</param>
        public CornerJoint(Element[] elements) : base()
        {
            if (elements.Length != Parts.Length) throw new Exception("CornerJoint needs 2 elements.");
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
        public CornerJoint(Element eA, double parameterA, Element eB, double parameterB) : base()
        {
            Parts[0] = new JointPart(eA as BeamElement, this, 0, parameterA);
            Parts[1] = new JointPart(eB as BeamElement, this, 1, parameterB);
        }
        public JointPart FirstHalf { get { return Parts[0]; } }
        public JointPart SecondHalf { get { return Parts[1]; } }
        public override string ToString()
        {
            return "CornerJoint";
        }
        public void Flip()
        {
            var temp = Parts[1];
            Parts[1] = Parts[0];
            Parts[0] = temp;
        }
    }

    public class SpliceJoint: Joint2
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
    }

    public class VBeamJoint: Joint3<BeamElement>
    {
        public VBeamJoint(List<Element> elements, Factory.JointCondition jc)
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
    }

}
