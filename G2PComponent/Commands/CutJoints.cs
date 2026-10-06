using D2P_Core;
using D2P_Core.Utility;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace G2PComponents.Commands
{
    /// <summary>
    /// Cuts the selected components with the joints they are part of (from JoinComponents): each
    /// component's Geometry, cut by the cutters tagged with it on its joint components, becomes
    /// its DetailedGeometry. Starts from Geometry each time, so it can be run again after joints
    /// change; run CutFixings afterwards, which cuts into DetailedGeometry.
    /// </summary>
    public class CutJointsCommand : Command
    {
        public CutJointsCommand()
        {
            Instance = this;
        }

        public static CutJointsCommand Instance { get; private set; }

        public override string EnglishName => "CutJoints";

        private bool m_drillings = true;

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var go = new GetObject();
            go.SetCommandPrompt("Select components to cut with their joints");
            go.EnablePreSelect(true, true);
            var drillings = new OptionToggle(m_drillings, "No", "Yes");
            go.AddOptionToggle("Drillings", ref drillings);

            while (true)
            {
                var res = go.GetMultiple(1, 0);
                if (res == GetResult.Option) continue;
                if (res != GetResult.Object) return Result.Cancel;
                break;
            }
            m_drillings = drillings.CurrentValue;

            var components = Instantiation.InstancesFromObjects(go.Objects().Select(x => x.Object()), Context.settings, doc)
                .GroupBy(x => x.ID).Select(g => g.First())
                .Where(x => !x.ShortName.Contains(Context.settings.JointDelimiter))   // not the joints themselves
                .ToList();

            var failed = new List<Guid>();
            foreach (var component in components)
            {
                var messages = new List<string>();
                var cut = Joining.CutJoints(component, doc, m_drillings, messages);
                foreach (var message in messages)
                    RhinoApp.WriteLine($"-- {message}");

                if (cut == null)
                {
                    failed.Add(component.ID);
                    continue;
                }

                var layer = doc.Layers.FindIndex(component.Attributes.First().LayerIndex);
                component.ReplaceMember(new ComponentMember(
                    new LayerInfo("DetailedGeometry", layer?.Color ?? component.LayerColor),
                    new GeometryBase[] { cut },
                    component.Attributes.First().Duplicate()));
                RHDoc.AddToRhinoDoc(component, doc, true);
            }

            doc.Objects.UnselectAll();
            if (failed.Count > 0)
                doc.Objects.Select(failed, true);
            doc.Views.Redraw();
            return Result.Success;
        }
    }
}
