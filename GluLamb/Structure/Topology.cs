using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry;

namespace GluLamb
{
    public struct Pair
    {
        public int A, B;
        public double tA, tB;
    }

    public class Topology
    {
        public class PairComparer : EqualityComparer<int[]>
        {
            public override bool Equals(int[] x, int[] y)
            {
                return ((x[0] == y[0] && x[1] == y[1]) || (x[1] == y[0] && x[0] == y[1]));
            }

            public override int GetHashCode(int[] obj)
            {
                if (obj[0] > obj[1])
                    return (obj[0]) ^ (obj[1] << 32);
                return (obj[1]) ^ (obj[0] << 32);
            }
        }

        public static List<Pair> FindCurvePairs(Dictionary<int, Curve> curves, double tolerance, double overlapTolerance)
        {
            var pairs = new List<Pair>();

            var keys = curves.Keys.ToList<int>();

            for (int i = 0; i < keys.Count - 1; ++i)
            {
                var crv0 = curves[keys[i]];
                if (crv0 == null) continue;

                for (int j = i + 1; j < keys.Count; ++j)
                {
                    var crv1 = curves[keys[j]];
                    if (crv1 == null) continue;

                    var intersections = Rhino.Geometry.Intersect.Intersection.CurveCurve(crv0, crv1, tolerance, overlapTolerance);

                    if (intersections.Count > 0)
                    {
                        foreach (var intersection in intersections)
                        {
                            if (intersection.IsPoint)
                                pairs.Add(new Pair { A = keys[i], B = keys[j], tA = intersection.ParameterA, tB = intersection.ParameterB });
                            else if (intersection.IsOverlap)
                                pairs.Add(new Pair { A = keys[i], B = keys[j], tA = intersection.OverlapA.Mid, tB = intersection.OverlapB.Mid });

                        }
                    }
                }
            }

            return pairs;
        }
    }
}
