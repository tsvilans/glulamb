using D2P_Core.Interfaces;
using D2P_Core.Utility;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace G2PComponents.Commands
{
    /// <summary>
    /// Rebuilds the Geometry of the selected components as plain blanks long enough for their
    /// joints: a box in the component plane, the size of the current Geometry's section, with its
    /// length extended at either end to reach what the joints need (recorded by JoinComponents),
    /// plus a margin. Ends that don't need extending keep their length. A separate command
    /// because it replaces whatever is on the Geometry layer.
    /// </summary>
    public class ExtendForJointsCommand : Command
    {
        public ExtendForJointsCommand()
        {
            Instance = this;
        }

        public static ExtendForJointsCommand Instance { get; private set; }

        public override string EnglishName => "ExtendForJoints";

        // Kept between runs
        private static double Margin = 0;

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var go = new GetObject();
            go.SetCommandPrompt("Select components to extend for their joints");
            go.EnablePreSelect(true, true);
            go.EnableUnselectObjectsOnExit(false);
            var margin = new OptionDouble(Margin, 0, double.MaxValue);
            go.AddOptionDouble("Margin", ref margin, "Extra length added to each end that is extended");

            while (true)
            {
                var res = go.GetMultiple(1, 0);
                if (res == GetResult.Option) { go.EnablePreSelect(false, true); continue; }
                if (res != GetResult.Object) return Result.Cancel;
                break;
            }
            Margin = margin.CurrentValue;

            var components = Instantiation.InstancesFromObjects(go.Objects().Select(x => x.Object()), Context.settings, doc)
                .GroupBy(x => x.ID).Select(g => g.First())
                .Where(x => !x.ShortName.Contains(Context.settings.JointDelimiter))
                .ToList();

            int extended = 0;
            foreach (var component in components)
            {
                var messages = new List<string>();
                if (Extend(component, doc, Margin, messages)) extended++;
                foreach (var message in messages)
                    RhinoApp.WriteLine($"-- {message}");
            }

            RhinoApp.WriteLine($"Extended {extended} of {components.Count} components.");
            doc.Views.Redraw();
            return Result.Success;
        }

        /// <summary>
        /// Rebuilds the component's Geometry as a box reaching every point its joints need, plus
        /// the margin at each extended end. Returns whether it changed anything.
        /// </summary>
        public static bool Extend(IComponent component, RhinoDoc doc, double margin, List<string> messages)
        {
            var ids = Utility.GetMemberIDs(component, Detailing.Basic, doc).ToList();
            var geometry = ids.Select(id => doc.Objects.FindId(id)?.Geometry).Where(g => g != null).ToList();
            if (geometry.Count == 0)
            {
                messages.Add($"{component.ShortName}: no Geometry.");
                return false;
            }

            var plane = component.Plane;
            var bounds = BoundingBox.Empty;
            foreach (var g in geometry) bounds.Union(g.GetBoundingBox(plane));

            double start = bounds.Min.X, end = bounds.Max.X;
            int joints = 0, unrecorded = 0;
            foreach (var joint in Joining.FindJoints(component, doc))
            {
                var points = Joining.ExtensionsFor(joint, component, doc);
                if (points == null) { unrecorded++; continue; }
                joints++;
                foreach (var p in points)
                {
                    plane.RemapToPlaneSpace(p, out var local);
                    start = Math.Min(start, local.X);
                    end = Math.Max(end, local.X);
                }
            }

            if (unrecorded > 0)
                messages.Add($"{component.ShortName}: {unrecorded} of its joints were made before extensions were recorded; join them again to include them.");

            var tolerance = doc.ModelAbsoluteTolerance;
            double atStart = bounds.Min.X - start, atEnd = end - bounds.Max.X;
            if (atStart <= tolerance && atEnd <= tolerance)
            {
                messages.Add($"{component.ShortName}: long enough for its {joints} joint{(joints == 1 ? "" : "s")}.");
                return false;
            }
            if (atStart > tolerance) start -= margin;
            if (atEnd > tolerance) end += margin;

            var box = new Box(plane, new Interval(start, end), new Interval(bounds.Min.Y, bounds.Max.Y), new Interval(bounds.Min.Z, bounds.Max.Z));
            doc.Objects.Replace(ids[0], box.ToBrep());
            foreach (var id in ids.Skip(1))
                doc.Objects.Delete(id, true);

            messages.Add($"{component.ShortName}: extended by {bounds.Min.X - start:0.#} at the start and {end - bounds.Max.X:0.#} at the end; Geometry rebuilt as a box.");
            return true;
        }
    }
}
