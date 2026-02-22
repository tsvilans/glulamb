using System.Drawing;
using D2P_Core;

namespace G2PComponents
{
    public static class Context
    {
        public static readonly Settings settings = new D2P_Core.Settings() { RootLayerName = "tas" };
        public static readonly ComponentType ProfileLeftType = new ComponentType("XPRL", "Profile - left offset", settings, 1.0, Color.Cyan);
        public static readonly ComponentType ProfileRightType = new ComponentType("XPRR", "Profile - right offset", settings, 1.0, Color.Cyan);
        public static readonly ComponentType EdgeType = new ComponentType("XEDG", "Edge", settings, 1.0, Color.Magenta);
        public static readonly ComponentType PocketType = new ComponentType("XPOC", "Pocket", settings, 1.0, Color.Lime);

        public static readonly Dictionary<string, ComponentType> ComponentTypes = new Dictionary<string, ComponentType>
        {
            { "PCOL", new ComponentType("PCOL", "Primary (Columns)", settings, 5.0, Color.Blue) },
            {"PTOP", new ComponentType("PTOP", "Primary (Top)", settings, 5.0, Color.Blue)},
            {"PMID", new ComponentType("PMID", "Primary (Mid)", settings, 5.0, Color.Blue)},
            { "PBTM", new ComponentType("PBTM", "Primary (Bottom)", settings, 5.0, Color.Blue)},

            { "STOP", new ComponentType("STOP", "Secondary (Top)", settings, 5.0, Color.Green)},
            { "SMID", new ComponentType("SMID", "Secondary (Mid)", settings, 5.0, Color.Green)},
            { "SBTM", new ComponentType("SBTM", "Secondary (Bottom)", settings, 5.0, Color.Green)},

            { "TBEN", new ComponentType("TBEN", "Tertiary (Benches)", settings, 5.0, Color.Tan)},
            { "TBTM", new ComponentType("TBTM", "Tertiary (Bottom)", settings, 5.0, Color.Tan)},
            { "TMID", new ComponentType("TMID", "Tertiary (Mid)", settings, 5.0, Color.Tan)},
            { "TTOP", new ComponentType("TTOP", "Tertiary (Top)", settings, 5.0, Color.Tan)},
            { "TRAI", new ComponentType("TRAI", "Tertiary (Railing)", settings, 5.0, Color.Tan)},
            { "TSTR", new ComponentType("TSTR", "Tertiary (Staircase)", settings, 5.0, Color.Tan)},

            { "EDRS", new ComponentType("EDRS", "Envelope (Doors)", settings, 5.0, Color.Maroon)},
            { "EHNG", new ComponentType("EHNG", "Envelope (Hinges)", settings, 5.0, Color.Maroon)},
            { "ESTR", new ComponentType("ESTR", "Envelope (Staircase)", settings, 5.0, Color.Maroon)},
            { "EHAT", new ComponentType("EHAT", "Envelope (Hatch)", settings, 5.0, Color.Maroon)},
            { "EDCK", new ComponentType("EDCK", "Envelope (Decking)", settings, 5.0, Color.Maroon)},
            { "EROF", new ComponentType("EROF", "Envelope (Roofing)", settings, 5.0, Color.Maroon)},
            { "ESID", new ComponentType("ESID", "Envelope (Siding)", settings, 5.0, Color.Maroon)},

            { "FSCR", new ComponentType("FSCR", "Fixing (Screw)", settings, 2.0, Color.Salmon)},
            { "FDW1", new ComponentType("FDW1", "Fixing (Cladding dowel, 12mm)", settings, 2.0, Color.DeepPink)},
            { "FDW2", new ComponentType("FDW2", "Fixing (Structural dowel, 16mm)", settings, 2.0, Color.OrangeRed)},
            { "FM10", new ComponentType("FM10", "Fixing (Structural bolt, 10mm)", settings, 2.0, Color.Red)},
            { "FM16", new ComponentType("FM16", "Fixing (Structural bolt, 16mm)", settings, 2.0, Color.Red)}
        };
    }
}
