using D2P_Core.Utility;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace G2PComponents.Commands
{
    public class SelectChildrenCommand : Command
    {
        public override string EnglishName => "SelectComponentChildren";

        public SelectChildrenCommand()
        {
            Instance = this;
        }

        public static SelectChildrenCommand Instance
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

            foreach (var component in components)
            {
                var children = Instantiation.GetChildren(component, null, doc);

                int count = 0;
                foreach (var child in children)
                {
                    count++;
                    // RhinoApp.WriteLine($"    {child.ShortName}");

                    foreach (var rhinoObject in child.RHObjects)
                    {
                        rhinoObject.Select(true, true);
                    }
                }
                RhinoApp.WriteLine($"-- {component.ShortName} has {count} children.");

            }

            doc.Views.Redraw();

            return Result.Success;
        }
    }
}
