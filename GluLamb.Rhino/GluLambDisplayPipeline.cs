using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino;

namespace GluLamb
{
    public class SampleCsDrawMeshConduit : Rhino.Display.DisplayConduit
    {
        public Rhino.Geometry.Mesh Mesh { get; set; }

        protected override void CalculateBoundingBox(Rhino.Display.CalculateBoundingBoxEventArgs e)
        {
            if (null != Mesh)
            {
                e.IncludeBoundingBox(Mesh.GetBoundingBox(false));
            }
        }

        protected override void PostDrawObjects(Rhino.Display.DrawEventArgs e)
        {
            if (null != Mesh)
            {
                Rhino.Display.DisplayMaterial material = new Rhino.Display.DisplayMaterial();
                material.IsTwoSided = true;
                material.Diffuse = System.Drawing.Color.Blue;
                material.BackDiffuse = System.Drawing.Color.Red;
                e.Display.EnableLighting(true);
                e.Display.DrawMeshShaded(Mesh, material);
                e.Display.DrawMeshWires(Mesh, System.Drawing.Color.Black);
            }
        }
    }
}
