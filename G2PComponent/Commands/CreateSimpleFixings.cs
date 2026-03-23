using D2P_Core;
using D2P_Core.Utility;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace G2PComponents.Commands
{


    public class CreateSimpleFixingsCommand : Command
    {
        public CreateSimpleFixingsCommand()
        {
            Instance = this;
        }

        public static CreateSimpleFixingsCommand Instance
        {
            get; private set;
        }

        public override string EnglishName => "CreateSimpleFixings";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // 1. Pick drill lines
            var go = new GetObject();
            go.SetCommandPrompt("Pick drill lines");
            go.GeometryFilter = ObjectType.Curve;
            go.EnablePreSelect(true, true);
            go.GetMultiple(1, 0);

            if (go.CommandResult() != Result.Success)
                return go.CommandResult();

            var gd = new GetNumber();
            gd.SetCommandPrompt("Diameter of drilling");
            gd.SetDefaultNumber(12);

            gd.Get();
            if (gd.CommandResult() != Result.Success)
                return gd.CommandResult();

            double diameter = gd.Number();
            double tolerance = doc.ModelAbsoluteTolerance;

            var group = new List<Guid>();

            for (int i = 0; i < go.ObjectCount; i++)
            {
                var rhinoObject = go.Object(i).Object();
                if (rhinoObject?.Geometry is not Curve curve)
                    continue;

                var axis = new Line(curve.PointAtStart, curve.PointAtEnd);

                // Build attributes with end arrowhead
                var attr = doc.CreateDefaultAttributes();
                attr.ObjectDecoration = ObjectDecoration.EndArrowhead;
                attr.Name = $"M{diameter:0.#}";
                attr.SetUserString("diameter", $"{diameter:0.#}");
                attr.SetUserString("depth", $"{axis.Length:0.#}");
                attr.WireDensity = -1;

                // Build drill cylinder
                var drillPlane = new Plane(axis.From, axis.Direction);
                var drillCircle = new Circle(drillPlane, diameter * 0.5);
                var drillVolume = new Cylinder(drillCircle, axis.Length);

                var guid = doc.Objects.AddBrep(drillVolume.ToBrep(true, true), attr);
                group.Add(guid);
            }

            if (group.Count > 0)
            {
                doc.Groups.Add(group);
            }

            doc.Views.Redraw();
            return Result.Success;
        }
    }
}
