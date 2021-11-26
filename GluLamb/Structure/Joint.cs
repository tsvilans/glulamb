using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;

namespace GluLamb
{
    public class JointPart<T>
    {
        public double Parameter;
        public T Element { get; }
        public Joint<T> Joint { get; }
        public List<Brep> Geometry;
        public int Index { get; }

        public JointPart(T element, Joint<T> joint, int index = 0)
        {
            if (element == null) throw new Exception("JointPart got bad input.");

            Element = element;
            Joint = joint;
            Index = index;
        }

        public override string ToString()
        {
            return "JointPart";
        }
    }

    public abstract class Joint<T>
    {
        public Plane Plane;
        public JointPart<T>[] Parts
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

    public abstract class Joint2<T> : Joint<T>
    {
        protected Joint2()
        {
            Parts = new JointPart<T>[2];
        }
    }

    public abstract class Joint3<T> : Joint<T>
    {
        protected Joint3()
        {
            Parts = new JointPart<T>[3];
        }
    }

    public abstract class Joint4<T> : Joint<T>
    {
        protected Joint4()
        {
            Parts = new JointPart<T>[4];
        }
    }

    public class CrossJoint : Joint2<BeamElement>
    {
        /// <summary>
        /// Creates a crossing joint between two beam elements.
        /// </summary>
        /// <param name="beamA">Beam element that goes over.</param>
        /// <param name="beamB">Beam element that goes under.</param>
        public CrossJoint(Element beamA, Element beamB) : base()
        {
            Parts[0] = new JointPart<BeamElement>(beamA as BeamElement, this, 0);
            Parts[1] = new JointPart<BeamElement>(beamB as BeamElement, this, 1);
        }

        public JointPart<BeamElement> Over { get { return Parts[0]; } }
        public JointPart<BeamElement> Under { get { return Parts[1]; } }

        public override string ToString()
        {
            return "CrossJoint";
        }

    }

    public class TenonJoint : Joint2<BeamElement>
    {

        /// <summary>
        /// Creates and mortise and tenon joint between two beam elements.
        /// </summary>
        /// <param name="tenon">Tenon</param>
        /// <param name="mortise">Mortise</param>
        public TenonJoint(Element tenon, Element mortise) : base()
        {
            Parts[0] = new JointPart<BeamElement>(tenon as BeamElement, this, 0);
            Parts[1] = new JointPart<BeamElement>(mortise as BeamElement, this, 1);
        }

        public JointPart<BeamElement> Tenon { get { return Parts[0]; } }
        public JointPart<BeamElement> Mortise { get { return Parts[1]; } }
        public override string ToString()
        {
            return "TenonJoint";
        }
    }

    public class FourWayJoint : Joint4<BeamElement>
    {
        /// <summary>
        /// Creates a joint between four beam elements.
        /// </summary>
        /// <param name="elements">Array of 4 beam elements.</param>
        public FourWayJoint(Element[] elements) : base()
        {
            if (elements.Length != Parts.Length) throw new Exception("FourWayJoint needs 4 elements.");
            for (int i = 0; i < Parts.Length; ++i)
            {
                Parts[i] = new JointPart<BeamElement>(elements[i] as BeamElement, this, i);
            }
        }
        public JointPart<BeamElement> TopLeft { get { return Parts[0]; } }
        public JointPart<BeamElement> TopRight { get { return Parts[1]; } }
        public JointPart<BeamElement> BottomLeft { get { return Parts[2]; } }
        public JointPart<BeamElement> BottomRight { get { return Parts[3]; } }
        public override string ToString()
        {
            return "FourWayJoint";
        }

    }

    public class SpliceJoint: Joint2<BeamElement>
    {
        /// <summary>
        /// Creates a splice joint between two beam elements.
        /// </summary>
        /// <param name="elements">Array of two beam elements.</param>
        public SpliceJoint(Element[] elements) : base()
        {
            if (elements.Length != Parts.Length) throw new Exception("SpliceJoint needs 2 elements.");
            for (int i = 0; i < Parts.Length; ++i)
            {
                Parts[i] = new JointPart<BeamElement>(elements[i] as BeamElement, this, i);
            }
        }

        /// <summary>
        /// Creates a splice joint between two beam elements.
        /// </summary>
        /// <param name="eA">First beam element.</param>
        /// <param name="eB">Second beam element.</param>
        public SpliceJoint(Element eA, Element eB) : base()
        {
            Parts[0] = new JointPart<BeamElement>(eA as BeamElement, this, 0);
            Parts[1] = new JointPart<BeamElement>(eB as BeamElement, this, 1);
        }
        public JointPart<BeamElement> FirstHalf { get { return Parts[0]; } }
        public JointPart<BeamElement> SecondHalf { get { return Parts[1]; } }
        public override string ToString()
        {
            return "SpliceJoint";
        }
    }

    public class VFloorJoint: Joint3<BeamElement>
    {
        /// <summary>
        /// Creates a joint between three beam elements.
        /// </summary>
        /// <param name="elements">Array of three beam elements.</param>
        public VFloorJoint(Element[] elements) : base()
        {
            if (elements.Length != Parts.Length) throw new Exception("SpliceJoint needs 2 elements.");
            for (int i = 0; i < Parts.Length; ++i)
            {
                Parts[i] = new JointPart<BeamElement>(elements[i] as BeamElement, this, i);
            }
        }
        /// <summary>
        /// Creates a joint between three beam elements.
        /// </summary>
        /// <param name="v0">First beam element in V.</param>
        /// <param name="v1">Second beam element in V.</param>
        /// <param name="floor">Floor plate beam element.</param>
        public VFloorJoint(Element v0, Element v1, Element floor) : base()
        {
            Parts[0] = new JointPart<BeamElement>(v0 as BeamElement, this, 0);
            Parts[1] = new JointPart<BeamElement>(v1 as BeamElement, this, 1);
            Parts[2] = new JointPart<BeamElement>(floor as BeamElement, this, 2);
        }

        public JointPart<BeamElement> V0 { get { return Parts[0]; } }
        public JointPart<BeamElement> V1 { get { return Parts[1]; } }
        public JointPart<BeamElement> Floor { get { return Parts[2]; } }
        public override string ToString()
        {
            return "VFloorJoint";
        }
    }

}
