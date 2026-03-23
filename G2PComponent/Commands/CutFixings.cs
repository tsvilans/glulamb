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
    /// WORK IN PROGRESS
    /// Identifies components that share the same geometry, have the same
    /// features, and therefore can be produced serially.
    /// </summary>
    public class CutFixingsCommand : Command
    {
        public override string EnglishName => "CutFixings";

        public CutFixingsCommand()
        {
            Instance = this;
        }

        public static CutFixingsCommand Instance
        {
            get; private set;
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // Pick components
            ObjRef[] objRefs;
            var rc = RhinoGet.GetMultipleObjects("Select components", true, ObjectType.AnyObject, out objRefs);
            if (rc != Result.Success || objRefs == null || objRefs.Length == 0)
                return rc;

            var components = Instantiation.InstancesFromObjects(objRefs.Select(x => x.Object()), Context.settings, doc);
            //components.Sort((x, y) => x.ShortName.CompareTo(y.ShortName));

            // Ask for new name
            var gs = new GetString();
            gs.SetDefaultString("Connectors");
            gs.SetCommandPrompt("Layer name for fixings");
            gs.AcceptNothing(false);

            gs.Get();
            if (gs.CommandResult() != Result.Success)
                return gs.CommandResult();

            string connectorLayerName = gs.StringResult();
            if (string.IsNullOrWhiteSpace(connectorLayerName))
                return Result.Failure;

            var connectors = Connector.GetAllConnectors(doc, new List<string> { connectorLayerName });

            var select = new List<Guid>();

            foreach (var component in components)
            {
                var layer = doc.Layers.FindIndex(component.Attributes.First().LayerIndex);

                (var placedConnectors, bool doubleSided) = Connector.IntersectConnectorsRay(component, connectors, 1.0, true, 10, 10, false, doc, false);

                var brep = Connector.CutConnectors(component, placedConnectors.Select(x => x.Connector), doc, 1e-2);

                if (brep == null)
                {
                    RhinoApp.WriteLine($"-- Cutting component {component.ShortName} failed.");
                    select.Add(component.ID);
                    select.AddRange(component.GeometryCollection.Keys);
                    continue;
                }

                var guids = component.AddMember(
                    new ComponentMember(
                        new LayerInfo("DetailedGeometry", layer.Color),
                        new GeometryBase[] { brep },
                        component.Attributes.First().Duplicate()));

                RHDoc.AddToRhinoDoc(component, doc, true);
            }

            if (select.Count > 0)
            {
                doc.Objects.UnselectAll();
            }
            doc.Objects.Select(select, true);
            doc.Views.Redraw();

            return Result.Success;
        }
    }
}
