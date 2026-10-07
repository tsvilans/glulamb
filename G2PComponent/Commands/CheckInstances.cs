using D2P_Core.Utility;
using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;

namespace G2PComponents.Commands
{
    /// <summary>
    /// Checks that components sharing a name (instances of one mark) have the same detailed
    /// geometry, for every mark among the selected components (Enter for all components).
    /// Separate from GenerateDetailed because it measures every instance, which is slow in a
    /// large model. Instances that differ or aren't detailed are reported and left selected.
    /// </summary>
    public class CheckInstancesCommand : Command
    {
        public CheckInstancesCommand()
        {
            Instance = this;
        }

        public static CheckInstancesCommand Instance { get; private set; }

        public override string EnglishName => "CheckInstances";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var go = new GetObject();
            go.SetCommandPrompt("Select components whose marks to check (Enter for all)");
            go.EnablePreSelect(true, true);
            go.AcceptNothing(true);

            var res = go.GetMultiple(1, 0);
            if (res == GetResult.Cancel) return Result.Cancel;

            var objects = res == GetResult.Object
                ? go.Objects().Select(x => x.Object())
                : doc.Objects.GetObjectList(new Rhino.DocObjects.ObjectEnumeratorSettings { ObjectTypeFilter = Rhino.DocObjects.ObjectType.Annotation, HiddenObjects = true, LockedObjects = true });
            var components = Instantiation.InstancesFromObjects(objects, Context.settings, doc)
                .GroupBy(x => x.ID).Select(g => g.First()).ToList();

            var (problems, marks, odd) = Detailing.CheckMarks(components, doc);
            foreach (var problem in problems)
                RhinoApp.WriteLine($"-- {problem}");

            doc.Objects.UnselectAll();
            if (marks == 0)
                RhinoApp.WriteLine("No repeated marks to check.");
            else if (problems.Count == 0)
                RhinoApp.WriteLine($"All instances of {marks} repeated mark{(marks == 1 ? "" : "s")} match.");
            else
            {
                doc.Objects.Select(odd, true);
                RhinoApp.WriteLine($"{problems.Count} of {marks} repeated marks differ; the {odd.Count} instance{(odd.Count == 1 ? "" : "s")} that differ or aren't detailed are selected.");
            }

            doc.Views.Redraw();
            return Result.Success;
        }
    }
}
