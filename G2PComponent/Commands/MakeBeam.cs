using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input;
using Rhino.Input.Custom;

namespace G2PComponents.Commands
{
    /// <summary>
    /// Make beams from centrelines: pick curves, give a section (45x95, or a preset). Each beam
    /// gets the next ID from the document's beam counter. The picked curve becomes the beam's Axis.
    /// </summary>
    public class MakeBeamCommand : Command
    {
        public MakeBeamCommand()
        {
            Instance = this;
        }

        public static MakeBeamCommand Instance
        {
            get; private set;
        }

        public override string EnglishName => "MakeBeam";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var go = new GetObject();
            go.SetCommandPrompt("Select beam centrelines");
            go.GeometryFilter = ObjectType.Curve;
            go.SubObjectSelect = false;
            go.EnablePreSelect(true, true);
            go.GetMultiple(1, 0);
            if (go.CommandResult() != Result.Success)
                return go.CommandResult();

            var objRefs = go.Objects();

            if (!SketchBeam.GetSection(doc, SketchBeam.LastSection, out int width, out int height))
                return Result.Cancel;

            double tolerance = doc.ModelAbsoluteTolerance;
            int made = 0;

            foreach (var objRef in objRefs)
            {
                var curve = objRef.Curve()?.DuplicateCurve();
                if (curve == null) continue;

                var id = SketchBeam.NextId(doc);
                var startY = SketchBeam.DefaultStartY(curve, tolerance);

                var beam = SketchBeam.Build(doc, id, curve, startY, width, height, out var brep, out var error);
                if (beam == null || brep == null)
                {
                    RhinoApp.WriteLine(error);
                    continue;
                }

                SketchBeam.Add(doc, id, beam, brep);

                // The centreline lives on as the beam's Axis
                doc.Objects.Delete(objRef, true);
                RhinoApp.WriteLine($"{id}: B{width}x{height}");
                made++;
            }

            RhinoApp.WriteLine($"Made {made} of {objRefs.Length} beams.");
            doc.Views.Redraw();
            return made > 0 ? Result.Success : Result.Failure;
        }
    }
}
