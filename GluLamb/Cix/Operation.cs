using GluLamb.Projects.HHDAC22;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Cix
{
    public abstract class Operation : ITransformable, ICix, IHasGeometry, ICloneable
    {
        public bool Enabled = true;
        public string Name = "Operation";
        public int Id = 0;

        public abstract void ToCix(List<string> cix, string prefix = "");
        public abstract void Transform(Transform xform);

        public abstract List<object> GetObjects();

        public abstract object Clone();

        public abstract bool SimilarTo(Operation op, double epsilon);

        public abstract BoundingBox Extents(Plane plane);
    }
}
