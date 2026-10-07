namespace GluLamb.Joints
{
    /// <summary>
    /// Blind corner lap: a corner half-lap where the first beam's tongue stops short of the
    /// second beam's outer side, so a lip of the second beam hides its end grain, pinned with a
    /// dowel. Port of BlindCornerJointX, which is CornerJointX with these settings; here it is
    /// the corner lap with a BlindOffset and a dowel by default.
    /// </summary>
    [JointType("glulamb.corner-lap-blind", Name = "Blind corner lap", Arity = 2,
        Description = "Two beam ends half-lapped at a corner, one end hidden behind a lip of the other, dowelled.")]
    public class BlindCornerLapJoint : CornerLapJoint
    {
        public BlindCornerLapJoint(JointX condition) : base(condition)
        {
            BlindOffset = 10.0;
            DowelDiameter = 16.0;
        }

        public static new double Score(JointX condition, IJointContext context) => CornerLapJoint.Score(condition, context) * 0.5;
    }
}
