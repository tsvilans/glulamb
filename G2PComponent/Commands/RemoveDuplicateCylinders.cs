using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace G2PComponents.Commands
{
    /// <summary>
    /// Deletes duplicate cylinders in the selection, e.g. connectors placed twice by
    /// ConnectComponents or ConnectManyComponents. Two cylinders are duplicates when their
    /// radii and both axis end points match within the tolerance, whichever way round the axis
    /// runs. The first of each set is kept. Only simple cylinders count: a cylindrical face with
    /// at most two caps, as a Brep or an Extrusion.
    /// </summary>
    public class RemoveDuplicateCylindersCommand : Command
    {
        public RemoveDuplicateCylindersCommand()
        {
            Instance = this;
        }

        public static RemoveDuplicateCylindersCommand Instance { get; private set; }

        public override string EnglishName => "RemoveDuplicateCylinders";

        private double Tolerance = 0.01;

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var go = new GetObject();
            go.SetCommandPrompt("Select cylinders");
            go.GeometryFilter = ObjectType.Brep | ObjectType.Extrusion;
            go.EnablePreSelect(true, true);
            var tolerance = new OptionDouble(Tolerance, 0, double.MaxValue);
            go.AddOptionDouble("Tolerance", ref tolerance);

            while (true)
            {
                var res = go.GetMultiple(1, 0);
                if (res == GetResult.Option) continue;
                if (res != GetResult.Object) return Result.Cancel;
                break;
            }
            Tolerance = tolerance.CurrentValue;

            var ids = go.Objects().Select(x => x.ObjectId).Distinct();
            var duplicates = FindDuplicates(ids, doc, Tolerance, out int cylinders);
            foreach (var id in duplicates)
                doc.Objects.Delete(id, true);

            RhinoApp.WriteLine($"{cylinders} cylinders in the selection; deleted {duplicates.Count} duplicates.");
            doc.Views.Redraw();
            return Result.Success;
        }

        /// <summary>
        /// The ids of the cylinders that duplicate an earlier one in the list, and how many of
        /// the objects are cylinders.
        /// </summary>
        public static List<Guid> FindDuplicates(IEnumerable<Guid> ids, RhinoDoc doc, double tolerance, out int cylinders)
        {
            var kept = new List<(Point3d A, Point3d B, double Radius)>();
            var duplicates = new List<Guid>();
            cylinders = 0;

            foreach (var id in ids)
            {
                var geometry = doc.Objects.FindId(id)?.Geometry;
                if (!TryGetCylinder(geometry, doc.ModelAbsoluteTolerance, out var a, out var b, out var radius)) continue;
                cylinders++;

                bool Same((Point3d A, Point3d B, double Radius) k) =>
                    Math.Abs(k.Radius - radius) <= tolerance &&
                    ((k.A.DistanceTo(a) <= tolerance && k.B.DistanceTo(b) <= tolerance) ||
                     (k.A.DistanceTo(b) <= tolerance && k.B.DistanceTo(a) <= tolerance));

                if (kept.Any(Same)) duplicates.Add(id);
                else kept.Add((a, b, radius));
            }
            return duplicates;
        }

        /// <summary>
        /// The axis end points and radius of a simple cylinder: one cylindrical face and at most
        /// two other (cap) faces.
        /// </summary>
        public static bool TryGetCylinder(GeometryBase? geometry, double tolerance, out Point3d a, out Point3d b, out double radius)
        {
            a = b = Point3d.Unset;
            radius = 0;
            var brep = geometry is Extrusion extrusion ? extrusion.ToBrep() : geometry as Brep;
            if (brep == null || brep.Faces.Count > 3) return false;

            foreach (var face in brep.Faces)
            {
                if (!face.IsCylinder(tolerance) || !face.TryGetFiniteCylinder(out var cylinder, tolerance)) continue;
                a = cylinder.CircleAt(cylinder.Height1).Center;
                b = cylinder.CircleAt(cylinder.Height2).Center;
                radius = cylinder.Radius;
                return true;
            }
            return false;
        }
    }
}
