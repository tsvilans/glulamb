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
        /// another's face ends half that beam's size from the other's centreline).
        /// </summary>
        public static JointX Condition(List<Beam> beams, double endTolerance = 0)
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

            var merged = JointX.MergeJoints(conditions, endTolerance * 2);
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
        /// A short D2P type id for a joint type: "J" and the initials of the id's words, padded
        /// with letters of the last word to four characters, made unique among the registered
        /// types (in id order) with a digit.
        /// </summary>
        public static string ComponentTypeId(string typeId)
        {
            string Code(string id)
            {
                var words = (id.StartsWith("glulamb.") ? id.Substring(8) : id).Split('-', '.', '_').Where(x => x.Length > 0).ToList();
                var code = "J" + string.Concat(words.Select(w => char.ToUpperInvariant(w[0])));
                var last = words.LastOrDefault() ?? "X";
                for (int i = 1; code.Length < 4 && i < last.Length; ++i)
                    code += char.ToUpperInvariant(last[i]);
                return code.Length > 4 ? code.Substring(0, 4) : code.PadRight(4, 'X');
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
