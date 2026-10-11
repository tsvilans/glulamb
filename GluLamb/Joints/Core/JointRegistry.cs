using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace GluLamb.Joints
{
    /// <summary>
    /// Describes a registered joint type: its id, metadata, parameters and how to create
    /// and score it.
    /// </summary>
    public class JointTypeInfo
    {
        public string Id => Attribute.Id;
        public string Name => string.IsNullOrEmpty(Attribute.Name) ? Type.Name : Attribute.Name;
        public Type Type { get; }
        public JointTypeAttribute Attribute { get; }
        public IReadOnlyList<PropertyInfo> Parameters { get; }

        private readonly ConstructorInfo m_constructor;
        private readonly MethodInfo m_score;

        internal JointTypeInfo(Type type, JointTypeAttribute attribute, ConstructorInfo constructor)
        {
            Type = type;
            Attribute = attribute;
            m_constructor = constructor;
            Parameters = JointParameters.GetProperties(type).ToList();

            // Optional: public static double Score(JointX condition, IJointContext context)
            m_score = type.GetMethod("Score", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(JointX), typeof(IJointContext) }, null);
            if (m_score != null && m_score.ReturnType != typeof(double))
                m_score = null;
        }

        public IJoint Create(JointX condition)
        {
            try
            {
                return (IJoint)m_constructor.Invoke(new object[] { condition });
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                throw e.InnerException;
            }
        }

        /// <summary>
        /// How well this joint type fits a condition. 0 means it can't handle it. Arity and
        /// topology are checked first; if the type defines a static Score method, that decides
        /// the rest, otherwise a matching type scores 1.
        /// </summary>
        public double Score(JointX condition, JointTopology topology, IJointContext context)
        {
            if (Attribute.Arity > 0 && condition.Parts.Count != Attribute.Arity) return 0;
            if (Attribute.Topology != JointTopology.Unknown && Attribute.Topology != topology) return 0;
            if (m_score == null) return 1;

            try
            {
                return (double)m_score.Invoke(null, new object[] { condition, context });
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public override string ToString() => $"{Name} ({Id})";
    }

    /// <summary>
    /// Registry of joint types. Types are discovered from assemblies by JointTypeAttribute
    /// and keyed by its Id. Problems (duplicate ids, missing constructors, assemblies that
    /// fail to load) are collected in Errors rather than thrown.
    /// </summary>
    public class JointRegistry
    {
        private static readonly Lazy<JointRegistry> m_default = new Lazy<JointRegistry>(() =>
        {
            var registry = new JointRegistry();
            registry.RegisterAssembly(typeof(JointRegistry).Assembly);
            return registry;
        });

        /// <summary>
        /// Shared registry, pre-loaded with the joints in GluLamb.
        /// </summary>
        public static JointRegistry Default => m_default.Value;

        /// <summary>
        /// Angle threshold used to classify two-ended joints into splice, corner and acute.
        /// </summary>
        public double PerpendicularThreshold = JointX.PerpendicularThreshold;

        private readonly Dictionary<string, JointTypeInfo> m_types = new Dictionary<string, JointTypeInfo>();

        public IReadOnlyCollection<JointTypeInfo> Types => m_types.Values;
        public List<string> Errors { get; } = new List<string>();

        public JointTypeInfo Get(string id) => id != null && m_types.TryGetValue(id, out var info) ? info : null;

        /// <summary>
        /// Register a joint type. Returns false (and records why in Errors) if the type
        /// isn't a usable joint or its id is already taken.
        /// </summary>
        public bool Register(Type type)
        {
            var attribute = type.GetCustomAttribute<JointTypeAttribute>(false);
            if (attribute == null) return false;

            if (type.IsAbstract || !typeof(IJoint).IsAssignableFrom(type))
            {
                Errors.Add($"{type.FullName}: has [JointType] but is abstract or does not implement IJoint.");
                return false;
            }

            if (string.IsNullOrEmpty(attribute.Id))
            {
                Errors.Add($"{type.FullName}: [JointType] has no id.");
                return false;
            }

            var ctor = type.GetConstructor(new[] { typeof(JointX) });
            if (ctor == null)
            {
                Errors.Add($"{type.FullName}: missing public constructor ({nameof(JointX)} condition).");
                return false;
            }

            if (m_types.TryGetValue(attribute.Id, out var existing))
            {
                if (existing.Type != type)
                    Errors.Add($"Duplicate joint id '{attribute.Id}': {type.FullName} ignored, " +
                        $"{existing.Type.FullName} already registered.");
                return false;
            }

            m_types[attribute.Id] = new JointTypeInfo(type, attribute, ctor);
            return true;
        }

        /// <summary>
        /// Register all joint types in an assembly. Returns the number registered.
        /// </summary>
        public int RegisterAssembly(Assembly assembly)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                Errors.Add($"{assembly.GetName().Name}: some types could not be loaded " +
                    $"({e.LoaderExceptions.FirstOrDefault()?.Message}).");
                types = e.Types.Where(t => t != null).ToArray();
            }

            return types.Count(Register);
        }

        /// <summary>
        /// Load joint assemblies from a folder and register their joint types. Assemblies are
        /// loaded into the same load context as GluLamb, so their joints share its types.
        /// Returns the number of joint types registered.
        /// </summary>
        public int LoadFromFolder(string folder, string searchPattern = "*.Joints.dll")
        {
            if (!Directory.Exists(folder)) return 0;

            var context = AssemblyLoadContext.GetLoadContext(typeof(IJoint).Assembly) ?? AssemblyLoadContext.Default;
            int count = 0;

            foreach (var path in Directory.GetFiles(folder, searchPattern))
            {
                try
                {
                    var name = AssemblyName.GetAssemblyName(path);
                    var assembly = context.Assemblies.FirstOrDefault(a => AssemblyName.ReferenceMatchesDefinition(a.GetName(), name))
                        ?? context.LoadFromAssemblyPath(Path.GetFullPath(path));
                    count += RegisterAssembly(assembly);
                }
                catch (Exception e) when (e is BadImageFormatException || e is FileLoadException || e is FileNotFoundException)
                {
                    Errors.Add($"{Path.GetFileName(path)}: {e.Message}");
                }
            }

            return count;
        }

        public IJoint Create(string id, JointX condition)
        {
            var info = Get(id) ?? throw new KeyNotFoundException($"Joint type '{id}' is not registered.");
            return info.Create(condition);
        }

        /// <summary>
        /// Joint types that can handle a condition, best first.
        /// </summary>
        public List<(JointTypeInfo Info, double Score)> Candidates(JointX condition, IJointContext context)
        {
            var topology = Classify(condition, PerpendicularThreshold);
            return m_types.Values
                .Select(info => (Info: info, Score: info.Score(condition, topology, context)))
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Info.Id, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Create the best-scoring joint for a condition, or null if no type handles it.
        /// </summary>
        public IJoint Allocate(JointX condition, IJointContext context)
        {
            var best = Candidates(condition, context).FirstOrDefault();
            return best.Info?.Create(condition);
        }

        public static JointTopology Classify(JointX condition, double perpendicularThreshold)
        {
            switch (JointX.ClassifyJoint(condition, perpendicularThreshold))
            {
                case "E": return JointTopology.End;
                case "F": return JointTopology.Feature;
                case "S": return JointTopology.Splice;
                case "L": return JointTopology.Corner;
                case "V": return JointTopology.Acute;
                case "T": return JointTopology.T;
                case "X": return JointTopology.Cross;
                case "null": return JointTopology.Unknown;
                default: return condition.Parts.Count > 2 ? JointTopology.Node : JointTopology.Unknown;
            }
        }
    }
}
