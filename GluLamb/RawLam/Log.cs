#if RAWLAM


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;

namespace RawLam
{
    public static class Functions
    {
        public static List<Lamella> ExtractUnbentLamellas(GluLamb.Glulam g, double resolution = 50.0)
        {
            var lams = new List<Lamella>();
            var lamcrvs = g.GetLamellaeCurves();

            double hw = g.Width / 2;
            double hh = g.Height / 2;

            double lw = g.Data.LamWidth;
            double lh = g.Data.LamHeight;

            double hlw = g.Data.LamWidth / 2;
            double hlh = g.Data.LamHeight / 2;

            int Nx = (int)Math.Ceiling(g.Data.LamWidth / resolution);
            int Ny = (int)Math.Ceiling(g.Data.LamHeight / resolution);

            double stepX = g.Data.LamWidth / Nx;
            double stepY = g.Data.LamHeight / Ny;

            int Nloop = Nx * 2 + Ny * 2; // Num verts in a loop

            Nx++; Ny++;

            int l = 0;
            for (int x = 0; x < g.Data.NumWidth; ++x)
            {
                for (int y = 0; y < g.Data.NumHeight; ++y)
                {
                    double length = lamcrvs[l].GetLength();

                    int Nz = (int)Math.Ceiling(length / resolution);
                    double stepZ = length / Nz;
                    Nz++;

                    var lam = new Lamella(g.Id);
                    lam.StackPositionX = x;
                    lam.StackPositionY = y;

                    Mesh lmesh = new Mesh();

                    // Make mesh data for body

                    for (int i = 0; i < Nz; ++i)
                    {
                        double posX = i * stepZ;

                        for (int j = 0; j < Ny; ++j)
                            lmesh.Vertices.Add(-hlw, -hlh + j * stepY, posX);

                        for (int k = 1; k < Nx; ++k)
                            lmesh.Vertices.Add(-hlw + k * stepX, -hlh + lh, posX);

                        for (int j = Ny - 2; j >= 0; --j)
                            lmesh.Vertices.Add(-hlw + lw, -hlh + j * stepY, posX);

                        for (int k = Nx - 2; k > 0; --k)
                            lmesh.Vertices.Add(-hlw + k * stepX, -hlh, posX);
                    }

                    for (int i = 0; i < Nz - 1; ++i)
                    {
                        for (int j = 0; j < Nloop - 1; ++j)
                        {
                            lmesh.Faces.AddFace(
                              (i + 1) * Nloop + j,
                              (i + 1) * Nloop + j + 1,
                              i * Nloop + j + 1,
                              i * Nloop + j
                            );
                        }
                        lmesh.Faces.AddFace(
                          (i + 1) * Nloop + Nloop - 1,
                          (i + 1) * Nloop,
                          i * Nloop,
                          i * Nloop + Nloop - 1
                        );
                    }

                    // Make mesh data for end faces

                    int c = lmesh.Vertices.Count;

                    for (int i = 0; i < Ny; ++i)
                        for (int j = 0; j < Nx; ++j)
                        {
                            lmesh.Vertices.Add(-hlw + stepX * j, -hlh + stepY * i, 0);
                        }

                    for (int i = 0; i < Ny - 1; ++i)
                        for (int j = 0; j < Nx - 1; ++j)
                        {
                            lmesh.Faces.AddFace(
                            c + Nx * i + j,
                            c + Nx * (i + 1) + j,
                            c + Nx * (i + 1) + j + 1,
                            c + Nx * i + j + 1
                            );
                        }

                    c = lmesh.Vertices.Count;

                    for (int i = 0; i < Ny; ++i)
                        for (int j = 0; j < Nx; ++j)
                        {
                            lmesh.Vertices.Add(-hlw + stepX * j, -hlh + stepY * i, length);
                        }

                    for (int i = 0; i < Ny - 1; ++i)
                        for (int j = 0; j < Nx - 1; ++j)
                        {
                            lmesh.Faces.AddFace(
                              c + Nx * i + j + 1,
                              c + Nx * (i + 1) + j + 1,
                              c + Nx * (i + 1) + j,
                              c + Nx * i + j
                              );
                        }

                    lam.Mesh = lmesh;
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

    public class Lamella
    {
        public Guid GlulamId;
        private int stackPositionX;
        private int stackPositionY;
        public int StackPositionX
        {
            get { return stackPositionX; }
            set { 
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

        public Lamella(Guid glulam_id)
        {
            GlulamId = glulam_id;
            stackPositionX = -1;
            stackPositionY = -1;
        }
    }


    public class Board
    {
        public Guid BoardId;
        public Guid LogId;

        public string Name;
        public List<Polyline> Top;
        public Polyline Centre;
        public List<Polyline> Bottom;
        public Plane Plane;
        public double Thickness;

        public Board(string name = "Board")
        {
            BoardId = Guid.NewGuid();
            Name = name;
            Top = new List<Polyline>();
            Centre = new Polyline();
            Bottom = new List<Polyline>();
            Plane = Plane.WorldXY;
            Thickness = 0;


        }

        public void Transform(Transform xform)
        {
            for (int i = 0; i < Top.Count; ++i)
            {
                Top[i].Transform(xform);
            }
            Centre.Transform(xform);

            for (int i = 0; i < Bottom.Count; ++i)
            {
                Bottom[i].Transform(xform);
            }
            Plane.Transform(xform);
        }

        public Board Duplicate()
        {
            var board = new Board()
            {
                Name = Name,
                Centre = Centre.Duplicate(),
                Plane = Plane,
                Thickness = Thickness

            };

            for (int i = 0; i < Top.Count; ++i)
            {
                board.Top.Add(Top[i].Duplicate());
            }

            for (int i = 0; i < Bottom.Count; ++i)
            {
                board.Bottom.Add(Bottom[i].Duplicate());
            }

            return board;
        }

    }


    public class Log
    {
        public Guid LogId;
        public string Name;
        public Mesh Mesh;
        public Plane BasePlane;

        public List<Board> Boards;

        public Log()
        {
            LogId = Guid.NewGuid();
            Boards = new List<Board>();
            BasePlane = Plane.WorldXY;
        }
        
        public void Transform(Transform xform)
        {
            Mesh.Transform(xform);
            BasePlane.Transform(xform);
            for (int i = 0; i < Boards.Count; ++i)
            {
                Boards[i].Transform(xform);
            }
        }

        public Log Duplicate()
        {
            var log = new Log() { Name = Name, BasePlane = BasePlane, Mesh = Mesh.DuplicateMesh() };
            for (int i = 0; i < Boards.Count; ++i)
            {
                log.Boards.Add(Boards[i].Duplicate());
            }

            return log;
        }
    }
}
#endif
