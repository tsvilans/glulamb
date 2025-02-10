using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Cix
{
    public interface ITransformable
    {
        void Transform(Transform xform);
    }

    public interface IHasGeometry
    {
        List<object> GetObjects();
    }

    public interface ICix
    {
        void ToCix(List<string> cix, string prefix = "");
    }
}
