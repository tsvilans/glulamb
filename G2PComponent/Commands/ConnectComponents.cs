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


    public class ConnectComponentsCommand : Command
    {
        public ConnectComponentsCommand()
        {
            Instance = this;
        }

        public static ConnectComponentsCommand Instance
        {
            get; private set;
        }

        private double SourceEdgeWidth = 20;
        private double TargetEdgeWidth = 20;
        private int SourceN = 2;
        private int TargetN = 2;
        private double Diameter = 5.0;
        private double MaxDepth = 0;
        private double ExtraDepth = 0;



        public override string EnglishName => "ConnectComponents";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            if (doc == null) doc = RhinoDoc.ActiveDoc;

            var getter = new GetObject();
            getter.EnablePreSelect(true, true);
            getter.EnableUnselectObjectsOnExit(false);
            getter.SetCommandPrompt("Select components to connect");

            var optionSourceEdgeWidth = new OptionDouble(SourceEdgeWidth, 0, double.MaxValue);
            getter.AddOptionDouble("SourceEdgeWidth", ref optionSourceEdgeWidth);
            var optionTargetEdgeWidth = new OptionDouble(TargetEdgeWidth, 0, double.MaxValue);
            getter.AddOptionDouble("TargetEdgeWidth", ref optionTargetEdgeWidth);

            var optionSourceN = new OptionInteger(SourceN, 1, int.MaxValue);
            getter.AddOptionInteger("SourceN", ref optionSourceN);
            var optionTargetN = new OptionInteger(TargetN, 1, int.MaxValue);
            getter.AddOptionInteger("TargetN", ref optionTargetN);

            var optionDiameter = new OptionDouble(Diameter, 0, double.MaxValue);
            getter.AddOptionDouble("Diameter", ref optionDiameter);

            var optionExtraDepth = new OptionDouble(ExtraDepth, 0, double.MaxValue);
            getter.AddOptionDouble("ExtraDepth", ref optionExtraDepth);

            var optionMaxDepth = new OptionDouble(MaxDepth, 0, double.MaxValue);
            getter.AddOptionDouble("MaxDepth", ref optionMaxDepth);

            GetResult res;
            while (true)
            {
                res = getter.GetMultiple(2, 2);
                if (res == GetResult.Option)
                {
                    continue;
                }
                if (res == GetResult.Nothing)
                    break; // Enter pressed, proceed
                if (res == GetResult.Cancel)
                    return Result.Cancel;
                if (res == GetResult.Object)
                    break;
                break;
            }

            var sourceObject = getter.Object(0);
            var targetObject = getter.Object(1);

            SourceEdgeWidth = optionSourceEdgeWidth.CurrentValue;
            TargetEdgeWidth = optionTargetEdgeWidth.CurrentValue;
            SourceN = optionSourceN.CurrentValue;
            TargetN = optionTargetN.CurrentValue;

            Diameter = optionDiameter.CurrentValue;
            ExtraDepth = optionExtraDepth.CurrentValue;
            MaxDepth = optionMaxDepth.CurrentValue;

            var source = Instantiation.InstancesFromObjects(new Guid[] { sourceObject.ObjectId }, Context.settings, doc).FirstOrDefault();
            var target = Instantiation.InstancesFromObjects(new Guid[] { targetObject.ObjectId }, Context.settings, doc).FirstOrDefault();

            if (source == null || target == null)
                return Result.Failure;

            RhinoApp.WriteLine($"Connecting {source.ShortName} to {target.ShortName}");

            if (source == target) throw new ArgumentException("Components cannot be the same!");

            var sourceBounds = Utility.GetComponentBounds(source, out Box sourceBox, doc);
            var targetBounds = Utility.GetComponentBounds(target, out Box targetBox, doc);

            var sourceFaces = Utility.DeBox(sourceBox);
            var targetFaces = Utility.DeBox(targetBox);

            BoxMating.FindMatingFaces(sourceFaces, targetFaces, 0.1, 5.0, out Rectangle3d bestA, out Rectangle3d bestB);

            var matingPlane = bestA.Plane;

            var (sourceWidth, sourceHeight) = Utility.GetRelativeDimensions(sourceBox, bestA.Plane.Normal);
            var (targetWidth, targetHeight) = Utility.GetRelativeDimensions(targetBox, bestB.Plane.Normal);

            var sourceDivisors = Utility.MakeDivisors(bestA.Plane, sourceWidth, SourceN, SourceEdgeWidth, Diameter * 1.5);

            // var sourceDivisors = MakeDivisors(bestA.Plane, sourceWidth, 1, 22.5);
            var targetDivisors = Utility.MakeDivisors(bestB.Plane, targetWidth, TargetN, TargetEdgeWidth, Diameter * 1.5);

            MaxDepth = MaxDepth > 0 ? MaxDepth : double.MaxValue;

            var group = new List<Guid>();

            foreach (var sdiv in sourceDivisors)
            {
                foreach (var tdiv in targetDivisors)
                {
                    Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(matingPlane, sdiv, tdiv, out Point3d xpoint);

                    var cylinder = new Cylinder(
                        new Circle(
                            new Plane(xpoint - matingPlane.Normal * (sourceHeight), matingPlane.Normal),
                            Diameter / 2
                        ), Math.Min(sourceHeight + ExtraDepth, MaxDepth)
                    );

                    var attr = doc.CreateDefaultAttributes();
                    attr.WireDensity = -1;

                    var guid = doc.Objects.AddBrep(cylinder.ToBrep(true, true), attr);
                    group.Add(guid);

                }
            }

            if (group.Count > 0)
            {
                doc.Groups.Add(group);
                doc.Objects.Select(group);
            }

            doc.Views.Redraw();
            return Result.Success;
        }
    }
}
