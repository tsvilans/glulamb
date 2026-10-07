using D2P_Core;
using D2P_Core.Interfaces;
using D2P_Core.Utility;
using GluLamb;
using GluLamb.Joints;
using Rhino;
using Rhino.Geometry;

namespace G2PComponents
{
    /// <summary>
    /// Joining D2P components with GluLamb joints: components become beams, the beams' joint
    /// condition is found, and a joint type from the registry builds the joint. The steps are
    /// separate from the Rhino command so they can run headless.
    /// </summary>
    public static class Joining
    {
        /// <summary>
        /// The beam for each component, with the component's short name as its id.
        /// </summary>
        public static List<Beam> Beams(IEnumerable<IComponent> components)
        {
            var beams = new List<Beam>();
            foreach (var component in components)
            {
                var beam = component is Component c ? Utility.ComponentToBeam(c) : null;
                if (beam == null) throw new ArgumentException($"Component {component.ShortName} has no Geometry to make a beam from.");
                beam.Id = component.ShortName;
                beams.Add(beam);
            }
            return beams;
        }

        /// <summary>
        /// The joint condition between the beams: where each pair comes closest, merged into one
        /// condition. A part counts as at the end of its beam if the joint is within endTolerance
        /// of the end (0: the largest section size of the beams, since a beam drawn to stop at
        /// another's face ends half that beam's size from the other's centreline). The pairs' meeting
        /// points are merged within mergeDistance (0: twice the end tolerance). A smaller end
        /// tolerance turns beams that only just run past each other into a crossing.
        /// </summary>
        public static JointX Condition(List<Beam> beams, double endTolerance = 0, double mergeDistance = 0)
        {
            if (beams.Count < 1) return null;
            if (endTolerance <= 0)
                endTolerance = beams.Max(b => Math.Max(b.Width, b.Height));

            if (beams.Count == 1)
                return null;

            var conditions = new List<JointX>();
            for (int i = 0; i < beams.Count; ++i)
                for (int k = i + 1; k < beams.Count; ++k)
                    conditions.Add(JointUtil.ForceConnect(beams[i].Centreline, i, beams[k].Centreline, k, -1, endTolerance));

            var merged = JointX.MergeJoints(conditions, mergeDistance > 0 ? mergeDistance : endTolerance * 2);
            if (merged.Count != 1) return null;

            var jc = merged[0];

            // Beams that end together: the one most upright first (a post), as Classify Joints does
            if (jc.Parts.Count >= 3 && jc.Parts.All(p => JointPartX.IsAtEnd(p.Case)))
            {
                var post = jc.Parts.OrderByDescending(p => Math.Abs(p.Direction * Vector3d.ZAxis) / Math.Max(p.Direction.Length, 1e-9)).First();
                jc.Parts.Remove(post);
                jc.Parts.Insert(0, post);
            }

            jc.Position = JointX.ConditionPlane(jc.Parts, jc.Position.Origin);
            return jc;
        }

        /// <summary>
        /// Adds the joint to the document as a D2P joint component: named after the components it
        /// joins (A-01+C-53, with #2, #3... for further joints between the same components), of a
        /// type per joint type, with its plane on the joint's position. Its label carries the
        /// joint type id and parameters as user strings. Each beam's cutters go on the
        /// CuttingGeometry layer, drill holes on Drillings and parts like dowels and plates on
        /// Hardware, each object tagged with the component it belongs to ("part"). Returns the
        /// joint component's name.
        /// </summary>
        public static string Store(RhinoDoc doc, List<IComponent> components, List<Beam> beams, IJoint joint, JointResult result)
        {
            var settings = Context.settings;
            var info = JointRegistry.Default.Get(joint.TypeId);
            var typeId = ComponentTypeId(joint.TypeId);
            var colour = System.Drawing.Color.DarkOrange;
            var componentType = new ComponentType(typeId, info?.Name ?? joint.TypeId, settings, 2.0, colour);

            // Name: the joined components in the condition's order, numbered if they are already joined
            var baseName = string.Join(settings.JointDelimiter.ToString(), joint.Parts.Select(p => beams[p.ElementIndex].Id));
            var existing = Instantiation.InstancesByName($"*{settings.TypeDelimiter}{baseName}", settings, doc).Count
                + Instantiation.InstancesByName($"*{settings.TypeDelimiter}{baseName}{settings.CountDelimiter}*", settings, doc).Count;
            var name = existing == 0 ? baseName : $"{baseName}{settings.CountDelimiter}{existing + 1}";

            var plane = joint.Position.IsValid ? joint.Position : Plane.WorldXY;
            var component = new Component(componentType, name, plane);

            var label = component.AttributeCollection[component.ID];
            label.SetUserString("JointType", joint.TypeId);
            label.SetUserString("Parts", string.Join(",", joint.Parts.Select(p => beams[p.ElementIndex].Id)));
            foreach (var kv in JointParameters.Get(joint))
                label.SetUserString(kv.Key, Convert.ToString(kv.Value, System.Globalization.CultureInfo.InvariantCulture));

            Rhino.DocObjects.ObjectAttributes Tagged(string part, string feature)
            {
                var attr = doc.CreateDefaultAttributes();
                attr.SetUserString("part", part);
                if (feature != null) attr.SetUserString("feature", feature);
                return attr;
            }

            var tolerance = doc.ModelAbsoluteTolerance;
            foreach (var beam in beams)
            {
                if (!result.Features.TryGetValue(beam.Id, out var features)) continue;
                var extended = Extended(beam, result);

                foreach (var feature in features)
                {
                    var layer = feature is GluLamb.Features.Drilling ? "Drillings" : "CuttingGeometry";
                    var cutters = feature.GetCutters(extended, tolerance).Where(x => x != null).Cast<GeometryBase>().ToList();
                    if (cutters.Count == 0) continue;
                    component.AddMember(new ComponentMember(new LayerInfo(layer, colour), cutters, Tagged(beam.Id, feature.ProcessingName)));
                }
            }

            foreach (var item in result.Hardware)
            {
                var geometry = item.GetGeometry();
                if (geometry == null) continue;
                var attr = Tagged(string.Join(",", item.BeamIds), null);
                attr.SetUserString("category", item.Category);
                attr.SetUserString("specification", item.Specification);
                component.AddMember(new ComponentMember(new LayerInfo("Hardware", colour), new[] { geometry }, attr));
            }

            RHDoc.AddToRhinoDoc(component, doc, true);
            return name;
        }

        /// <summary>
        /// The component's Geometry cut by every joint it is part of: the cutters on each joint
        /// component's CuttingGeometry layer tagged with this component (and its Drillings, if
        /// asked). Open cutters split the geometry and the largest piece is kept; closed ones are
        /// subtracted. Null if the component has no Geometry or no cutters.
        /// </summary>
        public static Brep CutJoints(IComponent component, RhinoDoc doc, bool drillings, List<string> messages)
        {
            var geometry = Utility.GetMember(component, "Geometry", doc).FirstOrDefault();
            var brep = geometry is Extrusion extrusion ? extrusion.ToBrep(true) : geometry as Brep;
            if (brep == null)
            {
                messages.Add($"{component.ShortName}: no Geometry to cut.");
                return null;
            }

            var cutters = new List<Brep>();
            var joints = FindJoints(component, doc);
            foreach (var joint in joints)
            {
                var layers = drillings ? new[] { "CuttingGeometry", "Drillings" } : new[] { "CuttingGeometry" };
                foreach (var layer in layers)
                    foreach (var id in Utility.GetMemberIDs(joint, layer, doc))
                    {
                        var obj = doc.Objects.FindId(id);
                        if (obj?.Attributes.GetUserString("part") != component.ShortName) continue;
                        if (obj.Geometry is Brep cutter) cutters.Add(cutter.DuplicateBrep());
                        else if (obj.Geometry is Extrusion ex) cutters.Add(ex.ToBrep(true));
                    }
            }

            if (cutters.Count == 0)
            {
                messages.Add($"{component.ShortName}: no joint cutters ({joints.Count} joints).");
                return null;
            }

            var cut = brep.DuplicateBrep().Cut(cutters, doc.ModelAbsoluteTolerance);
            if (cut == null || !cut.IsValid)
            {
                messages.Add($"{component.ShortName}: cutting failed.");
                return null;
            }
            messages.Add($"{component.ShortName}: cut by {cutters.Count} cutters from {joints.Count} joints.");
            return cut;
        }

        /// <summary>
        /// The joint components a component is part of. Like Instantiation.GetJoints, but also
        /// finds numbered joints (A-01+C-53#2) where the component is last, which GetJoints misses.
        /// </summary>
        public static List<IComponent> FindJoints(IComponent component, RhinoDoc doc)
        {
            var s = Context.settings;
            var labels = doc.Objects.GetObjectList(new Rhino.DocObjects.ObjectEnumeratorSettings
            {
                HiddenObjects = true,
                LockedObjects = true,
                NameFilter = $"*{component.ShortName}*",
                ObjectTypeFilter = Rhino.DocObjects.ObjectType.Annotation,
            }).Where(o =>
            {
                var name = o.Name ?? "";
                var typeEnd = name.IndexOf(s.TypeDelimiter);
                if (typeEnd >= 0) name = name.Substring(typeEnd + 1);
                if (!name.Contains(s.JointDelimiter)) return false;
                var count = name.IndexOf(s.CountDelimiter);
                if (count >= 0) name = name.Substring(0, count);
                return name.Split(s.JointDelimiter).Contains(component.ShortName);
            });
            return Instantiation.InstancesFromObjects(labels, s, doc);
        }

        private static Beam Extended(Beam beam, JointResult result)
        {
            if (!result.Extensions.TryGetValue(beam.Id, out var e)) return beam;
            var x = beam.Duplicate();
            var c = x.Centreline;
            if (e.Start > 0) c = c.Extend(CurveEnd.Start, e.Start, CurveExtensionStyle.Line);
            if (e.End > 0) c = c.Extend(CurveEnd.End, e.End, CurveExtensionStyle.Line);
            x.Centreline = c;
            return x;
        }

        /// <summary>
        /// A joint type id as a Rhino command option name (letters and digits only): the id
        /// without "glulamb.", in CamelCase, e.g. "glulamb.t-tenon" becomes "TTenon".
        /// </summary>
        public static string OptionName(string typeId)
        {
            var id = typeId.StartsWith("glulamb.") ? typeId.Substring(8) : typeId;
            return string.Concat(id.Split('-', '.', '_', ' ').Where(x => x.Length > 0)
                .Select(x => char.ToUpperInvariant(x[0]) + x.Substring(1)));
        }

        /// <summary>
        /// The joint condition letters used in the code (JointX.ClassifyJoint), by the first word
        /// of a joint type id: X crossing, L corner, T, S splice, V fork (acute), E end, plus K for
        /// K joints, P for post joints and N for other nodes. Types without a family word (e.g.
        /// glulamb.drilling) are F, a feature along a beam.
        /// </summary>
        private static readonly Dictionary<string, char> Families = new()
        {
            ["cross"] = 'X', ["corner"] = 'L', ["t"] = 'T', ["splice"] = 'S', ["branch"] = 'V',
            ["end"] = 'E', ["k"] = 'K', ["post"] = 'P', ["four"] = 'N',
        };

        /// <summary>
        /// A short D2P type id for a joint type: J, the condition letter, and two letters for the
        /// variant: the first two letters of a one-word variant, or the initials of the last two
        /// words. E.g. glulamb.cross-tapered is JXTA, glulamb.corner-tenon JLTE,
        /// glulamb.corner-lap-mitre JLLM, glulamb.k-plate-joist JKPJ. Made unique among the
        /// registered types (in id order) with a digit.
        /// </summary>
        public static string ComponentTypeId(string typeId)
        {
            string Code(string id)
            {
                var words = (id.StartsWith("glulamb.") ? id.Substring(8) : id).Split('-', '.', '_').Where(x => x.Length > 0).ToList();
                if (words.Count == 0) return "JFXX";

                char family = 'F';
                if (words.Count > 1 && words[0] == "four" && words[1] == "way") words.RemoveAt(1);
                if (Families.TryGetValue(words[0], out var f) && words.Count > 1)
                {
                    family = f;
                    words.RemoveAt(0);
                }

                var variant = words.Count == 1
                    ? words[0].PadRight(2, 'x').Substring(0, 2)
                    : string.Concat(words.Skip(words.Count - 2).Select(w => w[0]));
                return ("J" + family + variant).ToUpperInvariant();
            }

            var taken = new Dictionary<string, string>();
            foreach (var t in JointRegistry.Default.Types.OrderBy(x => x.Id))
            {
                var code = Code(t.Id);
                for (int n = 2; taken.ContainsKey(code) && taken[code] != t.Id; ++n)
                    code = code.Substring(0, 3) + n;
                taken[code] = t.Id;
                if (t.Id == typeId) return code;
            }
            return Code(typeId);
        }
    }
}
