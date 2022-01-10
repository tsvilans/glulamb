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
        public bool DefaultTenonJoint(TenonJoint tj)
        {
            return tj.Construct();
        }
        public bool DefaultSpliceJoint(SpliceJoint sj)
        {
            return sj.Construct();
        }
        public bool DefaultCornerJoint(CornerJoint cj)
        {
            return cj.Construct();
        }
        public bool DefaultCrossJoint(CrossJoint cj)
        {
            return cj.Construct();
        }
        public bool DefaultVBeamJoint(VBeamJoint vj)
        {
            return vj.Construct();
        }
        public bool DefaultFourWayJoint(FourWayJoint fj)
        {
            return fj.Construct();
        }
        public bool DefaultBranchJoint(BranchJoint bj)
        {
            return bj.Construct();
        }

    }
}
