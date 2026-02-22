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
    public class RenameComponentCommand : Command
    {
        public override string EnglishName => "RenameComponent";

        public RenameComponentCommand()
        {
            Instance = this;
        }

        public static RenameComponentCommand Instance
        {
            get; private set;
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // Pick component
            ObjRef objRef;
            var rc = RhinoGet.GetOneObject("Select component", true, ObjectType.AnyObject, out objRef);
            if (rc != Rhino.Commands.Result.Success || objRef == null)
                return rc;

            Guid guid = objRef.ObjectId;

            var components = Instantiation.InstancesFromObjects(new List<Guid> { guid }, Context.settings);
            if (components == null || components.Count == 0)
                return Result.Cancel;

            var component = components[0];

            RhinoApp.WriteLine($"-- Found component '{component.ShortName}'");

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

            var children = Instantiation.GetChildren(component, null, doc);

            foreach (var child in children)
            {
                string childName = child.ShortName.Replace(component.ShortName, name);
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

            string fullName = component.TypeID + Context.settings.TypeDelimiter + name;

            foreach (var obj in component.RHObjects)
            {
                var attr = obj.Attributes;
                attr.Name = fullName;
                obj.CommitChanges();
            }

            var componentLabel = component.Label.Duplicate() as TextEntity;
            componentLabel.PlainText = name;
            doc.Objects.Replace(component.ID, componentLabel);

            doc.Views.Redraw();

            return Result.Success;
        }
    }
}
