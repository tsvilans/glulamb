using D2P_Core.Utility;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace G2PComponents.Commands
{
    public class BulkRenameComponentsCommand : Command
    {
        public override string EnglishName => "BulkRenameComponents";

        public BulkRenameComponentsCommand()
        {
            Instance = this;
        }

        public static BulkRenameComponentsCommand Instance
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

            RhinoApp.WriteLine($"-- Found component '{components.Count}'");

            // Ask for new name
            var gs = new GetString();
            gs.SetCommandPrompt("New name");
            gs.AcceptNothing(false);

            gs.Get();
            if (gs.CommandResult() != Result.Success)
                return gs.CommandResult();

            string name = gs.StringResult();
            if (string.IsNullOrWhiteSpace(name))
                return Result.Failure;

            int counter = 1;
            foreach (var component in components)
            {
                var children = Instantiation.GetChildren(component, null, doc);
                var new_name = $"{name}-{counter:00}";

                foreach (var child in children)
                {
                    string childName = child.ShortName.Replace(component.ShortName, new_name);
                    string fullChildName = child.TypeID + Context.settings.TypeDelimiter + childName;

                    foreach (var obj in child.RHObjects)
                    {
                        var attr = obj.Attributes;
                        attr.Name = fullChildName;
                        obj.CommitChanges();
                    }

                    var label = child.Label.Duplicate() as TextEntity;
                    label.PlainText = childName;
                    doc.Objects.Replace(child.ID, label);
                }

                string fullName = component.TypeID + Context.settings.TypeDelimiter + new_name;

                foreach (var obj in component.RHObjects)
                {
                    var attr = obj.Attributes;
                    attr.Name = fullName;
                    obj.CommitChanges();
                }

                var componentLabel = component.Label.Duplicate() as TextEntity;
                componentLabel.PlainText = new_name;
                doc.Objects.Replace(component.ID, componentLabel);
                counter++;
            }

            doc.Views.Redraw();

            return Result.Success;
        }
    }
}
