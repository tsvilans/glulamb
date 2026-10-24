using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.GH
{
    internal static class Plausibility
    {
        /// <summary>
        /// Warn when a lamella dimension looks like it is in the wrong units for the model, e.g.
        /// metres in a millimetre model: under 5 mm or over 500 mm.
        /// </summary>
        public static void LamellaSize(Grasshopper.Kernel.GH_ActiveObject component, string name, double size)
        {
            if (!(size > 0)) return;
            double mm = size / Globals.FromMillimeters(1.0);
            if (mm < 5 || mm > 500)
                component.AddRuntimeMessage(Grasshopper.Kernel.GH_RuntimeMessageLevel.Warning,
                    $"{name} {size:0.####} is {mm:0.###} mm in this {Globals.Units} model; check that it is in model units.");
        }
    }

    internal static class UiNames
    {
        public static string TopologySection   = "1. Topology";
        public static string BeamSection       = "2. Beam";
        public static string JointsSection     = "3. Joints";
        public static string BlankSection      = "4. Blank";
        public static string UtilitiesSection  = "5. Utilities";
    }
}
