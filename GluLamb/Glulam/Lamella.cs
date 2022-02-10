using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;
using GluLamb;

namespace GluLamb
{ 
    public class Lamella
    {
        public Guid GlulamId;
        public Glulam Glulam = null;

        private int stackPositionX;
        private int stackPositionY;
        public double Width;
        public double Thickness;
        public double Length;


        public int StackPositionX
        {
            get { return stackPositionX; }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException("Stack position cannot be negative.");
                stackPositionX = value;
            }
        }
        public int StackPositionY
        {
            get { return stackPositionY; }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException("Stack position cannot be negative.");
                stackPositionY = value;
            }
        }

        public Plane Plane; // Local lamella space to glulam stack space
        public Mesh Mesh;

        public Lamella() : this(Guid.NewGuid())
        {
        }

        public Lamella(Guid glulam_id) : this(glulam_id, null)
        {
        }

        public Lamella(Glulam glulam) : this(glulam.Id, glulam)
        {
        }

        public Lamella(Guid glulam_id, Glulam glulam, double thickness = 10.0, int spx = -1, int spy = -1)
        {
            Thickness = 10.0;
            stackPositionX = spx;
            stackPositionY = spy;
            Glulam = glulam;
            GlulamId = glulam_id;
        }

        public static List<Lamella> ExtractUnbentLamellas(Glulam g, double resolution = 50.0, double extra_tolerance = 0.0)
        {
            var lams = new List<Lamella>();
            var lamcrvs = g.GetLamellaeCurves();

            double hw = g.Width / 2;
            double hh = g.Height / 2;

            double lw = g.Data.LamWidth;
            double lh = g.Data.LamHeight;

            double hlw = lw / 2;
            double hlh = lh / 2;

            int l = 0;
            for (int x = 0; x < g.Data.NumWidth; ++x)
            {
                for (int y = 0; y < g.Data.NumHeight; ++y)
                {
                    var length = lamcrvs[l].GetLength();
                    var lam = new Lamella(g.Id, g, lh, x, y);
                    Mesh lmesh = GluLamb.Utility.Create3dMeshGrid(lw + extra_tolerance, lh, length + extra_tolerance, resolution);

                    lam.Mesh = lmesh;
                    lam.Length = length;
                    lam.Width = g.Data.LamWidth;
                    //lam.Thickness = g.Data.LamHeight;
                    //lam.StackPositionX = x;
                    //lam.StackPositionY = y;
                    lam.Plane = new Plane(
                      new Point3d(
                          -hw + lw * x + hlw,
                          -hh + lh * y + hlh,
                          0),
                      Vector3d.XAxis,
                      Vector3d.YAxis);
                    lams.Add(lam);
                    l++;

                }
            }

            return lams;
        }
    }
}
