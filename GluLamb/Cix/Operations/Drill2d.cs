using GluLamb.Projects.HHDAC22;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Cix.Operations
{
    public class Drill2d : ITransformable, IHasGeometry, ICloneable
    {
        public Point3d Position;
        public double Diameter;
        public double Depth;

        public Drill2d(Point3d position, double diameter = 0, double depth = 0)
        {
            Position = position;
            Diameter = diameter;
            Depth = depth;
        }

        public object Clone()
        {
            return new Drill2d(Position, Diameter, Depth);
        }

        public List<object> GetObjects()
        {
            return new List<object> { Position };
        }

        public void Transform(Transform xform)
        {
            Position.Transform(xform);
        }


    }
}
