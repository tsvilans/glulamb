using D2P_Core;
using D2P_Core.Interfaces;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace G2PComponents
{
    public struct PlacedConnector
    {
        public Connector Connector;
        public BoxSide Side;
    }

    public partial class Connector
    {
        public string Name { get; set; }
        public Line Axis { get; set; }
        public double Diameter { get; set; }

        public Connector(Line axis, double diameter, string name = "Connector")
        {
            Name = name;
            Axis = axis;
            Diameter = diameter;
        }

        public Cylinder ToCylinder()
        {
            var plane = new Plane(Axis.From, Axis.Direction);
            var circle = new Circle(plane, Diameter * 0.5);
            return new Cylinder(circle, Axis.Length);
        }

        public static (List<PlacedConnector>, bool isDoubleSided) IntersectConnectorsRay(
                IComponent component,
                List<Connector> connectors,
                double breakthroughEpsilon = 0.5,
                bool compensateTilt = true,
                double startDistance = 0,
                double breakthroughDistance = 0,
                bool detailed = false,
                RhinoDoc doc = null,
                bool avoidBottom = false)
        {
            // --- Get geometry ---
            GeometryBase geometry = null;
            if (detailed)
                geometry = Utility.GetMember(component, "DetailedGeometry", doc).FirstOrDefault();
            geometry ??= Utility.GetMember(component, "Geometry", doc).FirstOrDefault();

            if (geometry == null)
                return (new List<PlacedConnector>(), false);

            return IntersectConnectorsRay(
                component.Label.Plane,
                geometry,
                connectors,
                breakthroughEpsilon,
                compensateTilt,
                startDistance,
                breakthroughDistance,
                detailed,
                doc,
                avoidBottom);
        }

        public static (List<PlacedConnector>, bool isDoubleSided) IntersectConnectorsRay(
                Plane baseplane,
                GeometryBase geometry,
                List<Connector> connectors,
                double breakthroughEpsilon = 0.5,
                bool compensateTilt = true,
                double startDistance = 0,
                double breakthroughDistance = 0,
                bool detailed = false,
                RhinoDoc doc = null,
                bool avoidBottom = false)
        {
            doc ??= RhinoDoc.ActiveDoc;
            double tol = doc?.ModelAbsoluteTolerance ?? 1e-3;

            var placed = new List<PlacedConnector>();
            bool isDoubleSided = false;

            var target = HitTarget.From(geometry, tol);
            if (target == null)
            {
                RhinoApp.WriteLine($"-- WARNING: Can't intersect connectors with a {geometry?.ObjectType}.");
                return (placed, false);
            }

            geometry.GetBoundingBox(baseplane, out Box box);
            BoundingBox worldBounds = geometry.GetBoundingBox(true);
            double reach = worldBounds.Diagonal.Length;

            foreach (var connector in connectors)
            {
                Line axis = connector.Axis;
                double length = axis.Length;
                double r = connector.Diameter * 0.5;
                if (length < tol) continue;

                // --- Bounding box rejection (now including the radius) ---
                var connectorBounds = new BoundingBox(new[] { axis.From, axis.To });
                connectorBounds.Inflate(r);
                if (!Overlaps(connectorBounds, worldBounds)) continue;

                Vector3d d = axis.Direction;
                d.Unitize();

                // --- Material along the axis line, as distances from axis.From ---
                var spans = target.MaterialSpans(axis.From, d, reach + length)
                    .Where(s => s.T1 > -breakthroughEpsilon && s.T0 < length + breakthroughEpsilon)
                    .ToList();
                if (spans.Count == 0) continue;

                double entry = spans[0].T0;               // first surface the axis crosses into material
                double exit = spans[spans.Count - 1].T1;  // last surface it leaves through

                double depth = spans.Sum(s => Math.Max(0, Math.Min(s.T1, length) - Math.Max(s.T0, 0)));
                if (depth < breakthroughEpsilon) continue; // only grazes the part

                bool startThru = entry >= -breakthroughEpsilon;     // start is outside, on, or just under the surface
                bool endThru = exit <= length + breakthroughEpsilon;

                // --- Choose where drilling starts (sA) and stops (sB) ---
                double sA, sB;
                bool exitOpen; // does the drill come out the other side?

                if (startThru)
                {
                    sA = entry;
                    sB = endThru ? exit : length;
                    exitOpen = endThru;
                }
                else if (endThru)
                {
                    sA = exit;
                    sB = 0;
                    exitOpen = false;
                }
                else
                {
                    RhinoApp.WriteLine($"-- WARNING: Connector {connector.Name} starts and stops within material!");
                    // Drill from whichever surface is closer.
                    bool fromStart = -entry <= exit - length;
                    sA = fromStart ? entry : exit;
                    sB = fromStart ? length : 0;
                    exitOpen = false;
                }

                Point3d p0 = axis.From + d * sA;
                Point3d p1 = axis.From + d * sB;
                Vector3d drill = p1 - p0;
                if (!drill.Unitize()) continue;

                // --- Steep connectors: prefer drilling from the top ---
                bool steep = Math.Abs(baseplane.ZAxis * drill) >= Math.Cos(Math.PI * 0.25);
                if (steep && drill * baseplane.ZAxis > 0)
                {
                    if (exitOpen)
                    {
                        (p0, p1) = (p1, p0);
                        drill.Reverse();
                    }
                    else
                    {
                        isDoubleSided = true; // blind, and only reachable from below
                    }
                }

                // --- Tilt compensation against the real surface at each end ---
                if (compensateTilt)
                {
                    p0 -= drill * TiltOffset(target.NormalAt(p0), drill, r);
                    if (exitOpen)
                        p1 += drill * TiltOffset(target.NormalAt(p1), drill, r);
                }

                if (exitOpen)
                    p1 += drill * breakthroughDistance;

                p0 -= drill * startDistance;

                // --- Side is still a bounding-box concept: classify by where the drill enters the box ---
                var newAxis = new Line(p0, p1);
                var side = BoxSide.Unknown;
                if (Intersection.LineBox(newAxis, box, tol, out Interval boxHit))
                    side = Utility.GetBoxSide(newAxis.PointAt(boxHit.T0), box);

                placed.Add(new PlacedConnector
                {
                    Connector = new Connector(newAxis, connector.Diameter, connector.Name),
                    Side = side
                });
            }

            return (placed, isDoubleSided);
        }

        /// <summary>
        /// How far the rim of a cylinder of radius r reaches past the point where its axis meets
        /// a surface with normal n. theta is clamped so near-parallel hits stay finite.
        /// </summary>
        static double TiltOffset(Vector3d n, Vector3d drill, double r)
        {
            if (!n.Unitize())
                return r; // unknown normal: assume 45 degrees

            double dot = Math.Min(1, Math.Abs(n * drill));
            double theta = Math.Min(Math.Acos(dot), RhinoMath.ToRadians(80));
            return r * Math.Tan(theta);
        }

        // Overlaps(BoundingBox, BoundingBox) is defined in ConnectorCut.cs.

        /// <summary>Line intersections, inside tests and normals for a Brep (exact) or a Mesh.</summary>
        sealed class HitTarget
        {
            readonly Brep _brep;
            readonly Mesh _mesh;
            readonly double _tol;

            HitTarget(Brep brep, Mesh mesh, double tol)
            {
                _brep = brep;
                _mesh = mesh;
                _tol = tol;
            }

            public static HitTarget From(GeometryBase geometry, double tol)
            {
                switch (geometry)
                {
                    case Brep brep:
                        return new HitTarget(brep, null, tol);
                    case Extrusion extrusion:
                        return new HitTarget(extrusion.ToBrep(true), null, tol);
                    case Mesh mesh:
                        var m = mesh.DuplicateMesh();
                        m.FaceNormals.ComputeFaceNormals();
                        return new HitTarget(null, m, tol);
                    default:
                        return null;
                }
            }

            /// <summary>Sorted distances along d from origin where the infinite line crosses the surface.</summary>
            public List<double> Crossings(Point3d origin, Vector3d d, double reach)
            {
                var line = new Line(origin - d * reach, origin + d * reach);
                Point3d[] points;

                if (_brep != null)
                    Intersection.CurveBrep(new LineCurve(line), _brep, _tol, out _, out points);
                else
                    points = Intersection.MeshLine(_mesh, line, out _);

                var result = new List<double>();
                foreach (double x in (points ?? Array.Empty<Point3d>()).Select(p => (p - origin) * d).OrderBy(x => x))
                    if (result.Count == 0 || x - result[result.Count - 1] > 10 * _tol) // edge hits come back twice
                        result.Add(x);
                return result;
            }

            /// <summary>
            /// Intervals (distances along d) of the line that lie inside material. Each gap between
            /// consecutive crossings is tested at its midpoint, so duplicate or grazing hits don't
            /// flip inside/outside the way pure parity would.
            /// </summary>
            public List<Interval> MaterialSpans(Point3d origin, Vector3d d, double reach)
            {
                var xs = Crossings(origin, d, reach);
                var spans = new List<Interval>();

                for (int i = 0; i + 1 < xs.Count; i++)
                {
                    double mid = 0.5 * (xs[i] + xs[i + 1]);
                    if (!IsInside(origin + d * mid, xs, mid)) continue;

                    if (spans.Count > 0 && xs[i] - spans[spans.Count - 1].T1 < 10 * _tol)
                        spans[spans.Count - 1] = new Interval(spans[spans.Count - 1].T0, xs[i + 1]); // split face: merge
                    else
                        spans.Add(new Interval(xs[i], xs[i + 1]));
                }
                return spans;
            }

            bool IsInside(Point3d p, List<double> crossings, double s)
            {
                if (_brep != null && _brep.IsSolid)
                    return _brep.IsPointInside(p, _tol, false);
                if (_mesh != null && _mesh.IsClosed)
                    return _mesh.IsPointInside(p, _tol, false);

                // Open geometry: fall back to parity along the line.
                return crossings.Count(x => x > s) % 2 == 1;
            }

            public Vector3d NormalAt(Point3d p)
            {
                if (_brep != null)
                {
                    if (_brep.ClosestPoint(p, out _, out _, out _, out _, 0, out Vector3d n))
                        return n;
                    return Vector3d.Zero;
                }

                MeshPoint mp = _mesh.ClosestMeshPoint(p, 0);
                return mp != null ? (Vector3d)_mesh.FaceNormals[mp.FaceIndex] : Vector3d.Zero;
            }
        }


        public static Brep CutConnectors(IComponent component, IEnumerable<Connector> connectors, RhinoDoc doc = null, double tolerance = 1e-3)
        {
            // --- Create cutter breps ---
            var cutters = connectors
                .Select(c => c.ToCylinder().ToBrep(true, true))
                .ToList();

            GeometryBase brep = null;

            // --- Get geometry ---
            var detailedGeometry = Utility.GetMember(component, "DetailedGeometry", doc).FirstOrDefault();
            if (detailedGeometry != null)
            {
                brep = detailedGeometry;
            }
            else
            {
                var geometry = Utility.GetMember(component, "Geometry", doc).FirstOrDefault();
                if (geometry != null)
                {
                    brep = geometry;
                }
            }

            if (brep == null)
                return null;

            // --- Handle extrusion ---
            if (brep is Extrusion extrusion)
                brep = extrusion.ToBrep(true);

            if (cutters.Count < 1)
                return brep as Brep;

            // --- Boolean difference ---
            var result = Brep.CreateBooleanDifference(
                new List<Brep> { brep as Brep },
                cutters,
                tolerance
            );

            if (result != null && result.Length > 0)
                return result.First();

            return null;
        }
        public static List<Connector> GetAllConnectors(
            RhinoDoc doc = null,
            List<string> layerNames = null,
            int precision = 3,
            bool sublayers = true)
        {
            doc ??= RhinoDoc.ActiveDoc;
            layerNames ??= new List<string> { "Connectors" };

            var connectors = new List<Connector>();

            var layers = new List<Layer>();

            foreach (var layerName in layerNames)
            {
                var layer = doc.Layers.FindName(layerName);

                if (layer == null) continue;

                layers.Add(layer);
                if (sublayers)
                {
                    var children = layer.GetChildren(true);
                    if (children != null)
                        layers.AddRange(children);
                }
            }

            foreach (Layer layer in layers)
            {
                var objects = doc.Objects.FindByLayer(layer);

                if (objects == null || objects.Length == 0)
                {
                    RhinoApp.WriteLine($"-- Didn't find any connectors on layer {layer.Name}.");
                    continue;
                }

                RhinoApp.WriteLine($"-- Found {objects.Length} connectors on layer {layer.Name}.");

                int counter = 0;

                foreach (var obj in objects)
                {
                    var geometry = obj.Geometry;
                    Brep brep = null;

                    // --- Handle extrusion ---
                    if (geometry is Extrusion extrusion)
                        brep = extrusion.ToBrep();
                    else
                        brep = geometry as Brep;

                    if (brep == null)
                        continue;

                    foreach (BrepFace face in brep.Faces)
                    {
                        if (face.TryGetFiniteCylinder(out Cylinder cyl, 1e-2))
                        {
                            // Construct axis
                            var axis = new Line(
                                cyl.BasePlane.Origin + cyl.Height1 * cyl.BasePlane.ZAxis,
                                cyl.BasePlane.Origin + cyl.Height2 * cyl.BasePlane.ZAxis
                            );

                            double diameter = Math.Round(cyl.Radius * 2.0, precision);

                            var conn = new Connector(axis, diameter, obj.Name);
                            connectors.Add(conn);

                            counter++;
                            break; // Only one cylinder per object (same as Python)
                        }
                    }
                }

                RhinoApp.WriteLine($"-- Processed {counter} connectors.");
            }

            return connectors;
        }
    }

}
