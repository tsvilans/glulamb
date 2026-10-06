using System.Reflection;

using D2P_Core.Utility;
using GluLamb;
using GluLamb.Joints;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input;
using Rhino.Input.Custom;

namespace G2PComponents.Commands
{
    /// <summary>
    /// Joins two or more components with a GluLamb joint: finds the joint condition between them,
    /// offers the joint types that handle it (the registry's choice first) and then the chosen
    /// type's parameters as command options, builds the joint and adds it to the document.
    /// </summary>
    public class JoinComponentsCommand : Command
    {
        public JoinComponentsCommand()
        {
            Instance = this;
        }

        public static JoinComponentsCommand Instance { get; private set; }

        public override string EnglishName => "JoinComponents";

        // Parameters last used per joint type, so repeated joins keep their settings
        private static readonly Dictionary<string, Dictionary<string, object>> LastParameters = new();

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            ObjRef[] objRefs;
            var rc = RhinoGet.GetMultipleObjects("Select components to join", true, ObjectType.AnyObject, out objRefs);
            if (rc != Result.Success || objRefs == null || objRefs.Length == 0)
                return rc;

            var components = Instantiation.InstancesFromObjects(objRefs.Select(x => x.Object()), Context.settings, doc)
                .GroupBy(x => x.ID).Select(g => g.First()).ToList();
            if (components.Count < 2)
            {
                RhinoApp.WriteLine("Select at least two components.");
                return Result.Failure;
            }

            List<Beam> beams;
            try
            {
                beams = Joining.Beams(components);
            }
            catch (ArgumentException e)
            {
                RhinoApp.WriteLine(e.Message);
                return Result.Failure;
            }

            var condition = Joining.Condition(beams);
            if (condition == null)
            {
                RhinoApp.WriteLine("These components don't meet at one place.");
                return Result.Failure;
            }

            var context = new BeamCollection(beams, doc.ModelAbsoluteTolerance);
            var candidates = JointRegistry.Default.Candidates(condition, context);
            if (candidates.Count == 0)
            {
                RhinoApp.WriteLine($"No joint type handles this {JointRegistry.Classify(condition, JointX.PerpendicularThreshold)} condition.");
                return Result.Failure;
            }

            // Joint type
            var go = new GetOption();
            go.SetCommandPrompt($"Joint type for {string.Join(", ", components.Select(x => x.ShortName))} (Enter for {candidates[0].Info.Name})");
            go.AcceptNothing(true);
            var byOption = new Dictionary<int, JointTypeInfo>();
            foreach (var (info, _) in candidates)
                byOption[go.AddOption(Joining.OptionName(info.Id))] = info;

            var type = candidates[0].Info;
            var res = go.Get();
            if (res == GetResult.Cancel) return Result.Cancel;
            if (res == GetResult.Option && byOption.TryGetValue(go.OptionIndex(), out var picked))
                type = picked;

            var joint = type.Create(condition);
            if (LastParameters.TryGetValue(type.Id, out var last))
                JointParameters.Set(joint, last);

            // Parameters
            if (!EditParameters(joint, type))
                return Result.Cancel;
            LastParameters[type.Id] = JointParameters.Get(joint);

            var result = joint.Construct(context);
            foreach (var message in result.Messages)
                RhinoApp.WriteLine($"-- {message}");
            if (!result.Success)
            {
                RhinoApp.WriteLine($"{type.Name} failed: {result.Status}.");
                return Result.Failure;
            }

            foreach (var kv in result.Extensions)
                RhinoApp.WriteLine($"-- {kv.Key} needs extending by {kv.Value.Start:0.#} at its start and {kv.Value.End:0.#} at its end to hold the joint.");

            var name = Joining.Store(doc, components, beams, joint, result);
            RhinoApp.WriteLine($"Joined {string.Join(", ", components.Select(x => x.ShortName))} with {type.Name} as {name}.");

            doc.Views.Redraw();
            return Result.Success;
        }

        /// <summary>
        /// The joint's parameters as command options (numbers, whole numbers, toggles), until Enter.
        /// </summary>
        private static bool EditParameters(IJoint joint, JointTypeInfo type)
        {
            var properties = type.Parameters.ToList();
            while (true)
            {
                var go = new GetOption();
                go.SetCommandPrompt($"{type.Name} parameters (Enter to build)");
                go.AcceptNothing(true);

                var doubles = new Dictionary<int, (PropertyInfo Property, OptionDouble Option)>();
                var ints = new Dictionary<int, (PropertyInfo Property, OptionInteger Option)>();
                var toggles = new Dictionary<int, (PropertyInfo Property, OptionToggle Option)>();

                foreach (var p in properties)
                {
                    var value = p.GetValue(joint);
                    var name = Joining.OptionName(p.Name);
                    if (p.PropertyType == typeof(double))
                    {
                        var unit = p.GetCustomAttribute<JointParameterAttribute>()?.Unit;
                        var option = new OptionDouble(unit == "radians" ? RhinoMath.ToDegrees((double)value) : (double)value);
                        doubles[go.AddOptionDouble(unit == "radians" ? name + "Degrees" : name, ref option)] = (p, option);
                    }
                    else if (p.PropertyType == typeof(int))
                    {
                        var option = new OptionInteger((int)value);
                        ints[go.AddOptionInteger(name, ref option)] = (p, option);
                    }
                    else if (p.PropertyType == typeof(bool))
                    {
                        var option = new OptionToggle((bool)value, "No", "Yes");
                        toggles[go.AddOptionToggle(name, ref option)] = (p, option);
                    }
                }

                var res = go.Get();
                if (res == GetResult.Cancel) return false;
                if (res != GetResult.Option) return true;

                // Read back every option, whichever changed
                foreach (var (p, option) in doubles.Values)
                {
                    var unit = p.GetCustomAttribute<JointParameterAttribute>()?.Unit;
                    p.SetValue(joint, unit == "radians" ? RhinoMath.ToRadians(option.CurrentValue) : option.CurrentValue);
                }
                foreach (var (p, option) in ints.Values) p.SetValue(joint, option.CurrentValue);
                foreach (var (p, option) in toggles.Values) p.SetValue(joint, option.CurrentValue);
            }
        }
    }
}
