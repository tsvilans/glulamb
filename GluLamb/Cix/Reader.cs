using GluLamb.Cix.Operations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Cix
{
    /// <summary>
    /// TODO: Upgrade GluLamb to .NET 8 or 9
    /// </summary>
    public class CixReader
    {
        public CixReader() 
        { 

        }

#if NET9
        public void Read(string cixPath)
        {
            var sides = new Dictionary<string, Dictionary<string, double>>()
            {
                {"E1", new Dictionary<string, double>()},
                {"E2", new Dictionary<string, double>()},
                {"IN", new Dictionary<string, double>()},
                {"OUT", new Dictionary<string, double>()},
                {"TOP", new Dictionary<string, double>()},
            };
            if (!System.IO.File.Exists(cixPath)) return;

            var lines = System.IO.File.ReadAllLines(cixPath);
            Operations = new List<Operation>();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (line.StartsWith("(") && line.EndsWith(")"))
                    continue;
                var keyValue = line.Split('=', StringSplitOptions.TrimEntries);
                if (keyValue.Length != 2) continue;

                var key = keyValue[0];
                var value = keyValue[1];

                var tok = key.Split('_', StringSplitOptions.TrimEntries);
                switch (tok[0])
                {
                    case ("IN"):
                        sides["IN"][string.Join('_', tok[1..])] = double.Parse(value);
                        break;
                    case ("OUT"):
                        sides["OUT"][string.Join('_', tok[1..])] = double.Parse(value);
                        break;
                    case ("E"):
                        if (tok[1] == "1")
                        {
                            sides["E1"][string.Join('_', tok[2..])] = double.Parse(value);
                            break;
                        }
                        else
                            sides["E2"][string.Join('_', tok[2..])] = double.Parse(value);
                        break;
                    default:
                        Console.WriteLine($"Found unknown parameter '{tok[0]}'.");
                        break;
                }
            }
        }

        List<Operation> Operations = null;

        public void FindHaks(Dictionary<string, double> cix, string prefix = "")
        {
            for (int i = 1; i < 10; ++i)
            {
                if (cix.ContainsKey($"HAK_{i}"))
                {
                    try
                    {
                        var cutout = CrossJointCutout.FromCix(cix, "", $"{i}");
                        if (cutout != null)
                        {
                            cutout.Name = $"{prefix}_{cutout.Name}";
                            Operations.Add(cutout);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"ERROR: {e.Message}");
                    }
                }
                else
                {
                    break;
                }
            }
        }

        public void FindEndCuts(Dictionary<string, double> cix, string prefix = "")
        {
            for (int i = 1; i < 10; ++i)
            {
                if (cix.ContainsKey($"CUT_{i}"))
                {
                    try
                    {
                        var endcut = EndCut.FromCix(cix, "", $"{i}");
                        if (endcut != null)
                        {
                            endcut.Name = $"{prefix}_{endcut.Name}";
                            Operations.Add(endcut);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"ERROR: {e.Message}");
                    }
                }
                else
                {
                    break;
                }
            }
        }
#endif
    }
}
