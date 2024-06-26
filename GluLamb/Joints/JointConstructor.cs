﻿using System;
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

        public JointSolver()
        {
            TenonJoint = typeof(TenonJoint);
            CrossJoint = typeof(CrossJoint);
            BranchJoint = typeof(BranchJoint);
            SpliceJoint = typeof(SpliceJoint);
            CornerJoint = typeof(CornerJoint);
            FourWayJoint = typeof(FourWayJoint);
            VBeamJoint = typeof(VBeamJoint);
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

            //for (int i = 0; i < jcs.Count; ++i)
            //{
            //    var joint = tenonXtor.Invoke(new object[] { beams, jcs[i] }) as Joint;
            //    joints.Add(joint);
            //}
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
                        Rhino.RhinoApp.WriteLine("Failed to make joint out of condition: {0}", jc);
                        break;
                }

                if (joint != null)
                    joints.Add(joint);
            }

            return joints;
        }
    }
}
