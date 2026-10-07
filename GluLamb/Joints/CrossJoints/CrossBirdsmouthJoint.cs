namespace GluLamb.Joints
{
    /// <summary>
    /// Birdsmouth: a pitched beam (e.g. a rafter) crossing over another (e.g. a wall plate) and
    /// notched to sit on it, with a seat on the plate's top and a plumb cut against its side;
    /// the seat runs out of the rafter's underside on the other side. The cross lap with an open
    /// side, and with the cut in the lower beam set by SeatDepth (0: the plate isn't cut at all).
    /// The upper beam is the rafter; Flip swaps which beam that is.
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

        public static double Score(JointX condition, IJointContext context) =>
            JointRegistry.Classify(condition, JointX.PerpendicularThreshold) == JointTopology.Cross ? 0.5 : 0.0;
    }
}
