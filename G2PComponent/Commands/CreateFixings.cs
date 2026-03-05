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
    // Parameter container — replace or extend fields to match your dictionary shape
    public class FixingParameters
    {
        public string Name { get; set; }
        public double Diameter { get; set; }
    }

    public class CreateFixingsCommand : Command
    {
        private static readonly Dictionary<string, FixingParameters> FixingParameters =
            new Dictionary<string, FixingParameters>
            {
        { "FS5",  new FixingParameters { Name = "S",   Diameter = 5  } },  // Screw predrilling
        { "FD12", new FixingParameters { Name = "D12", Diameter = 12 } },  // Solid dowels (cladding, siding)
        { "FD16", new FixingParameters { Name = "D16", Diameter = 16 } },  // Grooved dowels (structure)
        { "FM10", new FixingParameters { Name = "M10", Diameter = 11 } },  // Steel bolts
        { "FM16", new FixingParameters { Name = "M16", Diameter = 17 } },  // Steel bolts
            };

        public CreateFixingsCommand()
        {
            Instance = this;
        }

        public static CreateFixingsCommand Instance
        {
            get; private set;
        }

        public override string EnglishName => "CreateFixings";

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

            // 2. Get fixing type
            var gs = new GetString();
            gs.SetCommandPrompt("Type of fixing");
            gs.SetDefaultString("FSCR");
            foreach (var key in FixingParameters.Keys)
                gs.AddOption(key);

            gs.Get();
            if (gs.CommandResult() != Result.Success)
                return gs.CommandResult();

            string typeId = gs.StringResult().Trim();

            if (!FixingParameters.TryGetValue(typeId, out var fixing))
            {
                RhinoApp.WriteLine($"Unknown fixing type: {typeId}");
                return Result.Failure;
            }

            var drillType = Context.ComponentTypes.GetValueOrDefault(typeId);
            if (drillType == null)
            {
                return Result.Failure;
            }

            double diameter = fixing.Diameter;
            string basename = fixing.Name;
            double tolerance = doc.ModelAbsoluteTolerance;

            RhinoApp.WriteLine($"Fixing type: {typeId} — {basename}");

            // 3. Process each selected drill line
            for (int i = 0; i < go.ObjectCount; i++)
            {
                var rhinoObject = go.Object(i).Object();
                if (rhinoObject?.Geometry is not Curve curve)
                    continue;

                var axis = new Line(curve.PointAtStart, curve.PointAtEnd);

                // Build attributes with end arrowhead
                var attr = doc.CreateDefaultAttributes();
                attr.ObjectDecoration = ObjectDecoration.EndArrowhead;

                // Build drill cylinder
                var drillPlane = new Plane(axis.From, axis.Direction);
                var drillCircle = new Circle(drillPlane, diameter * 0.5);
                var drillVolume = new Cylinder(drillCircle, axis.Length);

                var drillComponent = new Component(drillType, $"{basename}", drillPlane);
                drillComponent.ReplaceMember(new ComponentMember(new LayerInfo("Geometry", drillType.LayerColor), [drillVolume.ToBrep(true, true)], doc.CreateDefaultAttributes()));
                drillComponent.ReplaceMember(new ComponentMember(new LayerInfo("Axis", drillType.LayerColor), [axis.ToNurbsCurve()], attr.Duplicate()));
                drillComponent.ReplaceMember(new ComponentMember(new LayerInfo("Diameter", drillType.LayerColor), [drillCircle.ToNurbsCurve()], doc.CreateDefaultAttributes()));

                attr = drillComponent.AttributeCollection[drillComponent.ID];
                attr.SetUserString("diameter", diameter.ToString());
                attr.SetUserString("depth", axis.Length.ToString());

                var guid = RHDoc.AddToRhinoDoc(drillComponent, doc, true);
            }

            doc.Views.Redraw();
            return Result.Success;
        }
    }
}
