using D2P_Core;
using Rhino.Geometry;

using System.Drawing;

namespace G2PComponents
{
    public static class Features
    {
        public static Component CreatePocket(string name, Plane plane, Curve outline, double depth)
        {
            var attr = new Rhino.DocObjects.ObjectAttributes();
            attr.ObjectDecoration = Rhino.DocObjects.ObjectDecoration.EndArrowhead;

            outline = outline.DuplicateCurve();
            outline.Transform(Transform.ProjectAlong(plane, plane.ZAxis));

            var depthCurve = new Line(plane.Origin, -plane.ZAxis, depth);
            var comp = new Component(Context.PocketType, name, plane);

            comp.AddMember(new ComponentMember(new LayerInfo("Outline", Color.Lime),
                new GeometryBase[] { outline }, attr.Duplicate()));

            comp.AddMember(new ComponentMember(new LayerInfo("Depth", Color.Lime),
                new GeometryBase[] { depthCurve.ToNurbsCurve() }, attr.Duplicate()));

            return comp;
        }

        public static Component CreateProfile(string name, Plane plane, Curve profile, double depth)
        {
            var attr = new Rhino.DocObjects.ObjectAttributes();
            attr.ObjectDecoration = Rhino.DocObjects.ObjectDecoration.EndArrowhead;

            profile = profile.DuplicateCurve();
            profile.Transform(Transform.ProjectAlong(plane, plane.ZAxis));

            var depthCurve = new Line(plane.Origin, -plane.ZAxis, depth);
            var comp = new Component(Context.ProfileLeftType, name, plane);

            comp.AddMember(new ComponentMember(new LayerInfo("Outline", Color.Cyan),
                new GeometryBase[] { profile }, attr.Duplicate()));

            comp.AddMember(new ComponentMember(new LayerInfo("Depth", Color.Cyan),
                new GeometryBase[] { depthCurve.ToNurbsCurve() }, attr.Duplicate()));

            return comp;
        }
    }
}
