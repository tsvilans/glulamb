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
    public class ChangeComponentTypeCommand : Command
    {
        public ChangeComponentTypeCommand()
        {
            Instance = this;
        }

        public static ChangeComponentTypeCommand Instance
        {
            get; private set;
        }

        public override string EnglishName => "ChangeComponentType";

        /// <summary>
        /// WIP. Change component type without changing geometry or members. Should
        /// just be swapping the TypeID and layer IDs in all related objects.
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="mode"></param>
        /// <returns></returns>
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // Pick components
            ObjRef[] objRefs;
            var rc = RhinoGet.GetMultipleObjects("Select components", true, ObjectType.AnyObject, out objRefs);
            if (rc != Result.Success || objRefs == null || objRefs.Length == 0)
                return rc;

            var components = Instantiation.InstancesFromObjects(objRefs.Select(x => x.Object()), Context.settings, doc);

            var allTypes = new Dictionary<string, ComponentType>();

            var componentTypeLayers = Layers.FindAllExistentComponentTypeRootLayers(Context.settings, doc);

            foreach (var componentTypeLayer in componentTypeLayers)
            {
                var typeId = Layers.GetComponentTypeID(componentTypeLayer, Context.settings);
                var typeName = Layers.GetComponentTypeName(componentTypeLayer, Context.settings);
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

            foreach (var component in components)
            {
                var shortName = component.ShortName.Replace(component.TypeID, componentType.TypeID);
                component.Label.PlainText = shortName;

                // Find all members and member layers
                // Swap layer indices on all related geometry

                // Guid newGuid = RHDoc.AddToRhinoDoc(component, doc, true);

            }

            doc.Views.Redraw();
            return Result.Success;
        }
    }
}
