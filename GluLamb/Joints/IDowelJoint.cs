using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Joints
{
    public interface IDowelJoint
    {
        double DowelLength { get; set; }
        double DowelDiameter { get; set; }
        double DowelLengthExtra { get; set; }
        double DowelSideTolerance { get; set; }
    }
}
