using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Joints
{
    public interface ITenonJoint
    {
        double TenonDepth { get; set; }
        double TenonWidth { get; set; }
        double TenonThickness { get; set; }
    }
}
