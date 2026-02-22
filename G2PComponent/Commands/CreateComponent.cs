using D2P_Core;
using D2P_Core.Utility;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace G2PComponents.Commands
{
    public class CreateComponentCommand : Command
    {
        public CreateComponentCommand()
        {
            Instance = this;
        }

        public static CreateComponentCommand Instance
        {
            get; private set;
        }

        public override string EnglishName => "CreateComponent";

        private OptionToggle optionAxis = new OptionToggle(true, "YAxis", "XAxis");

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            ObjRef[] objRefs;
            var rc = RhinoGet.GetMultipleObjects("Pick Breps", true, ObjectType.AnyObject, out objRefs);
            if (rc != Result.Success || objRefs == null || objRefs.Length == 0)
                return rc;

            string typeId = "NONE";
            string typeName = "Orphaned component";
            int counter = 1;

            var allTypes = new Dictionary<string, ComponentType>();

            var componentTypeLayers = Layers.FindAllExistentComponentTypeRootLayers(Context.settings, doc);

            foreach (var componentTypeLayer in componentTypeLayers)
            {
                typeId = Layers.GetComponentTypeID(componentTypeLayer, Context.settings);
                typeName = Layers.GetComponentTypeName(componentTypeLayer, Context.settings);
                double labelSize = Layers.GetComponentTypeLabelSize(componentTypeLayer, Context.settings);
                Color layerColor = componentTypeLayer.Color;

                allTypes[typeId] = new ComponentType(typeId, typeName, Context.settings, labelSize, layerColor);
            }

            // Ask for type
            var gs = new GetString();
            gs.SetCommandPrompt("Component type ID");

            foreach (var key in allTypes.Keys)
                gs.AddOption(key);

            gs.Get();
            if (gs.CommandResult() != Result.Success)
                return gs.CommandResult();

            string typeInput = gs.Option()?.EnglishName ?? gs.StringResult();

            if (!allTypes.ContainsKey(typeInput))
                return Result.Failure;

            var componentType = allTypes[typeInput];
            typeId = componentType.TypeID;
            typeName = componentType.TypeName;

            var errors = new List<RhinoObject>();

            foreach (var objRef in objRefs)
            {
                var rhinoObject = objRef.Object();
                if (rhinoObject == null) continue;

                string name;

                if (string.IsNullOrWhiteSpace(rhinoObject.Name))
                {
                    RhinoApp.WriteLine("Object requires a name!");
                    name = $"{typeId}-{counter:00}";
                    counter++;
                }
                else
                {
                    name = rhinoObject.Name;
                }

                Brep brep = null;

                if (rhinoObject.ObjectType == ObjectType.Brep)
                {
                    brep = rhinoObject.Geometry as Brep;
                }
                else if (rhinoObject.ObjectType == ObjectType.Extrusion)
                {
                    var ex = rhinoObject.Geometry as Extrusion;
                    brep = ex?.ToBrep(true);
                }

                if (brep == null)
                    continue;

                if (!brep.IsValid)
                {
                    RhinoApp.WriteLine($"Failed to get clean geometry for component: {name} - Brep valid: {brep.IsValid}");
                    rhinoObject.Select(true);
                    errors.Add(rhinoObject);
                    continue;
                }

                brep = brep.DuplicateBrep();

                Plane plane = GluLamb.Utility.FindBestBasePlane(brep, Vector3d.Unset);

                var tok = name.Split('-');

                if (Context.ComponentTypes.ContainsKey(tok[0]))
                    componentType = Context.ComponentTypes[tok[0]];
                else
                    componentType = new ComponentType(typeId, typeName, Context.settings, 2.0, Color.DimGray);

                RhinoApp.WriteLine($"Creating component '{name}' of type '{componentType.TypeName}'");

                var component = new Component(componentType, name, plane);
                var attr = component.AttributeCollection[component.ID];

                brep.GetBoundingBox(plane, out Box worldBox);

                int width = (int)Math.Round(worldBox.Y.Length);
                int height = (int)Math.Round(worldBox.Z.Length);
                int length = (int)Math.Round(worldBox.X.Length);

                if (height > width)
                {
                    var temp = width;
                    width = height;
                    height = temp;
                }

                attr.SetUserString("width", $"{width}");
                attr.SetUserString("height", $"{height}");
                attr.SetUserString("length", $"{length}");

                //RhinoApp.WriteLine($"Setting geometry: {brep.IsValid}");

                component.AddMember(
                    new ComponentMember(
                        new LayerInfo("Geometry", componentType.LayerColor),
                        new List<GeometryBase> { brep },
                        attr.Duplicate()
                    )
                );

                Guid newGuid = RHDoc.AddToRhinoDoc(component, doc, true);

                doc.Views.Redraw();

                rhinoObject.Select(false);
                doc.Objects.Hide(rhinoObject.Id, true);
            }

            foreach (var error in errors)
            {
                RhinoApp.WriteLine($"ERROR: {error.Name}");
            }

            doc.Views.Redraw();
            return Result.Success;
        }
    }
}
