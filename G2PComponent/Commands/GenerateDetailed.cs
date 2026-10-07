using D2P_Core.Utility;
using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;

namespace G2PComponents.Commands
{
    /// <summary>
    /// Makes the selected components' DetailedGeometry from their Geometry: their joints (from
    /// JoinComponents) and/or the connectors (fixings) that hit them, cut every time from the
    /// blank, so it can be run again whenever joints or fixings change. Replaces CutJoints and
    /// CutFixings.
    /// </summary>
    public class GenerateDetailedCommand : Command
    {
        public GenerateDetailedCommand()
        {
            Instance = this;
        }

        public static GenerateDetailedCommand Instance { get; private set; }

        public override string EnglishName => "GenerateDetailed";

        // Kept between runs
        private static bool CutJoints = true;
        private static bool CutConnectors = true;
        private static string ConnectorLayer = "Connectors";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var go = new GetObject();
            go.EnablePreSelect(true, true);
            go.EnableUnselectObjectsOnExit(false);

            var joints = new OptionToggle(CutJoints, "No", "Yes");
            var connectors = new OptionToggle(CutConnectors, "No", "Yes");

            while (true)
            {
                go.ClearCommandOptions();
                go.SetCommandPrompt($"Select components to detail (connector layer: {ConnectorLayer})");
                go.AddOptionToggle("Joints", ref joints);
                go.AddOptionToggle("Connectors", ref connectors);
                var layerOption = go.AddOption("ConnectorLayer");

                var res = go.GetMultiple(1, 0);
                if (res == GetResult.Option)
                {
                    if (go.OptionIndex() == layerOption)
                    {
                        var gs = new GetString();
                        gs.SetCommandPrompt("Layer name for connectors");
                        gs.SetDefaultString(ConnectorLayer);
                        if (gs.Get() == GetResult.String && !string.IsNullOrWhiteSpace(gs.StringResult()))
                            ConnectorLayer = gs.StringResult().Trim();
                    }
                    go.EnablePreSelect(false, true);
                    continue;
                }
                if (res != GetResult.Object) return Result.Cancel;
                break;
            }

            CutJoints = joints.CurrentValue;
            CutConnectors = connectors.CurrentValue;

            var components = Instantiation.InstancesFromObjects(go.Objects().Select(x => x.Object()), Context.settings, doc)
                .GroupBy(x => x.ID).Select(g => g.First())
                .Where(x => !x.ShortName.Contains(Context.settings.JointDelimiter))   // not the joints themselves
                .ToList();

            var connectorList = CutConnectors ? Connector.GetAllConnectors(doc, new List<string> { ConnectorLayer }) : null;
            if (CutConnectors && (connectorList == null || connectorList.Count == 0))
                RhinoApp.WriteLine($"-- No connectors found on layer {ConnectorLayer}.");

            int done = 0;
            var untouched = new List<Guid>();
            foreach (var component in components)
            {
                var messages = new List<string>();
                if (Detailing.Generate(component, doc, CutJoints, CutConnectors, connectorList, messages))
                    done++;
                else
                    untouched.Add(component.ID);
                foreach (var message in messages)
                    RhinoApp.WriteLine($"-- {message}");
            }

            RhinoApp.WriteLine($"Detailed {done} of {components.Count} components." +
                (untouched.Count > 0 ? $" {untouched.Count} had nothing to cut or failed (left selected)." : ""));

            // Instances that share a mark should come out the same
            var (problems, marks, odd) = Detailing.CheckMarks(components, doc);
            foreach (var problem in problems)
                RhinoApp.WriteLine($"-- {problem}");
            if (marks > 0 && problems.Count == 0)
                RhinoApp.WriteLine($"All instances of {marks} repeated mark{(marks == 1 ? "" : "s")} match.");

            doc.Objects.UnselectAll();
            if (untouched.Count > 0)
                doc.Objects.Select(untouched, true);
            if (odd.Count > 0)
            {
                doc.Objects.Select(odd, true);
                RhinoApp.WriteLine($"The {odd.Count} instance{(odd.Count == 1 ? "" : "s")} that differ{(odd.Count == 1 ? "s" : "")} or aren't detailed are selected.");
            }
            doc.Views.Redraw();
            return Result.Success;
        }
    }
}
