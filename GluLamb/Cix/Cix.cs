using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Cix
{
    public static class Cix
    {
        public static Transform ToZDown
        {
            get
            {
                if (_toZDown == Transform.Unset)
                {
                    _toZDown = Rhino.Geometry.Transform.Identity;
                    _toZDown.M22 = -1;

                }
                return _toZDown;
            }
        }

        private static Transform _toZDown = Transform.Unset;
    }
}
