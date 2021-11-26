using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;

namespace GluLamb.Factory
{
    public class StructureFactory
    {
        public static List<Joint<BeamElement>> FindJointConditions(List<BeamElement> elements, double searchDistance, double overlapDistance, double end_threshold = 0.1)
        {
            int counter = 0;
            foreach (var ele in elements)
            {
                counter++;
            }

            var joints = new List<Joint<BeamElement>>();

            var joined = new List<List<int>>();

            // Find joints between more than 2 elements
            for (int i = 0; i < elements.Count - 1; ++i)
            {
                var endpoints0 = new Point3d[] { elements[i].Beam.Centreline.PointAtStart, elements[i].Beam.Centreline.PointAtEnd };
                var matches = new List<int>();

                for (int j = i + 1; j < elements.Count; ++j)
                {
                    var endpoints1 = new Point3d[] { elements[j].Beam.Centreline.PointAtStart, elements[j].Beam.Centreline.PointAtEnd };
                    for (int k = 0; k < 2; ++k)
                    {
                        for (int l = 0; l < 2; ++l)
                        {
                            if (endpoints0[k].DistanceTo(endpoints1[l]) < searchDistance)
                            {
                                matches.Add(j);
                            }
                        }
                    }
                }
                if (matches.Count == 3)
                {
                    var vjoint = new VFloorJoint(matches.Select(x => elements[x]).ToArray());
                    vjoint.Plane = new Plane(endpoints0[0], Vector3d.XAxis, Vector3d.YAxis);

                    joints.Add(vjoint);
                }
                else if (matches.Count == 4)
                {
                    var fjoint = new FourWayJoint(matches.Select(x => elements[x]).ToArray());
                    fjoint.Plane = new Plane(endpoints0[0], Vector3d.XAxis, Vector3d.YAxis);
                    joints.Add(fjoint);
                }
                else
                    matches = new List<int>(); // temporary... 

                joined.Add(matches);
            }


            for (int i = 0; i < elements.Count - 1; ++i)
            {
                for (int j = i + 1; j < elements.Count; ++j)
                {
                    if (joined[i].Contains(j)) continue;

                    var e0 = elements[i];
                    var e1 = elements[j];

                    var crv0 = e0.Beam.Centreline;
                    var crv1 = e1.Beam.Centreline;
                    var intersections = Rhino.Geometry.Intersect.Intersection.CurveCurve(crv0, crv1, searchDistance, overlapDistance);

                    foreach (var intersection in intersections)
                    {
                        int type = 0;

                        var tA = intersection.ParameterA;
                        var tB = intersection.ParameterB;

                        if (Math.Abs(tA - crv0.Domain.Min) < end_threshold || Math.Abs(tA - crv0.Domain.Max) < end_threshold)
                            type += 1;
                        if (Math.Abs(tB - crv1.Domain.Min) < end_threshold || Math.Abs(tB - crv1.Domain.Max) < end_threshold)
                            type += 2;

                        Joint<BeamElement> joint;

                        switch (type)
                        {
                            case (0):
                                joint = new CrossJoint(e0, e1);
                                break;
                            case (3):
                                joint = new CrossJoint(e0, e1);
                                break;
                            case (1):
                                joint = new TenonJoint(e0, e1);

                                break;
                            case (2):
                                joint = new TenonJoint(e1, e0);

                                break;
                            default:
                                throw new Exception("Invalid classification");
                        }

                        joint.Plane = new Plane(intersection.PointA, Vector3d.XAxis, Vector3d.YAxis);
                        joints.Add(joint);
                    }
                }
            }

            return joints;
        }
    }

    public class JointConditionPart : IEquatable<JointConditionPart>
    {
        public int Index;
        public int Case;
        public double Parameter;

        public JointConditionPart(int index, int jointcase, double parameter)
        {
            Index = index;
            Case = jointcase; // 0 for end of curve, 1 for mid-curve
            Parameter = parameter; // parameter on curve where the intersection is closest to
        }

        public override bool Equals(object other)
        {
            if (other is JointConditionPart)
                return Index == (other as JointConditionPart).Index && Case == (other as JointConditionPart).Case;
            return false;
        }

        public bool Equals(JointConditionPart other)
        {
            return Index == other.Index && Case == other.Case;
        }

        public override int GetHashCode()
        {
            return Index.GetHashCode() ^ Case.GetHashCode();
        }
    }

    public class JointCondition
    {
        public List<JointConditionPart> Parts;
        public Point3d Position;

        public JointCondition(Point3d pos, List<JointConditionPart> parts)
        {
            Position = pos;
            Parts = parts;
        }

        public void Absorb(JointCondition jc)
        {
            Parts.AddRange(jc.Parts);
            Parts = Parts.Distinct().ToList();
        }

        public static List<JointCondition> FindJointConditions(List<Curve> curves, double radius = 100.0, double end_threshold = 10.0, double merge_distance = 50.0)
        {
            var jcs = new List<JointCondition>();

            for (int i = 0; i < curves.Count - 1; ++i)
            {
                for (int j = i + 1; j < curves.Count; ++j)
                {
                    var crv0 = curves[i];
                    var crv1 = curves[j];

                    var res = Rhino.Geometry.Intersect.Intersection.CurveCurve(crv0, crv1, radius, radius);

                    foreach (var r in res)
                    {
                        var pos = (r.PointA + r.PointB) / 2;

                        var tA = r.ParameterA;
                        var tB = r.ParameterB;

                        int case0 = Math.Abs(tA - crv0.Domain.Min) < end_threshold || Math.Abs(tA - crv0.Domain.Max) < end_threshold ? 0 : 1;
                        int case1 = Math.Abs(tB - crv1.Domain.Min) < end_threshold || Math.Abs(tB - crv1.Domain.Max) < end_threshold ? 0 : 1;

                        var jc = new JointCondition(pos,
                          new List<JointConditionPart>()
                          {
                              new JointConditionPart(i, case0, tA),
                              new JointConditionPart(j, case1, tB)
                            });

                        jcs.Add(jc);
                    }
                }
            }

            return MergeJointConditions(jcs, merge_distance);
        }

        public static List<string> ClassifyJoints(List<JointCondition> jcs)
        {
            var types = new List<string>();
            int c = 0;

            foreach (var jc in jcs)
            {
                string type = "Unknown";

                switch (jc.Parts.Count)
                {
                    // The joint has 2 members
                    case (2):
                        c = jc.Parts[0].Case | (jc.Parts[1].Case << 1);
                        switch (c)
                        {
                            case (0):
                                type = "EndToEndJoint";
                                break;
                            case (1):
                                type = "TenonJoint";
                                break;
                            case (2):
                                type = "TenonJoint";
                                break;
                            case (3):
                                type = "CrossJoint";
                                break;
                        }
                        break;
                    // The joint has 3 members
                    case (3):
                        c = jc.Parts[0].Case | (jc.Parts[1].Case << 1) | (jc.Parts[2].Case << 2);
                        switch (c)
                        {
                            case (0):
                                type = "ThreeWayEndToEndJoint";
                                break;
                            case (1):
                                goto case (2);
                            case (2):
                                goto case (4);
                            case (4):
                                type = "VFloorJoint";
                                break;
                            default:
                                type = "ThreeWayJoint";
                                break;
                        }

                        break;
                    // The joint has 4 members
                    case (4):
                        type = "FourWayJoint";
                        break;
                    default:
                        break;
                }
                types.Add(type);
            }

            return types;
        }

        public static List<JointCondition> MergeJointConditions(List<JointCondition> jcs, double merge_distance = 50.0)
        {
            var flags = new bool[jcs.Count];
            var jcs_new = new List<JointCondition>();

            for (int i = 0; i < jcs.Count - 1; ++i)
            {
                if (flags[i]) continue;

                for (int j = i + 1; j < jcs.Count; ++j)
                {
                    if (jcs[i].Position.DistanceTo(jcs[j].Position) < merge_distance)
                    {
                        flags[j] = true;
                        jcs[i].Absorb(jcs[j]);
                    }
                }
                jcs_new.Add(jcs[i]);
            }

            return jcs_new;
        }
    }
}
