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

        public static Plane ToCixPlane(Plane plane, out double angle, Plane? reference = null)
        {

            Plane _ref = Plane.WorldXY;
            if (reference.HasValue)
                _ref = reference.Value;

            // In case normal is parallel to reference normal
            var dot = plane.ZAxis * _ref.ZAxis;
            Console.WriteLine($"dot={dot:0.00}");

            var sign = dot < 0 ? -1 : 1;

            Vector3d xaxis = Vector3d.Unset;

            if (Math.Abs(dot) >= 1.0)
            {
                xaxis = dot >= 1.0 ? _ref.XAxis : -_ref.XAxis;
                angle = Vector3d.VectorAngle(xaxis, plane.Normal) * sign;
            }
            else
            {
                xaxis = Vector3d.CrossProduct(plane.ZAxis, _ref.ZAxis);
                var projected = new Vector3d(plane.Normal - (_ref.Normal * Vector3d.Multiply(_ref.Normal, plane.Normal)));
                angle = Vector3d.VectorAngle(projected, plane.Normal) * sign;
            }

            var yaxis = Vector3d.CrossProduct(xaxis, plane.ZAxis);

            return new Plane(plane.Origin, xaxis, yaxis);
            /*
             * Double-check the above before committing.
             * 
            Plane _ref = Plane.WorldXY;
            if (reference.HasValue)
                _ref = reference.Value;


            // In case normal is parallel to reference normal
            var dot = plane.ZAxis * _ref.ZAxis;
            var xaxis = dot >= 1.0 ? _ref.XAxis : dot <= -1 ? -_ref.XAxis : Vector3d.CrossProduct(plane.ZAxis, _ref.ZAxis);

            var projected = new Vector3d(plane.Normal - (_ref.Normal * Vector3d.Multiply(_ref.Normal, plane.Normal)));

            angle = Vector3d.VectorAngle(projected, plane.Normal);

            var yaxis = Vector3d.CrossProduct(xaxis, plane.ZAxis);

            return new Plane(plane.Origin, xaxis, yaxis);
            */
        }
    }

}
