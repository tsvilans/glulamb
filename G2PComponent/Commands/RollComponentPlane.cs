using D2P_Core.Utility;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace G2PComponents.Commands
{
    public class FlipComponentPlaneCommand : Command
    {
        public FlipComponentPlaneCommand()
        {
            Instance = this;
        }

        public static FlipComponentPlaneCommand Instance
        {
            get; private set;
        }

        public override string EnglishName => "FlipComponentPlane";

        private OptionToggle optionAxis = new OptionToggle(true, "YAxis", "XAxis");

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {

            const ObjectType geometryFilter = ObjectType.AnyObject;

            GetObject go = new GetObject();
            go.SetCommandPrompt("Select components");
            go.GeometryFilter = geometryFilter;
            go.AddOptionToggle("Axis", ref optionAxis);
            go.GroupSelect = true;
            go.SubObjectSelect = false;
            go.EnableClearObjectsOnEntry(false);
            go.EnableUnselectObjectsOnExit(false);
            go.DeselectAllBeforePostSelect = false;
            go.EnablePostSelect(true);
            go.AcceptNothing(true);

            for (; ; )
            {
                GetResult res = go.GetMultiple(1, 0);

                if (res == GetResult.Option)
                {
                    go.EnablePreSelect(false, true);
                    continue;
                }

                else if (res != GetResult.Object)
                    return Result.Cancel;

                if (go.ObjectsWerePreselected)
                {
                    //bHavePreselectedObjects = true;
                    go.EnablePreSelect(false, true);
                    continue;
                }

                break;
            }

            foreach (var rhObject in go.Objects())
            {
                rhObject.Object().Select(true, true);
            }

            var components = Instantiation.InstancesFromObjects(go.Objects().Select(x => x.Object()), Context.settings);
            foreach (var component in components)
            {
                var plane = component.Plane;
                var brep = Utility.GetMember(component, "Geometry").First() as Brep;

                if (brep == null) continue;

                var flippedPlane = Utility.FlipBasePlane(brep, plane, optionAxis.CurrentValue);
                var label = component.Label.Duplicate() as TextEntity;
                label.Plane = flippedPlane;

                doc.Objects.Replace(component.ID, label);
            }

            return Result.Success;
        }
    }
}