namespace GluLamb.Reconstruction.Test
{
    internal static partial class Program
    {
        /// <summary>
        /// GluLamb.Reconstruction.Test --dualcon
        /// Remeshes a tetrahedron with dual contouring and prints the result (from GluLamb.Raw.Test).
        /// Needs the native dualcon.dll next to the executable.
        /// </summary>
        private static int RunDualCon()
        {
            var vertices = new float[] { 0, 0, 0, 1, 1, 0, 0, 1, 0, 1, 0, 1 };
            var faces = new int[] { 0, 1, 2, 1, 2, 3, 2, 3, 0, 3, 0, 1 };

            try
            {
                var dc = new DualCon();
                dc.Remesh(vertices, faces);

                Console.WriteLine("Vertices:");
                foreach (var v in dc.Output.Vertices.Where(v => v != null))
                    Console.WriteLine($"  {v[0]:0.000} {v[1]:0.000} {v[2]:0.000}");
                Console.WriteLine("Quads:");
                foreach (var f in dc.Output.Quads.Where(f => f != null))
                    Console.WriteLine($"  {f[0]} {f[1]} {f[2]} {f[3]}");
                return 0;
            }
            catch (DllNotFoundException e)
            {
                Console.WriteLine($"dualcon.dll not found: {e.Message}");
                return 1;
            }
        }
    }
}
