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


    public class ConnectManyComponentsCommand : Command
    {
        public ConnectManyComponentsCommand()
        {
            Instance = this;
        }

        public static ConnectManyComponentsCommand Instance
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



        public override string EnglishName => "ConnectManyComponents";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            if (doc == null) doc = RhinoDoc.ActiveDoc;

            var getterSource = new GetObject();
            getterSource.EnablePreSelect(true, true);
            getterSource.EnableUnselectObjectsOnExit(false);
            getterSource.SetCommandPrompt("Select components to connect");

            var optionSourceEdgeWidth = new OptionDouble(SourceEdgeWidth, 0, double.MaxValue);
            getterSource.AddOptionDouble("SourceEdgeWidth", ref optionSourceEdgeWidth);
            var optionTargetEdgeWidth = new OptionDouble(TargetEdgeWidth, 0, double.MaxValue);
            getterSource.AddOptionDouble("TargetEdgeWidth", ref optionTargetEdgeWidth);

            var optionSourceN = new OptionInteger(SourceN, 1, int.MaxValue);
            getterSource.AddOptionInteger("SourceN", ref optionSourceN);
            var optionTargetN = new OptionInteger(TargetN, 1, int.MaxValue);
            getterSource.AddOptionInteger("TargetN", ref optionTargetN);

            var optionDiameter = new OptionDouble(Diameter, 0, double.MaxValue);
            getterSource.AddOptionDouble("Diameter", ref optionDiameter);

            var optionExtraDepth = new OptionDouble(ExtraDepth, 0, double.MaxValue);
            getterSource.AddOptionDouble("ExtraDepth", ref optionExtraDepth);

            var optionMaxDepth = new OptionDouble(MaxDepth, 0, double.MaxValue);
            getterSource.AddOptionDouble("MaxDepth", ref optionMaxDepth);

            GetResult res;
            while (true)
            {
                res = getterSource.GetMultiple(1, 0);
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

            var sourceObjects = getterSource.Objects();

            var getterTarget = new GetObject();
            getterTarget.EnablePreSelect(false, false);
            getterTarget.EnableUnselectObjectsOnExit(false);
            getterTarget.SetCommandPrompt("Select target components");

            res = getterTarget.GetMultiple(1, 0);
            var targetObjects = getterTarget.Objects();

            SourceEdgeWidth = optionSourceEdgeWidth.CurrentValue;
            TargetEdgeWidth = optionTargetEdgeWidth.CurrentValue;
            SourceN = optionSourceN.CurrentValue;
            TargetN = optionTargetN.CurrentValue;

            Diameter = optionDiameter.CurrentValue;
            ExtraDepth = optionExtraDepth.CurrentValue;
            MaxDepth = optionMaxDepth.CurrentValue;

            var sources = Instantiation.InstancesFromObjects(sourceObjects.Select(x => x.ObjectId), Context.settings, doc);
            var targets = Instantiation.InstancesFromObjects(targetObjects.Select(x => x.ObjectId), Context.settings, doc);

            if (!sources.Any() || !targets.Any())
                return Result.Failure;

            var group = new List<Guid>();

            foreach (var source in sources)
            {
                foreach (var target in targets)
                {
                    RhinoApp.WriteLine($"Connecting {source.ShortName} to {target.ShortName}");

                    if (source == target) 
                    {
                        RhinoApp.WriteLine($"ERROR: Connecting components cannot be the same!");
                        continue;
                    }

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


                    foreach (var sdiv in sourceDivisors)
                    {
                        foreach (var tdiv in targetDivisors)
                        {
                            if (!Rhino.Geometry.Intersect.Intersection.PlanePlanePlane(matingPlane, sdiv, tdiv, out Point3d xpoint))
                            {
                                // Intersection failed. One of the planes is probably parallel, probably because
                                // the components are parallel along the X-axis.
                                continue;
                            }
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
