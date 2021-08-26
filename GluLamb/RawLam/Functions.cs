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

    public class BoardPlacement
    {
        public BoardPlacement(Polyline poly)
        {
            Outline = poly;
            BoundingBox = poly.BoundingBox;

            RangeX = BoundingBox.Max.X - BoundingBox.Min.X;
            RangeY = BoundingBox.Max.Y - BoundingBox.Min.Y;

            MinX = BoundingBox.Min.X;
            MinY = BoundingBox.Min.Y;

            Lamellas = new List<Rectangle3d>();
            Full = false;
        }

        public List<Rectangle3d> Lamellas;
        public bool Full;
        public BoundingBox BoundingBox;
        public Polyline Outline;

        public double RangeX;
        public double RangeY;

        public double MinX;
        public double MinY;

    }

    /// <summary>
    /// Class to allocate Lamellas to Boards. IN PROGRESS. Needs to be integrated with SortingHat.
    /// </summary>
    public class LamellaAllocator
    {
        public bool UseRotation;
        private Random rnd;
        public LamellaAllocator()
        {
            rnd = new Random();
        }

        public void Run(List<Polyline> B, Rectangle3d L)
        {
            /* ---- Initialize ---- */
            var boards = new List<BoardPlacement>();

            for (int i = 0; i < B.Count; ++i)
            {
                boards.Add(new BoardPlacement(B[i]));
            }

            var lamellas = new List<Rectangle3d>();
            int N = 1000;

            for (int i = 0; i < N; ++i)
            {
                lamellas.Add(new Rectangle3d(Plane.WorldXY, L.Width, L.Height + rnd.NextDouble() * 200.0));
                //lamellas.Add(L);
            }

            var placed = new List<Rectangle3d>();
            var debug = new List<object>();

            int tries = 200;
            bool flag = false;
            int index = -1;

            double rotation = 0.1;

            // Metrics
            int counter = 0;
            int num_lamellas_placed = 0;

            for (int i = 0; i < lamellas.Count; ++i)
            {
                Rectangle3d lam = lamellas[i];

                for (int j = 0; j < tries; ++j)
                {
                    counter++;

                    index = rnd.Next(0, boards.Count);
                    var board = boards[index];
                    if (board.Full) continue;

                    lam = lamellas[i];

                    double xRange = board.RangeX - lam.Width;
                    double yRange = board.RangeY - lam.Height;

                    flag = false;
                    var x = rnd.NextDouble() * xRange + board.MinX;
                    var y = rnd.NextDouble() * yRange + board.MinY;

                    //Print("range {0} {1}", xRange, yRange);
                    //Print("xy {0} {1}", x, y);
                    if (UseRotation)
                        lam.Transform(Transform.Rotation(rnd.NextDouble() * rotation, Point3d.Origin));
                    lam.Transform(Transform.Translation(new Vector3d(x, y, 0)));


                    debug.Add(lam);

                    if (!IsInside(boards[index].Outline.ToNurbsCurve(), lam))
                    {
                        //Print("Outside!");
                        continue;
                    }

                    foreach (var p in boards[index].Lamellas)
                    {
                        if (IsOverlapping(p, lam))
                        {
                            flag = true;
                            break;
                        }
                    }

                    if (!flag)
                    {
                        boards[index].Lamellas.Add(lam);
                        num_lamellas_placed++;
                        break;
                    }

                }

                if (flag)
                {
                    boards[index].Full = true;
                    break;
                }
            }
        }
        public bool IsOverlapping(Rectangle3d r1, Rectangle3d r2)
        {
            if (!UseRotation) // Use simple, axis-aligned method
            {
                // If one rectangle is on left side of other
                if (r1.BoundingBox.Min.X >= r2.BoundingBox.Max.X ||
                  r2.BoundingBox.Min.X >= r1.BoundingBox.Max.X)
                    return false;

                // If one rectangle is above other
                if (r1.BoundingBox.Min.Y >= r2.BoundingBox.Max.Y ||
                  r2.BoundingBox.Min.Y >= r1.BoundingBox.Max.Y)
                    return false;

                return true;
            }

            var res = Rhino.Geometry.Intersect.Intersection.CurveCurve(r1.ToNurbsCurve(), r2.ToNurbsCurve(), 0.1, 0.1);
            return res.Count > 0;
        }

        public bool IsInside(Curve crv, Rectangle3d r)
        {
            for (int i = 0; i < 4; ++i)
                if (crv.Contains(r.Corner(i), Plane.WorldXY, 0.1) != PointContainment.Inside)
                    return false;
            return true;
        }
    }
}
#endif