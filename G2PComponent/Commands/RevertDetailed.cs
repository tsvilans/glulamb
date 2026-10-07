using D2P_Core.Utility;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input;

namespace G2PComponents.Commands
{
    /// <summary>
    /// Deletes the selected components' DetailedGeometry, leaving their Geometry.
    /// </summary>
    public class RevertDetailedCommand : Command
    {
        public RevertDetailedCommand()
        {
            Instance = this;
        }

        public static RevertDetailedCommand Instance { get; private set; }

        public override string EnglishName => "RevertDetailed";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var rc = RhinoGet.GetMultipleObjects("Select components to revert to their basic geometry", true, ObjectType.AnyObject, out ObjRef[] objRefs);
            if (rc != Result.Success || objRefs == null || objRefs.Length == 0)
                return rc;

            var components = Instantiation.InstancesFromObjects(objRefs.Select(x => x.Object()), Context.settings, doc)
                .GroupBy(x => x.ID).Select(g => g.First()).ToList();

            int count = components.Count(c => Detailing.Revert(c, doc) > 0);
            RhinoApp.WriteLine($"Reverted {count} of {components.Count} components.");

            doc.Views.Redraw();
            return Result.Success;
        }
    }
}
