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

        public override string ToString()
        {
            return "Joint";
        }

        public abstract bool Construct(bool append = false);
    }

    public abstract class Joint1 : Joint
    {
        protected Joint1()
        {
            Parts = new JointPart[1];
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
}
