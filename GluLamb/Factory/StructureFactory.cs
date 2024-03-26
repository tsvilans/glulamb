using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;
using GluLamb.Joints;

namespace GluLamb.Factory
{
    [Obsolete]
    public class StructureFactory
    {
        public static List<Joint> FindBeamJointConditions(List<Element> elements, double searchDistance, double overlapDistance, double end_threshold = 0.1)
        {
            int counter = 0;
            foreach (var ele in elements)
            {
                counter++;
            }
            BeamElement be0, be1;

            var joints = new List<Joint>();
            var joined = new List<List<int>>();

            // Find joints between more than 2 elements
            for (int i = 0; i < elements.Count - 1; ++i)
            {
                be0 = elements[i] as BeamElement;
                if (be0 == null) continue;

                var endpoints0 = new Point3d[] { be0.Beam.Centreline.PointAtStart, be0.Beam.Centreline.PointAtEnd };
                var matches = new List<int>();

                for (int j = i + 1; j < elements.Count; ++j)
                {
                    be1 = elements[j] as BeamElement;
                    if (be1 == null) continue;
                    var endpoints1 = new Point3d[] { be1.Beam.Centreline.PointAtStart, be1.Beam.Centreline.PointAtEnd };
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
                    var vjoint = new VBeamJoint(matches.Select(x => elements[x]).ToArray());
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
                be0 = elements[i] as BeamElement;
                if (be0 == null) continue;

                for (int j = i + 1; j < elements.Count; ++j)
                {
                    if (joined[i].Contains(j)) continue;

                    be1 = elements[j] as BeamElement;
                    if (be1 == null) continue;

                    var crv0 = be0.Beam.Centreline;
                    var crv1 = be1.Beam.Centreline;
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

                        Joint joint;

                        switch (type)
                        {
                            case (0):
                                joint = new CrossJoint(be0, tA, be1, tB);
                                break;
                            case (3):
                                joint = new CrossJoint(be0, tA, be1, tB);
                                break;
                            case (1):
                                joint = new TenonJoint(be0, tA, be1, tB);
                                break;
                            case (2):
                                joint = new TenonJoint(be1, tB, be0, tA);

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

        public JointConditionPart(JointConditionPart jcp)
        {
            Index = jcp.Index;
            Case = jcp.Case;
            Parameter = jcp.Parameter;
        }

        public override bool Equals(object other)
        {
            if (other is JointConditionPart)
                return Equals(other as JointConditionPart);
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

        /// <summary>
        /// Angle at which a joint is considered either a splice or a corner.
        /// </summary>
        public static double SpliceCornerThreshold = Rhino.RhinoMath.ToRadians(15.0);
        public static double BranchThreshold = Rhino.RhinoMath.ToRadians(30.0);

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

                        var lA = crv0.GetLength(new Interval(crv0.Domain.Min, tA));
                        var lB = crv1.GetLength(new Interval(crv1.Domain.Min, tB));

                        var crv0Length = crv0.GetLength();
                        var crv1Length = crv1.GetLength();

                        int case0 = Math.Abs(lA) < end_threshold || Math.Abs(lA - crv0Length) < end_threshold ? 0 : 1;
                        int case1 = Math.Abs(lB ) < end_threshold || Math.Abs(lB - crv1Length) < end_threshold ? 0 : 1;
                        
                        if ((case0 | (case1 << 1)) > 0 && Math.Abs(crv0.TangentAt(tA) * crv1.TangentAt(tB)) > 0.95)
                        {
                            case0 = 0;
                            case1 = 0;
                        }

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

        public static List<JointCondition> FindJointConditions(List<Element> elements, double radius = 100.0, double end_threshold = 10.0, double merge_distance = 50.0)
        {
            if (elements.Count < 1) throw new Exception("FindJointConditions(): No elements in list!");

            var jcs = new List<JointCondition>();

            var curves = elements.Select(x => (x as BeamElement).Beam.Centreline).ToList();
            return FindJointConditions(curves, radius, end_threshold, merge_distance);

            for (int i = 0; i < elements.Count - 1; ++i)
            {
                var crv0 = (elements[i] as BeamElement).Beam.Centreline;

                for (int j = i + 1; j < elements.Count; ++j)
                {
                    var crv1 = (elements[j] as BeamElement).Beam.Centreline;

                    var res = Rhino.Geometry.Intersect.Intersection.CurveCurve(crv0, crv1, radius, radius);

                    foreach (var r in res)
                    {
                        var pos = (r.PointA + r.PointB) / 2;

                        var tA = r.ParameterA;
                        var tB = r.ParameterB;

                        var lA = crv0.GetLength(new Interval(crv0.Domain.Min, tA));
                        var lB = crv1.GetLength(new Interval(crv1.Domain.Min, tB));

                        var crv0Length = crv0.GetLength();
                        var crv1Length = crv1.GetLength();

                        int case0 = Math.Abs(lA) < end_threshold || Math.Abs(lA - crv0Length) < end_threshold ? 0 : 1;
                        int case1 = Math.Abs(lB) < end_threshold || Math.Abs(lB - crv1Length) < end_threshold ? 0 : 1;

                        if ((case0 | (case1 << 1)) > 0 && Math.Abs(crv0.TangentAt(tA) * crv1.TangentAt(tB)) > 0.95)
                        {
                            case0 = 0;
                            case1 = 0;
                        }

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

        public static List<Joint> ClassifyJoints(List<Element> beams, List<JointCondition> jcs)
        {
            var types = new List<Joint>();
            int c = 0;

            foreach (var jc in jcs)
            {
                Joint type = null;

                switch (jc.Parts.Count)
                {
                    // The joint has 2 members
                    case (2):
                        c = jc.Parts[0].Case | (jc.Parts[1].Case << 1);
                        switch (c)
                        {
                            case (0):
                                //type = "EndToEndJoint";
                                var t0 = (beams[jc.Parts[0].Index] as BeamElement).Beam.Centreline.TangentAt(jc.Parts[0].Parameter);
                                var t1 = (beams[jc.Parts[1].Index] as BeamElement).Beam.Centreline.TangentAt(jc.Parts[1].Parameter);
                                if (t0 * t1 < 0)
                                    t1 = -t1;

                                if (Math.Abs(t0 * t1) < Math.Cos(SpliceCornerThreshold))
                                    type = new CornerJoint(beams, jc);
                                else if (t0 * t1 < Math.Cos(BranchThreshold))
                                    type = new BranchJoint(beams, jc);
                                else
                                    type = new SpliceJoint(beams, jc);
                                break;
                            case (1):
                                //type = "TenonJoint";
                                type = new TenonJoint(beams, jc.Parts[1], jc.Parts[0]);
                                break;
                            case (2):
                                //type = "TenonJoint";
                                type = new TenonJoint(beams, jc.Parts[0], jc.Parts[1]);
                                break;
                            case (3):
                                //type = "CrossJoint";
                                type = new CrossJoint(beams, jc);
                                break;
                        }
                        break;
                    // The joint has 3 members
                    case (3):
                        type = new VBeamJoint(beams, jc);
                        break;
                    // The joint has 4 members
                    case (4):
                        type = new FourWayJoint(beams, jc);
                        break;
                    default:
                        break;
                }
                types.Add(type);
            }

            return types;
        }

        public static void FindAndClassifyJoints(Structure structure, double search_distance, double end_distance, double merge_distance)
        {
            var jcs = FindJointConditions(structure.Elements, search_distance, end_distance, merge_distance);
            var joints = ClassifyJoints(structure.Elements, jcs);

            structure.Joints = joints;
        }

        public static List<JointCondition> MergeJointConditions(List<JointCondition> jcs, double merge_distance = 50.0)
        {
            if (jcs.Count < 1) return jcs;

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

            if (!flags[jcs.Count - 1])
                jcs_new.Add(jcs[jcs.Count - 1]);

            return jcs_new;
        }

        public override string ToString()
        {
            return string.Format("JointCondition ({0})", string.Join(",", Parts.Select(x => x.Index.ToString()).ToArray()));
        }


    }
}
