using System;
using System.Collections.Generic;
using System.Linq;

using Rhino;

namespace GluLamb.Joints
{
    /// <summary>
    /// Everything a joint needs from its surroundings: beams by identifier, and tolerances.
    /// Each host (Grasshopper, scripts, Rhino commands) supplies its own implementation.
    /// </summary>
    public interface IJointContext
    {
        /// <summary>
        /// Resolve a joint part to its beam. Returns null if it can't be found.
        /// </summary>
        Beam GetBeam(JointPartX part);

        /// <summary>
        /// Resolve a beam by identifier. Returns null if it can't be found.
        /// </summary>
        Beam GetBeam(string id);

        double Tolerance { get; }
        double AngleTolerance { get; }
    }

    /// <summary>
    /// IJointContext over a list of beams. Parts are resolved by BeamId, or by
    /// ElementIndex into the list when BeamId is empty, which keeps joints from
    /// index-based classification (e.g. Grasshopper lists) working.
    /// </summary>
    public class BeamCollection : IJointContext
    {
        private readonly List<Beam> m_beams;
        private readonly Dictionary<string, Beam> m_lookup;

        public double Tolerance { get; set; }
        public double AngleTolerance { get; set; }

        public IReadOnlyList<Beam> Beams => m_beams;

        /// <summary>
        /// Create a context from beams. Throws ArgumentException listing any duplicate
        /// identifiers, since resolving them would be ambiguous.
        /// </summary>
        public BeamCollection(IEnumerable<Beam> beams, double tolerance = 1e-3, double angleTolerance = 0)
        {
            m_beams = beams.ToList();
            Tolerance = tolerance;
            AngleTolerance = angleTolerance > 0 ? angleTolerance : RhinoMath.ToRadians(1.0);

            var duplicates = m_beams.Where(x => x != null)
                .GroupBy(x => x.Id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Count > 0)
                throw new ArgumentException($"Duplicate beam ids: {string.Join(", ", duplicates)}");

            m_lookup = m_beams.Where(x => x != null).ToDictionary(x => x.Id);
        }

        public Beam GetBeam(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return m_lookup.TryGetValue(id, out var beam) ? beam : null;
        }

        public Beam GetBeam(JointPartX part)
        {
            if (part == null) return null;
            if (!string.IsNullOrEmpty(part.BeamId))
                return GetBeam(part.BeamId);

            if (part.ElementIndex >= 0 && part.ElementIndex < m_beams.Count)
                return m_beams[part.ElementIndex];

            return null;
        }
    }
}
