namespace GluLamb.Joints
{
    /// <summary>
    /// Birdsmouth: a pitched beam (e.g. a rafter) crossing over another (e.g. a wall plate) and
    /// notched to sit on it, with a seat on the plate's top and a plumb cut against its side;
    /// the seat runs out of the rafter's underside on the other side. The cross lap with an open
    /// side, and with the cut in the lower beam set by SeatDepth (0: the plate isn't cut at all).
    /// The rafter is the pitched beam, wherever its centreline is; Flip swaps which beam that is.
    /// </summary>
    [JointType("glulamb.cross-birdsmouth", Name = "Birdsmouth", Arity = 2, Topology = JointTopology.Cross,
        Description = "A pitched beam notched to sit on another it crosses, with a seat and a plumb cut.")]
    public class CrossBirdsmouthJoint : CrossLapJoint
    {
        [JointParameter(Description = "Depth of the seat cut into the lower beam (the plate), from its top. 0 = the plate isn't cut. (Used instead of LapDepth.)", Unit = "length")]
        public double SeatDepth { get; set; } = 0;

        public CrossBirdsmouthJoint(JointX condition) : base(condition)
        {
            OpenSide = 1;
        }

        protected override double? FixedLapDepth => System.Math.Max(0, SeatDepth);

        /// <summary>
        /// The rafter is the beam that is pitched to the seat, i.e. runs out of the plane both beams'
        /// sides span; when neither is (or both equally), the higher one. Flip swaps.
        /// </summary>
        protected override int OverPart(Beam[] beams, Rhino.Geometry.Vector3d up, double tolerance)
        {
            var planes = new[] { beams[0].GetPlane(m_parts[0].Parameter), beams[1].GetPlane(m_parts[1].Parameter) };
            var directions = new[] { beams[0].Centreline.TangentAt(m_parts[0].Parameter), beams[1].Centreline.TangentAt(m_parts[1].Parameter) };
            var seat = Rhino.Geometry.Vector3d.CrossProduct(
                Utility.ClosestAxis(planes[0], directions[1]),
                Utility.ClosestAxis(planes[1], directions[0]));
            if (!seat.Unitize())
                return base.OverPart(beams, up, tolerance);

            double pitch0 = System.Math.Abs(directions[0] * seat), pitch1 = System.Math.Abs(directions[1] * seat);
            if (System.Math.Abs(pitch0 - pitch1) < 1e-3)
                return base.OverPart(beams, up, tolerance);

            int rafter = pitch0 > pitch1 ? 0 : 1;
            return Flip ? 1 - rafter : rafter;
        }

        public static double Score(JointX condition, IJointContext context) =>
            JointRegistry.Classify(condition, JointX.PerpendicularThreshold) == JointTopology.Cross ? 0.5 : 0.0;
    }
}
