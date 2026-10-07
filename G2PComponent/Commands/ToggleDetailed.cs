using Rhino;
using Rhino.Commands;

namespace G2PComponents.Commands
{
    /// <summary>
    /// Switches every component type between showing its detailed and its basic geometry, by
    /// layer. Types without a DetailedGeometry layer keep showing their Geometry.
    /// </summary>
    public class ToggleDetailedCommand : Command
    {
        public ToggleDetailedCommand()
        {
            Instance = this;
        }

        public static ToggleDetailedCommand Instance { get; private set; }

        public override string EnglishName => "ToggleDetailed";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            bool detailed = !Detailing.ShowingDetailed(doc);
            int changed = Detailing.Show(doc, detailed);
            RhinoApp.WriteLine(changed == 0
                ? "No component type has detailed geometry yet."
                : $"Showing {(detailed ? "detailed" : "basic")} geometry.");

            doc.Views.Redraw();
            return Result.Success;
        }
    }
}
