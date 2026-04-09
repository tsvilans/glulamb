using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace G2PComponents
{
    public static class BoxMating
    {
        public static bool FindMatingFaces(
            List<Rectangle3d> facesA,
            List<Rectangle3d> facesB,
            double angleTol,
            double distTol,
            out Rectangle3d bestA,
            out Rectangle3d bestB)
        {
            bestA = Rectangle3d.Unset;
            bestB = Rectangle3d.Unset;

            double minDist = double.MaxValue;

            foreach (var fa in facesA)
            {
                foreach (var fb in facesB)
                {
                    // 1. Parallel + facing check
                    double dot = fa.Plane.Normal * fb.Plane.Normal;

                    if (dot > -Math.Cos(angleTol))
                        continue; // not opposite enough

                    // 2. Distance between planes
                    double dist = Math.Abs(fa.Plane.DistanceTo(fb.Plane.Origin));
                    if (dist > distTol && dist > minDist)
                        continue;

                    // 3. Project rectangle A onto plane B
                    var projectedA = ProjectRectangleToPlane(fa, fb.Plane);

                    // 4. Convert both rectangles into Plane B coordinates (2D)
                    var polyA = ToPolyline2D(projectedA, fb.Plane);
                    var polyB = ToPolyline2D(fb, fb.Plane);

                    // 5. Check overlap
                    if (!PolygonsOverlap(polyA, polyB))
                        continue;

                    // 6. Keep smallest separation
                    if (dist < minDist)
                    {
                        minDist = dist;
                        bestA = fa;
                        bestB = fb;
                    }
                }
            }

            return bestA.IsValid && bestB.IsValid;
        }

        static Rectangle3d ProjectRectangleToPlane(Rectangle3d rect, Plane targetPlane)
        {
            Point3d[] projected = new Point3d[4];

            for (int i = 0; i < 4; i++)
            {
                projected[i] = targetPlane.ClosestPoint(rect.Corner(i));
            }

            return new Rectangle3d(targetPlane, projected[0], projected[2]);
        }

        static List<Point2d> ToPolyline2D(Rectangle3d rect, Plane plane)
        {
            var result = new List<Point2d>();

            for (int i = 0; i < 4; ++i)
            {
                plane.RemapToPlaneSpace(rect.Corner(i), out Point3d uv);
                result.Add(new Point2d(uv.X, uv.Y));
            }

            return result;
        }

        static bool PolygonsOverlap(List<Point2d> a, List<Point2d> b)
        {
            return !SeparatingAxisExists(a, b) && !SeparatingAxisExists(b, a);
        }

        static bool SeparatingAxisExists(List<Point2d> polyA, List<Point2d> polyB)
        {
            for (int i = 0; i < polyA.Count; i++)
            {
                var p1 = polyA[i];
                var p2 = polyA[(i + 1) % polyA.Count];

                Vector2d edge = p2 - p1;
                Vector2d axis = new Vector2d(-edge.Y, edge.X);
                axis.Unitize();

                double minA, maxA, minB, maxB;
                ProjectPolygon(polyA, axis, out minA, out maxA);
                ProjectPolygon(polyB, axis, out minB, out maxB);

                if (maxA < minB || maxB < minA)
                    return true; // gap found
            }

            return false;
        }

        static void ProjectPolygon(List<Point2d> poly, Vector2d axis, out double min, out double max)
        {
            min = max = poly[0].X * axis.X + poly[0].Y * axis.Y;

            foreach (var p in poly)
            {
                double d = p.X * axis.X + p.Y * axis.Y;
                if (d < min) min = d;
                if (d > max) max = d;
            }
        }
    }
}
