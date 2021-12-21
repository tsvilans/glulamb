using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;

namespace GluLamb.Joints
{
    public partial class JointConstructor
    {
        public Func<TenonJoint, bool> ProcessTenonJoint;
        public Func<SpliceJoint, bool> ProcessSpliceJoint;
        public Func<CornerJoint, bool> ProcessCornerJoint;
        public Func<CrossJoint, bool> ProcessCrossJoint;
        public Func<VBeamJoint, bool> ProcessVBeamJoint;
        public Func<FourWayJoint, bool> ProcessFourWayJoint;
        public Func<BranchJoint, bool> ProcessBranchJoint;

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
