using System.Runtime.CompilerServices;

namespace GluLamb.Gallery
{
    internal static class Program
    {
        /// <summary>
        /// GluLamb.Gallery [--out folder] [--docs folder] [--types filter] [--no-3dm] [--verbose]
        ///   --out    where to write gallery.3dm and the run summary (default: ./gallery-out)
        ///   --docs   where to write the catalogue (README.md and one SVG per type), e.g. docs/joints
        ///   --types  only joint type ids containing this text
        ///   --no-3dm skip the .3dm
        ///   --verbose  list each beam's features and how much of it is kept
        /// Exits with 1 if any cell fails.
        /// </summary>
        [STAThread]
        private static int Main(string[] args)
        {
            RhinoInside.Resolver.Initialize();
            return Run(args);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Run(string[] args)
        {
            var options = Options.Parse(args);
            using (new Rhino.Runtime.InProcess.RhinoCore(new[] { "/netcore", "/nosplash" }, Rhino.Runtime.InProcess.WindowStyle.NoWindow))
            {
                if (Rhino.RhinoDoc.ActiveDoc == null)
                    Rhino.RhinoDoc.ActiveDoc = Rhino.RhinoDoc.Create(null);

                var gallery = new Gallery(options);
                return gallery.Run() ? 0 : 1;
            }
        }
    }

    internal class Options
    {
        public string Out = "gallery-out";
        public string Docs = null;
        public string Types = null;
        public bool Write3dm = true;
        public bool Verbose = false;

        public static Options Parse(string[] args)
        {
            var o = new Options();
            for (int i = 0; i < args.Length; ++i)
            {
                switch (args[i])
                {
                    case "--out": o.Out = args[++i]; break;
                    case "--docs": o.Docs = args[++i]; break;
                    case "--types": o.Types = args[++i]; break;
                    case "--no-3dm": o.Write3dm = false; break;
                    case "--verbose": o.Verbose = true; break;
                    default: throw new ArgumentException($"Unknown argument {args[i]}");
                }
            }
            return o;
        }
    }
}
