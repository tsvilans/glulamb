using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;

namespace GluLamb.Joints
{
    public class JointSolver
    {
        /// <summary>
        /// Angle at which a joint is considered either a splice or a corner.
        /// </summary>
        public static double SpliceCornerThreshold = Rhino.RhinoMath.ToRadians(15.0);
        public static double BranchThreshold = Rhino.RhinoMath.ToRadians(30.0);

        private Type _tenonJoint;
        public Type TenonJoint
        {
            set
            {
                if (value.IsSubclassOf(typeof(TenonJoint)) || value == typeof(TenonJoint))
                {
                    _tenonJoint = value;
                }
                else
                    throw new ArgumentException(string.Format("Type needs to inherit from TenonJoint: {0}", value));
            }
            get
            {
                return _tenonJoint;
            }
        }

        private Type _branchJoint;
        public Type BranchJoint
        {
            set
            {
                if (value.IsSubclassOf(typeof(BranchJoint)) || value == typeof(BranchJoint))
                {
                    _branchJoint = value;
                }
                else
                    throw new ArgumentException(string.Format("Type needs to inherit from BranchJoint: {0}", value));
            }
            get
            {
                return _branchJoint;
            }
        }

        private Type _spliceJoint;
        public Type SpliceJoint
        {
            set
            {
                if (value.IsSubclassOf(typeof(SpliceJoint)) || value == typeof(SpliceJoint))
                {
                    _spliceJoint = value;
                }
                else
                    throw new ArgumentException(string.Format("Type needs to inherit from SpliceJoint: {0}", value));
            }
            get
            {
                return _spliceJoint;
            }
        }

        private Type _crossJoint;
        public Type CrossJoint
        {
            set
            {
                if (value.IsSubclassOf(typeof(CrossJoint)) || value == typeof(CrossJoint))
                {
                    _crossJoint = value;
                }
                else
                    throw new ArgumentException(string.Format("Type needs to inherit from CrossJoint: {0}", value));
            }
            get
            {
                return _crossJoint;
            }
        }

        private Type _cornerJoint;
        public Type CornerJoint
        {
            set
            {
                if (value.IsSubclassOf(typeof(CornerJoint)) || value == typeof(CornerJoint))
                {
                    _cornerJoint = value;
                }
                else
                    throw new ArgumentException(string.Format("Type needs to inherit from CornerJoint: {0}", value));
            }
            get
            {
                return _cornerJoint;
            }
        }
        private Type _vbeamJoint;
        public Type VBeamJoint
        {
            set
            {
                if (value.IsSubclassOf(typeof(VBeamJoint)) || value == typeof(VBeamJoint))
                {
                    _vbeamJoint = value;
                }
                else
                    throw new ArgumentException(string.Format("Type needs to inherit from VBeamJoint: {0}", value));
            }
            get
            {
                return _vbeamJoint;
            }
        }

        private Type _fourWayJoint;
        public Type FourWayJoint
        {
            set
            {
                if (value.IsSubclassOf(typeof(FourWayJoint)) || value == typeof(FourWayJoint))
                {
                    _fourWayJoint = value;
                }
                else
                    throw new ArgumentException(string.Format("Type needs to inherit from FourWayJoint: {0}", value));
            }
            get
            {
                return _fourWayJoint;
            }
        }

        public List<Joint> Solve(List<Element> beams, List<Factory.JointCondition> jcs)
        {
            var joints = new List<Joint>();

            var types = new Type[] { typeof(List<Element>), typeof(Factory.JointCondition) };
            var tenontypes = new Type[] { typeof(List<Element>), typeof(Factory.JointConditionPart), typeof(Factory.JointConditionPart) };

            ConstructorInfo tenonXtor = _tenonJoint.GetConstructor(
              BindingFlags.Instance | BindingFlags.Public, null,
              CallingConventions.HasThis, tenontypes, null);

            ConstructorInfo spliceXtor = _spliceJoint.GetConstructor(
              BindingFlags.Instance | BindingFlags.Public, null,
              CallingConventions.HasThis, types, null);

            ConstructorInfo branchXtor = _branchJoint.GetConstructor(
              BindingFlags.Instance | BindingFlags.Public, null,
              CallingConventions.HasThis, types, null);

            ConstructorInfo crossXtor = _crossJoint.GetConstructor(
              BindingFlags.Instance | BindingFlags.Public, null,
              CallingConventions.HasThis, types, null);

            ConstructorInfo cornerXtor = _cornerJoint.GetConstructor(
              BindingFlags.Instance | BindingFlags.Public, null,
              CallingConventions.HasThis, types, null);

            ConstructorInfo vbeamXtor = _vbeamJoint.GetConstructor(
              BindingFlags.Instance | BindingFlags.Public, null,
              CallingConventions.HasThis, types, null);

            ConstructorInfo fourWayXtor = _fourWayJoint.GetConstructor(
              BindingFlags.Instance | BindingFlags.Public, null,
              CallingConventions.HasThis, types, null);

            for (int i = 0; i < jcs.Count; ++i)
            {
                var joint = tenonXtor.Invoke(new object[] { beams, jcs[i] }) as Joint;
                joints.Add(joint);
            }
            int c = 0;

            foreach (var jc in jcs)
            {
                Joint joint = null;

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
                                    joint = cornerXtor.Invoke(new object[] { beams, jc }) as CornerJoint;
                                else if (t0 * t1 < Math.Cos(BranchThreshold))
                                    joint = branchXtor.Invoke(new object[] { beams, jc }) as BranchJoint;
                                else
                                    joint = spliceXtor.Invoke(new object[] { beams, jc }) as SpliceJoint;
                                break;
                            case (1):
                                //type = "TenonJoint";
                                joint = tenonXtor.Invoke(new object[] { beams, jc.Parts[1], jc.Parts[0] }) as TenonJoint;
                                break;
                            case (2):
                                //type = "TenonJoint";
                                joint = tenonXtor.Invoke(new object[] { beams, jc.Parts[0], jc.Parts[1] }) as TenonJoint;
                                break;
                            case (3):
                                //type = "CrossJoint";
                                joint = crossXtor.Invoke(new object[] { beams, jc }) as CrossJoint;
                                break;
                        }
                        break;
                    // The joint has 3 members
                    case (3):
                        joint = vbeamXtor.Invoke(new object[] { beams, jc }) as VBeamJoint;
                        break;
                    // The joint has 4 members
                    case (4):
                        joint = fourWayXtor.Invoke(new object[] { beams, jc }) as FourWayJoint;
                        break;
                    default:
                        break;
                }
                joints.Add(joint);
            }


            return joints;
        }
    }


    public partial class JointConstructor
    {
        public Func<TenonJoint, bool> ProcessTenonJoint;
        public Func<SpliceJoint, bool> ProcessSpliceJoint;
        public Func<CornerJoint, bool> ProcessCornerJoint;
        public Func<CrossJoint, bool> ProcessCrossJoint;
        public Func<VBeamJoint, bool> ProcessVBeamJoint;
        public Func<FourWayJoint, bool> ProcessFourWayJoint;
        public Func<BranchJoint, bool> ProcessBranchJoint;

        private Type _FWJ;
        public Type FWJ
        {
            set
            {
                if (value.IsSubclassOf(typeof(FourWayJoint)))
                {
                    _FWJ = value;
                }
                else
                    throw new ArgumentException("Type needs to inherit from FourWayJoint.");
            }
            get
            {
                return _FWJ;
            }
        }

        public JointConstructor()
        {
            ProcessTenonJoint = DefaultTenonJoint;
            ProcessSpliceJoint = DefaultSpliceJoint;
            ProcessCornerJoint = DefaultCornerJoint;
            ProcessCrossJoint = DefaultCrossJoint;
            ProcessVBeamJoint = DefaultVBeamJoint;
            ProcessFourWayJoint = DefaultFourWayJoint;
            ProcessBranchJoint = DefaultBranchJoint;
        }

        public void ProcessJoints(List<Joint> joints)
        {
            var types = new Type[] { typeof(FourWayJoint) };
            ConstructorInfo constructorInfoObj = FWJ.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public, null,
                CallingConventions.HasThis, types, null);


            for (int i = 0; i < joints.Count; ++i)
            {
                if (joints[i] == null) throw new Exception(string.Format("JointConstructor.ProcessJoints(): Null joint at index {0}", i));
                try
                {
                    if (joints[i] is TenonJoint)
                    {
                        var tj = joints[i] as TenonJoint;
                        var jgeo = ProcessTenonJoint.Invoke(tj);
                    }
                    else if (joints[i] is CrossJoint)
                    {
                        var cj = joints[i] as CrossJoint;
                        var jgeo = ProcessCrossJoint.Invoke(cj);

                    }
                    else if (joints[i] is FourWayJoint)
                    {
                        var fj = joints[i] as FourWayJoint;
                        var jgeo = ProcessFourWayJoint.Invoke(fj);

                        constructorInfoObj.Invoke(new object[] { fj });

                    }
                    else if (joints[i] is VBeamJoint)
                    {
                        var vj = joints[i] as VBeamJoint;
                        var jgeo = ProcessVBeamJoint.Invoke(vj);

                    }
                    else if (joints[i] is SpliceJoint)
                    {
                        var sj = joints[i] as SpliceJoint;
                        var jgeo = ProcessSpliceJoint.Invoke(sj);
                    }
                    else if (joints[i] is CornerJoint)
                    {
                        var cj = joints[i] as CornerJoint;
                        var jgeo = ProcessCornerJoint.Invoke(cj);
                    }
                    else if (joints[i] is BranchJoint)
                    {
                        var bj = joints[i] as BranchJoint;
                        var jgeo = ProcessBranchJoint.Invoke(bj);
                    }
                }
                catch (Exception e)
                {
                    throw new Exception(string.Format("{0} :: {1}", e.Message, e.StackTrace));
                }
            }
        }
    }
}
