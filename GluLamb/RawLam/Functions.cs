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

                    var lam = new Lamella(g.Id, g, g.Data.LamHeight, x, y);
                    //lam.StackPositionX = x;
                    //lam.StackPositionY = y;

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

    /*
     Glulam g = ...
     Log l = ...
     var lamellas = Functions.ExtractUnbentLamellas(g, resolution);
     var sh = new SortingHat();
     sh.AddBoards(l.Boards);
     sh.AddLamellas(lamellas);

     sh.MatchLamellasAndBoards();
     // profit
     
     */

    /// <summary>
    /// Sort boards and lamellas, find appropriate boards for each lamella.
    /// </summary>
    public class SortingHat
    {
        public List<Log> Logs;
        public List<GluLamb.Glulam> Glulams;

        public Dictionary<Board, HashSet<Lamella>> BoardMatches;
        public Dictionary<Lamella, HashSet<Board>> LamellaMatches;

        /* -- Parameters -- */
        double MaxExtraThickness = 3.0;

        public SortingHat()
        {
            Logs = new List<Log>();
            Glulams = new List<GluLamb.Glulam>();
        }

        public void AddBoards(IEnumerable<Board> boards)
        {
            foreach (var brd in boards)
                BoardMatches.Add(brd, new HashSet<Lamella>());
        }

        public void AddLamellas(IEnumerable<Lamella> lamellas)
        {
            foreach (var lam in lamellas)
                LamellaMatches.Add(lam, new HashSet<Board>());
        }

        /// <summary>
        /// Matches boards and lamellas based on their thickness, with some defined tolerance for boards being thicker.
        /// </summary>
        public void MatchLamellasAndBoards()
        {
            // Clear already matched pairs
            foreach (var brd in BoardMatches.Keys)
                BoardMatches[brd].Clear();
            foreach (var lam in LamellaMatches.Keys)
                LamellaMatches[lam].Clear();

            // Match based on thickness
            foreach (var lam in LamellaMatches.Keys)
            {
                foreach (var brd in BoardMatches.Keys)
                {
                    if (brd.Thickness >= lam.Thickness && brd.Thickness <= lam.Thickness + MaxExtraThickness)
                    {
                        LamellaMatches[lam].Add(brd);
                        BoardMatches[brd].Add(lam);
                    }
                }
            }
        }
    }
}
#endif