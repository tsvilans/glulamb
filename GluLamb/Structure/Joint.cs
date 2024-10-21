using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino;
using Rhino.Geometry;

namespace GluLamb
{

    public enum JointCaseBits
    {
        MiddleEndBit = 16,
        WhichEndBit = 15,
        PerpAlignedBit = 14,
        ObliqueAcuteBit = 13
    }

    /// <summary>
    /// New joint part class to replace old one, using beams and indices rather than
    /// references to elements
    /// </summary>
    public class JointPartX: IEquatable<JointPartX>, IComparable<JointPartX>
    {
        public static bool IsAtEnd(int c) =>            (c & (1 << (int)JointCaseBits.MiddleEndBit)) == 0;
        public static bool IsAtMiddle(int c) =>         (c & (1 << (int)JointCaseBits.MiddleEndBit)) > 0;

        public static bool IsPerpendicular(int c) =>    (c & (1 << (int)JointCaseBits.PerpAlignedBit)) == 0;
        public static bool IsAligned(int c) =>          (c & (1 << (int)JointCaseBits.PerpAlignedBit)) > 0;

        public static bool IsOblique(int c) =>          (c & (1 << (int)JointCaseBits.ObliqueAcuteBit)) == 0;
        public static bool IsAcute(int c) =>            (c & (1 << (int)JointCaseBits.ObliqueAcuteBit)) > 0;

        public static bool End0(int c) =>               (c & (1 << (int)JointCaseBits.WhichEndBit)) == 0;
        public static bool End1(int c) =>               (c & (1 << (int)JointCaseBits.WhichEndBit)) > 0;

        public static int SetAtEnd(int c) => c &= ~(1 << (int)JointCaseBits.MiddleEndBit);
        public static int SetAtMiddle(int c) => c |= (1 << (int)JointCaseBits.MiddleEndBit);

        public static int SetAtEnd0(int c) => c &= ~(1 << (int)JointCaseBits.WhichEndBit);
        public static int SetAtEnd1(int c) => c |= (1 << (int)JointCaseBits.WhichEndBit);

        public static int SetPerpendicular(int c) => c &= ~(1 << (int)JointCaseBits.PerpAlignedBit);
        public static int SetAligned(int c) => c |= (1 << (int)JointCaseBits.PerpAlignedBit);

        public static int SetOblique(int c) => c &= ~(1 << (int)JointCaseBits.ObliqueAcuteBit);
        public static int SetAcute(int c) => c |= (1 << (int)JointCaseBits.ObliqueAcuteBit);

        public int ElementIndex = -1;
        public int JointIndex = -1;
        public double Parameter = 0;
        public int Case = 0;
        public Vector3d Direction = Vector3d.Zero;

        public List<Brep> Geometry = new List<Brep>();

        public JointPartX()
        {

        }

        public int CompareTo(JointPartX other)
        {
            return Case.CompareTo(other.Case);
        }

        public bool Equals(JointPartX other)
        {
            if (Case == other.Case 
                && ElementIndex == other.ElementIndex
                //&& Parameter == other.Parameter
                //&& JointIndex == other.JointIndex
                ) { return true; }
            return false;
        }

        public override int GetHashCode()
        {
            return ElementIndex.GetHashCode() ^ Case.GetHashCode();
        }

        public JointPartX DuplicateJointPart()
        {
            return new JointPartX()
            {
                ElementIndex = ElementIndex,
                Case = Case,
                JointIndex = JointIndex,
                Direction = Direction,
                Parameter = Parameter,
                Geometry = Geometry.Select(x => x.DuplicateBrep()).ToList()
            };
        }

    }

    public class JointX
    {
        public static double PerpendicularThreshold = RhinoMath.ToRadians(45);

        public Point3d Position;
        public List<JointPartX> Parts;

        public JointX() : this(new List<JointPartX>(), Point3d.Unset) { }
        public JointX(List<JointPartX> parts) : this(parts, Point3d.Unset) { }
        public JointX(List<JointPartX> parts, Point3d position)
        {
            this.Parts = parts;
            this.Position = position;
        }

        public override string ToString() => $"Joint ({GetType().Name})";

        public virtual int Construct(Dictionary<int, Beam> beams)
        {
            return 0; // return 0 if successful
        }

        public virtual JointX DuplicateJoint()
        {
            return new JointX() { 
                Position = Position, 
                Parts = Parts.Select(x => x.DuplicateJointPart()).ToList() 
            };
        }

        public void Absorb(JointX other)
        {
            Parts.AddRange(other.Parts);
            Parts = Parts.Distinct().ToList();
        }

        public static string ClassifyJoint(JointX joint, double perpendicularThreshold=Math.PI * 0.25)
        {
            string type = "null";

            switch(joint.Parts.Count)
            {
                case 0: // Joint has no parts! Invalid.
                    type = "null";
                    break;
                case 1: // Joint could be an end-detail or other feature
                    if (JointPartX.IsAtEnd(joint.Parts[0].Case))
                    {
                        type = "E";
                    }
                    else
                    {
                        type = "F";
                    }
                    break;
                case 2:
                    if (JointPartX.IsAtEnd(joint.Parts[0].Case) && JointPartX.IsAtEnd(joint.Parts[1].Case))
                    {
                        // corner or splice joint
                        double dot = joint.Parts[0].Direction * joint.Parts[1].Direction;
                        double angle = Math.Acos(dot);

                        if (angle < perpendicularThreshold)
                        {
                            type = "V";
                        }
                        else if (angle > perpendicularThreshold && angle < (Math.PI - perpendicularThreshold))
                        {
                            type = "L";
                        }
                        else
                        {
                            type = "S";
                        }
                    }
                    else if (JointPartX.IsAtMiddle(joint.Parts[0].Case) && JointPartX.IsAtMiddle(joint.Parts[1].Case))
                    {
                        // crossing joint
                        type = "X";
                    }
                    else
                    {
                        // T-joint
                        type = "T";
                    }

                    break;
                default:
                    type = $"{joint.Parts.Count}J";
                    break;
            }

            return type;
        }

        public static List<JointX> MergeJoints(List<JointX> joints, double merge_distance = 50.0)
        {
            if (joints.Count < 1) return joints;

            var flags = new bool[joints.Count];
            var newJoints = new List<JointX>();

            for (int i = 0; i < joints.Count - 1; ++i)
            {
                if (flags[i]) continue;

                for (int j = i + 1; j < joints.Count; ++j)
                {
                    if (joints[i].Position.DistanceTo(joints[j].Position) < merge_distance)
                    {
                        flags[j] = true;
                        joints[i].Absorb(joints[j]);
                    }
                }
                newJoints.Add(joints[i]);
            }

            if (!flags[joints.Count - 1])
                newJoints.Add(joints[joints.Count - 1]);

            for (int i = 0; i < newJoints.Count; ++i)
            {
                newJoints[i].Parts.Sort();
            }

            return newJoints;
        }

        public virtual void Configure(Dictionary<string, double> values)
        {
        }

        public virtual List<object> GetDebugList() { return null; }

    }

    [Serializable]
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

    [Serializable]
    public abstract class Joint
    {
        public Plane Plane;
        public JointPart[] Parts
        {
            get;
            protected set;
        }

        public List<object> debug;

        public override string ToString()
        {
            return "Joint";
        }

        public abstract bool Construct(bool append = false);
    }

    [Serializable]
    public abstract class Joint1 : Joint
    {
        protected Joint1()
        {
            Parts = new JointPart[1];
        }
    }

    [Serializable]
    public abstract class Joint2 : Joint
    {
        protected Joint2()
        {
            Parts = new JointPart[2];
        }
    }

    [Serializable]
    public abstract class Joint3<T> : Joint
    {
        protected Joint3()
        {
            Parts = new JointPart[3];
        }
    }

    [Serializable]
    public abstract class Joint4<T> : Joint
    {
        protected Joint4()
        {
            Parts = new JointPart[4];
        }
    }
}
