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
    }
}
