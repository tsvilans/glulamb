using D2P_Core.Interfaces;
using D2P_Core.Utility;
using Rhino;
using Rhino.Commands;
using Rhino.Display;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace G2PComponents.Commands
{
    /// <summary>
    /// Sets the edge profile (e.g. tongue and groove) of board components, applied by
    /// GenerateDetailed. Shows each board's Edge1 and Edge2 while the options are edited.
    /// Profile=None removes it. Starts from the first selected board's profile, or the last one set.
    /// </summary>
    public class SetEdgeProfileCommand : Command
    {
        public SetEdgeProfileCommand()
        {
            Instance = this;
        }

        public static SetEdgeProfileCommand Instance { get; private set; }

        public override string EnglishName => "SetEdgeProfile";

        private static EdgeProfile Last = new EdgeProfile { Type = EdgeProfileType.TongueAndGroove };

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var go = new GetObject();
            go.SetCommandPrompt("Select boards");
            go.EnablePreSelect(true, true);
            go.EnableUnselectObjectsOnExit(false);
            if (go.GetMultiple(1, 0) != GetResult.Object) return Result.Cancel;

            var components = Instantiation.InstancesFromObjects(go.Objects().Select(x => x.Object()), Context.settings, doc)
                .GroupBy(x => x.ID).Select(g => g.First())
                .Where(x => !x.ShortName.Contains(Context.settings.JointDelimiter))
                .ToList();
            if (components.Count == 0)
            {
                RhinoApp.WriteLine("No components selected.");
                return Result.Failure;
            }

            var profile = (components.Select(x => EdgeProfile.Read(x, doc)).FirstOrDefault(x => x != null) ?? Last).Duplicate();
            if (profile.Type == EdgeProfileType.None) profile.Type = EdgeProfileType.TongueAndGroove;

            var conduit = new EdgeConduit(components, doc) { Profile = profile, Enabled = true };
            doc.Views.Redraw();
            try
            {
                if (!EditOptions(profile, doc)) return Result.Cancel;
            }
            finally
            {
                conduit.Enabled = false;
            }

            int set = 0;
            foreach (var component in components)
            {
                var obj = doc.Objects.FindId(component.ID);
                if (obj == null) continue;
                var attributes = obj.Attributes.Duplicate();
                profile.Write(attributes);
                if (doc.Objects.ModifyAttributes(obj, attributes, true)) set++;
            }

            if (profile.Type != EdgeProfileType.None) Last = profile.Duplicate();
            RhinoApp.WriteLine(profile.Type == EdgeProfileType.None
                ? $"Removed the edge profile from {set} components."
                : $"Set {profile.Type} edges on {set} components; GenerateDetailed applies them.");
            doc.Views.Redraw();
            return Result.Success;
        }

        private static bool EditOptions(EdgeProfile profile, RhinoDoc doc)
        {
            var types = Enum.GetNames(typeof(EdgeProfileType));
            var kinds = Enum.GetNames(typeof(EdgeKind));

            while (true)
            {
                var gp = new GetOption();
                gp.SetCommandPrompt("Edge profile (Enter to apply)");
                gp.AcceptNothing(true);

                var tongueWidth = new OptionDouble(profile.TongueWidth, 0, double.MaxValue);
                var tongueDepth = new OptionDouble(profile.TongueDepth, 0, double.MaxValue);
                var grooveWidth = new OptionDouble(profile.GrooveWidth, 0, double.MaxValue);
                var grooveDepth = new OptionDouble(profile.GrooveDepth, 0, double.MaxValue);
                var offset = new OptionDouble(profile.Offset);
                var protrude = new OptionToggle(profile.Protrude, "No", "Yes");

                int typeOption = gp.AddOptionList("Profile", types, (int)profile.Type);
                int edge1Option = -1, edge2Option = -1, swapOption = -1;
                if (profile.Type != EdgeProfileType.None)
                {
                    edge1Option = gp.AddOptionList("Edge1", kinds, (int)profile.Edge1);
                    edge2Option = gp.AddOptionList("Edge2", kinds, (int)profile.Edge2);
                    swapOption = gp.AddOption("Swap");
                    gp.AddOptionDouble("TongueWidth", ref tongueWidth);
                    gp.AddOptionDouble("TongueDepth", ref tongueDepth);
                    gp.AddOptionDouble("GrooveWidth", ref grooveWidth, "Groove width (0 = the tongue's)");
                    gp.AddOptionDouble("GrooveDepth", ref grooveDepth, "Groove depth (0 = the tongue's)");
                    gp.AddOptionDouble("Offset", ref offset);
                    gp.AddOptionToggle("TongueProtrudes", ref protrude);
                }

                var res = gp.Get();
                if (res == GetResult.Nothing) return true;
                if (res != GetResult.Option) return false;

                var index = gp.OptionIndex();
                if (index == typeOption) profile.Type = (EdgeProfileType)gp.Option().CurrentListOptionIndex;
                else if (index == edge1Option) profile.Edge1 = (EdgeKind)gp.Option().CurrentListOptionIndex;
                else if (index == edge2Option) profile.Edge2 = (EdgeKind)gp.Option().CurrentListOptionIndex;
                else if (index == swapOption) (profile.Edge1, profile.Edge2) = (profile.Edge2, profile.Edge1);

                profile.TongueWidth = tongueWidth.CurrentValue;
                profile.TongueDepth = tongueDepth.CurrentValue;
                profile.GrooveWidth = grooveWidth.CurrentValue;
                profile.GrooveDepth = grooveDepth.CurrentValue;
                profile.Offset = offset.CurrentValue;
                profile.Protrude = protrude.CurrentValue;
                doc.Views.Redraw();
            }
        }

        /// <summary>
        /// Shows what each board's two long edges get, and the profiled section at the board's
        /// start.
        /// </summary>
        private class EdgeConduit : DisplayConduit
        {
            private readonly List<(Point3d Edge1, Point3d Edge2, Plane Section, double Width, double Thickness)> m_boards = new();
            public EdgeProfile Profile;

            public EdgeConduit(IEnumerable<IComponent> components, RhinoDoc doc)
            {
                foreach (var component in components)
                    if (EdgeProfile.EdgeMarkers(component, doc, out var e1, out var e2)
                        && EdgeProfile.Frame(component, doc, out var section, out _, out var width, out var thickness))
                        m_boards.Add((e1, e2, section, width, thickness));
            }

            protected override void DrawForeground(DrawEventArgs e)
            {
                var colour = System.Drawing.Color.DarkOrange;
                foreach (var board in m_boards)
                {
                    e.Display.DrawDot(board.Edge1, $"1: {Profile.Edge1}", colour, System.Drawing.Color.White);
                    e.Display.DrawDot(board.Edge2, $"2: {Profile.Edge2}", System.Drawing.Color.SteelBlue, System.Drawing.Color.White);
                }
            }

            protected override void PostDrawObjects(DrawEventArgs e)
            {
                if (Profile.Type == EdgeProfileType.None) return;
                var messages = new List<string>();
                foreach (var board in m_boards)
                {
                    var section = Profile.Section(board.Width, board.Thickness, messages, "");
                    if (section == null) continue;
                    section.Transform(Transform.PlaneToPlane(Plane.WorldXY, board.Section));
                    e.Display.DrawPolyline(section, System.Drawing.Color.DarkOrange, 3);
                }
            }
        }
    }
}
