using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Connections
{
    public class CrossingLapJoint : Connection
    {
        public CrossingLapJoint(Element eleA, Element eleB, double positionA, double positionB, string name = "") : base(eleA, eleB, positionA, positionB, name)
        {

        }
    }
}
