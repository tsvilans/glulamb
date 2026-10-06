using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

using GluLamb.Joints;

namespace GluLamb.Gallery
{
    internal static class Program
    {
        /// <summary>
        /// GluLamb.Gallery [--out folder] [--docs folder] [--types filter] [--no-3dm] [--verbose]
        ///   --out      where to write the results (default: ./gallery-out): summary.txt, and per
        ///              joint type (in types/) a .json of its cells, a .3dm of its grid and a .png drawing
        ///   --docs     where to write the catalogue (README.md and one SVG per type), e.g. docs/joints
        ///   --types    only joint type ids containing this text
        ///   --no-3dm   skip the .3dm files
        ///   --verbose  list each beam's features and how much of it is kept
        /// Each joint type runs in its own process (Rhino can lose its licence during a long run,
        /// e.g. to another Rhino session); a type whose process loses it is tried again. Exits
        /// with 1 if any cell fails.
        /// </summary>
        [STAThread]
        private static int Main(string[] args)
        {
            RhinoInside.Resolver.Initialize();
            var options = Options.Parse(args);
            return options.Worker != null ? Worker(options) : Driver(options, args);
        }

        /// <summary>
        /// Run each joint type in its own process, then write the summary and the catalogue.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Driver(Options options, string[] args)
        {
            var types = JointRegistry.Default.Types
                .Where(t => options.Types == null || t.Id.Contains(options.Types))
                .OrderBy(t => t.Id)
                .ToList();
            foreach (var error in JointRegistry.Default.Errors)
                Console.WriteLine($"Registry: {error}");

            var parts = Path.Combine(options.Out, "types");
            Directory.CreateDirectory(parts);

            // Pass everything but the type filter on to the workers
            var forward = new List<string>();
            for (int i = 0; i < args.Length; ++i)
            {
                if (args[i] == "--types") { ++i; continue; }
                forward.Add(args[i]);
            }

            foreach (var type in types)
            {
                var result = Path.Combine(parts, type.Id + ".json");
                if (File.Exists(result)) File.Delete(result);

                for (int attempt = 1; attempt <= 3; ++attempt)
                {
                    var info = new ProcessStartInfo(Environment.ProcessPath) { UseShellExecute = false };
                    foreach (var a in forward) info.ArgumentList.Add(a);
                    info.ArgumentList.Add("--worker");
                    info.ArgumentList.Add(type.Id);

                    using (var process = Process.Start(info))
                    {
                        process.WaitForExit();
                        if (process.ExitCode != 2) break;
                    }
                    Console.WriteLine($"{type.Id}: lost the Rhino licence, trying again ({attempt}).");
                }
            }

            // Gather the results
            var records = new List<TypeRecord>();
            foreach (var type in types)
            {
                var file = Path.Combine(parts, type.Id + ".json");
                if (!File.Exists(file))
                {
                    Console.WriteLine($"{type.Id}: no results.");
                    continue;
                }
                records.Add(JsonSerializer.Deserialize<TypeRecord>(File.ReadAllText(file)));
            }

            var cells = records.SelectMany(r => r.Cells.Select(c => (r.Id, Cell: c))).ToList();
            File.WriteAllLines(Path.Combine(options.Out, "summary.txt"),
                cells.Select(x => $"{(x.Cell.Pass ? "ok" : "FAIL")}\t{x.Id}\t{x.Cell.Case}\t{x.Cell.Variant}\t{string.Join("; ", x.Cell.Problems.Concat(x.Cell.Messages))}"));

            var failed = cells.Count(x => !x.Cell.Pass);
            Console.WriteLine($"{cells.Count} cells, {failed} failed, over {records.Count} joint types.");

            if (options.Docs != null)
                Catalogue.Write(options.Docs, types, records);

            return failed == 0 && records.Count == types.Count ? 0 : 1;
        }

        /// <summary>
        /// One joint type, in Rhino: exits with 2 if Rhino loses its licence.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Worker(Options options)
        {
            using (new Rhino.Runtime.InProcess.RhinoCore(new[] { "/netcore", "/nosplash" }, Rhino.Runtime.InProcess.WindowStyle.NoWindow))
            {
                if (Rhino.RhinoDoc.ActiveDoc == null)
                    Rhino.RhinoDoc.ActiveDoc = Rhino.RhinoDoc.Create(null);

                try
                {
                    return new Gallery(options).Run(options.Worker) ? 0 : 1;
                }
                catch (Rhino.Runtime.NotLicensedException)
                {
                    return 2;
                }
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
        public string Worker = null;

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
                    case "--worker": o.Worker = args[++i]; break;
                    default: throw new ArgumentException($"Unknown argument {args[i]}");
                }
            }
            return o;
        }
    }

    /// <summary>
    /// One joint type's results, written by its worker process.
    /// </summary>
    internal class TypeRecord
    {
        public string Id { get; set; }
        public Dictionary<string, string> Defaults { get; set; } = new Dictionary<string, string>();
        public List<OfferRecord> Offered { get; set; } = new List<OfferRecord>();
        public List<CellRecord> Cells { get; set; } = new List<CellRecord>();
    }

    internal class OfferRecord
    {
        public string Case { get; set; }
        public string Family { get; set; }
        public bool Default { get; set; }
    }

    internal class CellRecord
    {
        public string Case { get; set; }
        public string Variant { get; set; }
        public bool Pass { get; set; }
        public List<string> Problems { get; set; } = new List<string>();
        public List<string> Messages { get; set; } = new List<string>();
    }
}
