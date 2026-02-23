using GluLamb.Factory;
using Rhino;
using System.Reflection;
using System.Xml.Linq;

namespace GluLamb.Joints
{
    public class JointEntry
    {
        public Type Type;
        public ConstructorInfo Constructor;
    }

    public class JointFactory
    {
        private readonly Dictionary<string, JointEntry> _jointTypes = new();

        public IReadOnlyCollection<string> Available => _jointTypes.Keys;

        public void LoadFromFolder(string folder)
        {
            if (!Directory.Exists(folder))
                return;

            foreach (var dll in Directory.GetFiles(folder, "*.dll"))
            {
                //RhinoApp.WriteLine($"-- Searching {dll}");
                try
                {
                    var assembly = Assembly.LoadFrom(dll);
                    RegisterFromAssembly(assembly);
                }
                catch(BadImageFormatException)
                {
                    //RhinoApp.WriteLine($"-- Could not load unmanaged assembly {dll}");
                }
                catch (FileLoadException)
                {
                    //RhinoApp.WriteLine($"-- Could not load assembly {dll}");
                }
            }
        }

        public void RegisterFromAssembly(Assembly assembly)
        {
            var types = assembly.GetTypes()
                .Where(t =>
                    !t.IsAbstract &&
                    typeof(Joint).IsAssignableFrom(t));

            foreach (var type in types)
            {
                if (Available.Contains(type.Name))
                {
                    //RhinoApp.WriteLine($"-- Joint {type.Name} already registered.");
                    continue;
                    //throw new Exception($Joint {type.Name} already registered.");
                }

                var ctor = type.GetConstructor(
                  BindingFlags.Instance | BindingFlags.Public, null,
                  CallingConventions.HasThis, new Type[] { typeof(List<Element>), typeof(Factory.JointCondition) }, null);

                if (ctor == null)
                {
                    RhinoApp.WriteLine($"-- Joint {type.Name} missing required constructor.");
                    continue;
                    //throw new Exception($"Joint {type.Name} missing required constructor");
                }

                _jointTypes[type.Name] = new JointEntry
                {
                    Type = type,
                    Constructor = ctor
                };
            }
        }

        public Joint Create(string typeName, List<Element> elements, JointCondition jointCondition)
        {
            if (!_jointTypes.TryGetValue(typeName, out var entry))
                throw new Exception($"Joint type '{typeName}' not registered");

            return (Joint)entry.Constructor.Invoke(new object[] { elements, jointCondition });
        }
    }
}
