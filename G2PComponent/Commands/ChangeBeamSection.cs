using D2P_Core.Interfaces;
using D2P_Core.Utility;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input;
using Rhino.Input.Custom;

namespace G2PComponents.Commands
{
    /// <summary>
    /// Give sketched beams (MakeBeam) a new section and rebuild their Breps. The beam keeps its
    /// ID; the label's Y-axis keeps the orientation. The Brep follows the Axis as it is now, so
    /// Enter (same section) rebuilds a beam after its centreline was edited.
    /// </summary>
    public class ChangeBeamSectionCommand : Command
    {
        public ChangeBeamSectionCommand()
        {
            Instance = this;
        }

        public static ChangeBeamSectionCommand Instance
        {
            get; private set;
        }

        public override string EnglishName => "ChangeBeamSection";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var go = new GetObject();
            go.SetCommandPrompt("Select beams");
            go.GroupSelect = true;
            go.SubObjectSelect = false;
            go.EnablePreSelect(true, true);
            go.GetMultiple(1, 0);
            if (go.CommandResult() != Result.Success)
                return go.CommandResult();

            var beams = Instantiation.InstancesFromObjects(go.Objects().Select(x => x.Object()), Context.settings, doc)
                .GroupBy(c => c.ID).Select(g => g.First())
                .Where(c => SketchBeam.IsBeamType(c.TypeID))
                .ToList();

            if (beams.Count == 0)
            {
                RhinoApp.WriteLine("No beams selected (made with MakeBeam, type B<w>x<h>).");
                return Result.Nothing;
            }

            if (!SketchBeam.GetSection(doc, beams[0].TypeID.Substring(1), out int width, out int height))
                return Result.Cancel;

            var oldTypes = beams.Select(c => c.ComponentType).GroupBy(t => t.TypeID).Select(g => g.First()).ToList();

            int changed = 0;
            foreach (var component in beams)
            {
                if (Rebuild(doc, component, width, height))
                    changed++;
            }

            foreach (var type in oldTypes)
                DeleteIfEmpty(doc, type);

            RhinoApp.WriteLine($"Changed {changed} of {beams.Count} beams to B{width}x{height}.");
            doc.Views.Redraw();
            return changed > 0 ? Result.Success : Result.Failure;
        }

        private static bool Rebuild(RhinoDoc doc, IComponent component, int width, int height)
        {
            var id = component.ShortName;
            var axis = SketchBeam.GetAxis(component, doc)?.DuplicateCurve();
            if (axis == null)
            {
                RhinoApp.WriteLine($"{id}: no Axis to rebuild from.");
                return false;
            }

            var beam = SketchBeam.Build(doc, id, axis, component.Plane.YAxis, width, height, out var brep, out var error);
            if (beam == null || brep == null)
            {
                RhinoApp.WriteLine(error);
                return false;
            }

            // Out with the old (label, Axis, Geometry and anything else on its layers), in with the new
            var old = component.RHObjects.Select(o => o.Id).ToList();
            foreach (var guid in old)
                doc.Objects.Delete(guid, true);

            SketchBeam.Add(doc, id, beam, brep);
            return true;
        }

        /// <summary>
        /// Remove a section's layers once no beam is left on them.
        /// </summary>
        private static void DeleteIfEmpty(RhinoDoc doc, D2P_Core.ComponentType type)
        {
            var root = Layers.GetComponentTypeRootLayer(type, doc);
            if (root == null) return;

            var layers = (root.GetChildren() ?? Array.Empty<Layer>()).Append(root).ToList();
            if (layers.Any(l => doc.Objects.FindByLayer(l)?.Length > 0))
                return;

            foreach (var layer in layers)
                doc.Layers.Delete(layer.Index, true);
        }
    }
}
