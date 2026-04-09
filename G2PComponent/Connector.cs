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

    public class Connector
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
            doc ??= RhinoDoc.ActiveDoc;

            double epsilon = 1e-6;
            var intersectingConnectors = new List<PlacedConnector>();

            // --- Get geometry ---
            GeometryBase geometry = Utility.GetMember(component, "Geometry", doc).FirstOrDefault();

            if (geometry is Extrusion extrusion)
                geometry = extrusion.ToBrep();
            else if (geometry == null)
                return (new List<PlacedConnector>(), false);

            // --- Mesh ---
            var meshes = Mesh.CreateFromBrep((Brep)geometry, MeshingParameters.FastRenderMesh);
            var mesh = new Mesh();

            foreach (var m in meshes)
                mesh.Append(m);

            mesh.Weld(0);
            mesh.RebuildNormals();

            Plane plane = component.Label.Plane;
            geometry.GetBoundingBox(plane, out Box box);
            var worldBounds = geometry.GetBoundingBox(true);

            bool isDoubleSided = false;

            foreach (var connector in connectors)
            {
                Line axis = connector.Axis;

                // --- Bounding box rejection ---
                if (
                    Math.Max(axis.FromX, axis.ToX) < worldBounds.Min.X ||
                    Math.Min(axis.FromX, axis.ToX) > worldBounds.Max.X ||
                    Math.Max(axis.FromY, axis.ToY) < worldBounds.Min.Y ||
                    Math.Min(axis.FromY, axis.ToY) > worldBounds.Max.Y ||
                    Math.Max(axis.FromZ, axis.ToZ) < worldBounds.Min.Z ||
                    Math.Min(axis.FromZ, axis.ToZ) > worldBounds.Max.Z
                )
                    continue;

                double length = axis.Length;
                if (length < 1.0) continue;

                Vector3d direction = axis.Direction;
                direction.Unitize();

                // --- Rays ---
                double frontRay = Intersection.MeshRay(mesh, new Ray3d(axis.From, direction));
                double backRay = Intersection.MeshRay(mesh, new Ray3d(axis.From - direction * breakthroughEpsilon, -direction));
                double endRay = Intersection.MeshRay(mesh, new Ray3d(axis.To + direction * breakthroughEpsilon, direction));

                //if ((frontRay + breakthroughEpsilon) >= length)
                //    continue;

                if (frontRay < breakthroughEpsilon) continue;

                bool startThru = double.IsNegativeInfinity(backRay);
                bool endThru = double.IsNegativeInfinity(endRay);

                if (!startThru && !endThru)
                {
                    RhinoApp.WriteLine($"-- WARNING: Connector {connector.Name} starts and stops within material!");
                }

                // --- Flip logic ---
                if (backRay >= 0 && frontRay > breakthroughEpsilon)
                {
                    axis.Flip();
                    direction.Reverse();
                    (startThru, endThru) = (endThru, startThru);
                }
                else if (backRay > breakthroughEpsilon)
                {
                    continue;
                }
                else if (double.IsNegativeInfinity(backRay) && double.IsNegativeInfinity(frontRay))
                {
                    continue;
                }

                if (Intersection.LineBox(axis, box, 1e-3, out Interval interval))
                {
                    if (interval.Min < (1 + 0.2 / axis.Length) &&
                        interval.Max > (0 - 0.2 / axis.Length))
                    {
                        double t0 = interval.T0;
                        double t1 = interval.T1;

                        double tmin = 0;
                        double tmax = 1;

                        if (Math.Abs(t0 * length) < breakthroughEpsilon)
                            tmin = t0;

                        if (Math.Abs((t1 - 1) * length) < breakthroughEpsilon)
                            tmax = t1;

                        Vector3d zaxis = plane.ZAxis;
                        bool sides = Math.Abs(plane.ZAxis * direction) < Math.Cos(Math.PI * 0.25);

                        if (sides)
                            zaxis = plane.YAxis;

                        if (!sides && (axis.Direction * zaxis) > 0)
                        {
                            if ((t0 + epsilon) >= tmin && (t1 - epsilon) <= tmax)
                            {
                                (t0, t1) = (t1, t0);
                            }
                            else if (t0 < tmin && t1 <= tmax)
                            {
                                (t0, t1) = (t1, tmin);
                                (startThru, endThru) = (endThru, startThru);
                            }
                            else if (t0 >= tmin && t1 > tmax)
                            {
                                isDoubleSided = true;
                            }
                        }

                        Interval limited = new Interval(Math.Max(t0, t0), Math.Min(tmax, t1));
                        double depth = limited.Length * axis.Length;

                        if (Math.Abs(depth) < breakthroughEpsilon)
                            continue;

                        Point3d p0 = axis.PointAt(limited.T0);
                        Point3d p1 = axis.PointAt(limited.T1);

                        Vector3d drillVector = p1 - p0;
                        drillVector.Unitize();

                        var side = Utility.GetBoxSide(p0, box);

                        // --- Tilt compensation ---
                        if (side != BoxSide.Unknown && compensateTilt)
                        {
                            Vector3d tiltAxis = plane.ZAxis;

                            if (side == BoxSide.Inside || side == BoxSide.Outside)
                                tiltAxis = plane.YAxis;
                            else if (side == BoxSide.Left || side == BoxSide.Right)
                                tiltAxis = plane.XAxis;

                            double dot = Math.Abs(drillVector * tiltAxis);
                            dot = Math.Min(1, dot);

                            double tiltOffset = connector.Diameter * 0.5 *
                                Math.Tan(Math.Acos(dot));

                            p0 -= drillVector * tiltOffset;

                            if (endThru)
                                p1 += drillVector * tiltOffset;
                        }

                        if (endThru)
                            p1 += drillVector * breakthroughDistance;

                        p0 -= drillVector * startDistance;

                        var newAxis = new Line(p0, p1);

                        intersectingConnectors.Add(new PlacedConnector { Connector = new Connector(newAxis, connector.Diameter, connector.Name), Side = side });

                    }
                }
            }

            return (intersectingConnectors, isDoubleSided);
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
