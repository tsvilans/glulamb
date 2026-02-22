using D2P_Core.Utility;
using GluLamb;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace G2PComponents.Commands
{
    public class RebuildComponentGeometryCommand : Command
    {
        public override string EnglishName => "RebuildComponentGeometry";

        public RebuildComponentGeometryCommand()
        {
            Instance = this;
        }

        public static RebuildComponentGeometryCommand Instance
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
                var cutters = new List<Brep>();

                var guid = Utility.GetMemberIDs(component, "Geometry").First();

                var rhObject = doc.Objects.FindId(guid);
                var body = rhObject.Geometry as Brep;
                if (body == null) continue;

                body.GetBoundingBox(component.Label.Plane, out Box box);

                foreach (var child in children)
                {
                    if (!child.TypeID.StartsWith("X")) continue;

                    cutters.AddRange(Utility.GetMember(child, "Geometry").OfType<Brep>() ?? Enumerable.Empty<Brep>());
                    cutters.AddRange(Utility.GetMember(child, "Brep").OfType<Brep>() ?? Enumerable.Empty<Brep>());
                }

                if (cutters.Count > 0 || true)
                {
                    var cut = box.ToBrep().Cut(Brep.JoinBreps(cutters, 1e-3) ?? Enumerable.Empty<Brep>());

                    doc.Objects.Replace(rhObject.Id, cut);
                    rhObject.CommitChanges();
                }
            }

            doc.Views.Redraw();

            return Result.Success;
        }
    }
}
