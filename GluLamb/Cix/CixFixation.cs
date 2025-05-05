using Rhino.Render.CustomRenderMeshes;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Cix
{
    /// <summary>
    /// Up to 8 beams can be used.
    /// The following rules apply:
    ///
    /// If 6 to 8 beams are used, the minimum distance between beams must be 300 mm.
    /// If only 1 to 4 beams are used, the minimum distance can be 229 mm.
    /// </summary>
    public class CixFixation
    {
        public double Length = 146;
        public double Width = 132;
        public double Safety = 17;
        public double Y = 0;

        public List<double> BeamPositions;

        public CixFixation()
        {
            BeamPositions = new List<double>();
        }

        public CixFixation(List<double> beamPositions)
        {
            BeamPositions = beamPositions;
        }

        public void ToCix(List<string> cix, string prefix = "")
        {
            cix.Add($"(FIXATION)");
            cix.Add($"{prefix}BEAM_N={BeamPositions.Count}");
            for (int i = 0; i < BeamPositions.Count; ++i)
            {
                cix.Add($"{prefix}BEAM_{i + 1}={BeamPositions[i]:0.###}");
            }

        }

        public CixFixation Duplicate()
        {
            var fixation = new CixFixation()
            {
                Length = Length,
                Width = Width,
                Safety = Safety,
                Y = Y
            };
            fixation.BeamPositions.AddRange(BeamPositions);

            return fixation;
        }
    }
}
