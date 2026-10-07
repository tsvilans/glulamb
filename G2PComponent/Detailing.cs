using D2P_Core;
using D2P_Core.Interfaces;
using D2P_Core.Utility;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace G2PComponents
{
    /// <summary>
    /// Detailed geometry of components. A component's Geometry is its blank and the source of
    /// truth; its DetailedGeometry is always made from it, by cutting its joints (from
    /// JoinComponents) and then its connectors (fixings), and never cut further.
    /// </summary>
    public static class Detailing
    {
        public const string Basic = "Geometry";
        public const string Detailed = "DetailedGeometry";

        /// <summary>
        /// Deletes the component's DetailedGeometry. Returns how many objects were deleted.
        /// </summary>
        public static int Revert(IComponent component, RhinoDoc doc)
        {
            var ids = Utility.GetMemberIDs(component, Detailed, doc).ToList();
            foreach (var id in ids)
                doc.Objects.Delete(id, true);
            return ids.Count;
        }

        /// <summary>
        /// Makes the component's DetailedGeometry from its Geometry: its joints cut first (if
        /// joints), then the connectors on the given layers (if connectors). Returns whether
        /// anything was cut; messages say what happened.
        /// </summary>
        public static bool Generate(IComponent component, RhinoDoc doc, bool joints, bool connectors,
            List<Connector> connectorList, List<string> messages)
        {
            Revert(component, doc);
            component = Reload(component, doc);
            bool cut = false;

            if (joints)
            {
                var brep = Joining.CutJoints(component, doc, true, messages);
                if (brep != null)
                {
                    Write(component, brep, doc);
                    component = Reload(component, doc);
                    cut = true;
                }
            }

            if (connectors && connectorList != null && connectorList.Count > 0)
            {
                // Placed on the blank, cut into the joint-cut geometry if there is one
                var (placed, _) = Connector.IntersectConnectorsRay(component, connectorList, 1.0, true, 10, 10, false, doc, false);
                var hits = placed.Select(x => x.Connector).ToList();
                if (hits.Count > 0)
                {
                    var brep = Connector.CutConnectors(component, hits, doc, 1e-2, 1, 0.5);
                    if (brep == null)
                        messages.Add($"{component.ShortName}: cutting {hits.Count} connectors failed.");
                    else
                    {
                        Revert(component, doc);
                        component = Reload(component, doc);
                        Write(component, brep, doc);
                        messages.Add($"{component.ShortName}: cut {hits.Count} connectors.");
                        cut = true;
                    }
                }
            }

            return cut;
        }

        /// <summary>
        /// Shows the detailed or the basic geometry of all component types, by layer. Only the
        /// types that have a DetailedGeometry layer switch; others keep showing their Geometry.
        /// Returns the number of layers changed.
        /// </summary>
        public static int Show(RhinoDoc doc, bool detailed)
        {
            var settings = Context.settings;
            var detailedSuffix = settings.LayerDelimiter + Detailed;
            var basicSuffix = settings.LayerDelimiter + Basic;

            var detailedLayers = doc.Layers.Where(l => !l.IsDeleted && l.Name.EndsWith(detailedSuffix)).ToList();
            var types = detailedLayers.Select(l => l.Name.Substring(0, l.Name.Length - detailedSuffix.Length)).ToHashSet();
            var basicLayers = doc.Layers.Where(l => !l.IsDeleted && l.Name.EndsWith(basicSuffix) && !l.Name.EndsWith(detailedSuffix)
                && types.Contains(l.Name.Substring(0, l.Name.Length - basicSuffix.Length))).ToList();

            int changed = 0;
            void Set(Layer layer, bool visible)
            {
                if (layer.IsVisible == visible) return;
                layer.IsVisible = visible;
                layer.CommitChanges();
                changed++;
            }

            foreach (var layer in detailedLayers) Set(layer, detailed);
            foreach (var layer in basicLayers) Set(layer, !detailed);
            return changed;
        }

        /// <summary>
        /// Whether the detailed geometry is showing: any DetailedGeometry layer visible.
        /// </summary>
        public static bool ShowingDetailed(RhinoDoc doc)
        {
            var suffix = Context.settings.LayerDelimiter + Detailed;
            return doc.Layers.Any(l => !l.IsDeleted && l.Name.EndsWith(suffix) && l.IsVisible);
        }

        /// <summary>
        /// Checks that every instance of the given components' marks (components sharing a short
        /// name, e.g. several A-01s) came out the same: compares their DetailedGeometry by volume,
        /// area, face count and where the cuts took material from (the shift of its centroid from
        /// the blank's, in the component's plane, allowing for the piece being turned over or end
        /// for end). Returns a line per mark that differs or isn't
        /// fully detailed, the number of marks with more than one instance, and the ids of the
        /// instances that differ from the first of their mark or aren't detailed.
        /// </summary>
        public static (List<string> Problems, int Marks, List<Guid> Odd) CheckMarks(IEnumerable<IComponent> components, RhinoDoc doc)
        {
            var problems = new List<string>();
            var odd = new List<Guid>();
            int marks = 0;
            var delimiter = Context.settings.JointDelimiter;
            foreach (var mark in components.GroupBy(x => x.Name).Select(g => g.First()))
            {
                if (mark.ShortName.Contains(delimiter)) continue;
                var instances = Instantiation.InstancesByName(mark, doc).GroupBy(x => x.ID).Select(g => g.First()).ToList();
                if (instances.Count < 2) continue;
                marks++;

                var signatures = instances.Select(x => Signature(x, doc)).ToList();
                var missing = signatures.Count(x => x == null);
                if (missing == instances.Count) continue;   // none detailed: nothing to compare
                if (missing > 0)
                {
                    problems.Add($"{mark.ShortName}: {missing} of {instances.Count} instances have no detailed geometry.");
                    odd.AddRange(instances.Where((x, i) => signatures[i] == null).Select(x => x.ID));
                    continue;
                }

                var first = signatures[0];
                var differing = new List<string>();
                for (int i = 1; i < signatures.Count; ++i)
                {
                    var why = Differs(first, signatures[i]);
                    if (why == null) continue;
                    differing.Add(why);
                    odd.Add(instances[i].ID);
                }
                if (differing.Count > 0)
                    problems.Add($"{mark.ShortName}: {differing.Count} of {instances.Count} instances differ from the others ({string.Join("; ", differing.Distinct())}).");
            }
            return (problems, marks, odd);
        }

        private record Shape(double Volume, double Area, int Faces, Vector3d Shift, double Size);

        private static Shape Signature(IComponent component, RhinoDoc doc)
        {
            Brep AsBrep(GeometryBase g) => g is Extrusion e ? e.ToBrep(true) : g as Brep;
            var detailed = AsBrep(Utility.GetMember(component, Detailed, doc).FirstOrDefault());
            var blank = AsBrep(Utility.GetMember(component, Basic, doc).FirstOrDefault());
            if (detailed == null || blank == null) return null;

            var vd = VolumeMassProperties.Compute(detailed);
            var vb = VolumeMassProperties.Compute(blank);
            var ad = AreaMassProperties.Compute(detailed);
            if (vd == null || vb == null || ad == null) return null;

            var plane = component.Plane;
            var d = vd.Centroid - vb.Centroid;
            var shift = new Vector3d(d * plane.XAxis, d * plane.YAxis, d * plane.ZAxis);
            return new Shape(vd.Volume, ad.Area, detailed.Faces.Count, shift, blank.GetBoundingBox(false).Diagonal.Length);
        }

        private static string Differs(Shape a, Shape b)
        {
            const double relative = 1e-4;
            if (a.Faces != b.Faces) return $"{b.Faces} faces, not {a.Faces}";
            if (Math.Abs(a.Volume - b.Volume) > relative * Math.Max(a.Volume, b.Volume)) return $"volume differs by {Math.Abs(a.Volume - b.Volume) / Math.Max(a.Volume, b.Volume):P2}";
            if (Math.Abs(a.Area - b.Area) > relative * Math.Max(a.Area, b.Area)) return "surface area differs";
            // The same piece turned end for end or upside down (a half turn about one of its
            // plane's axes) is still the same piece; a mirror image isn't
            var turns = new[] { new Vector3d(1, 1, 1), new Vector3d(1, -1, -1), new Vector3d(-1, 1, -1), new Vector3d(-1, -1, 1) };
            var limit = relative * 10 * Math.Max(a.Size, b.Size);
            if (!turns.Any(t => (a.Shift - new Vector3d(b.Shift.X * t.X, b.Shift.Y * t.Y, b.Shift.Z * t.Z)).Length <= limit))
                return "cuts in different places, or mirrored";
            return null;
        }

        private static IComponent Reload(IComponent component, RhinoDoc doc) =>
            Instantiation.InstancesFromObjects(new[] { component.ID }, Context.settings, doc).FirstOrDefault() ?? component;

        private static void Write(IComponent component, Brep brep, RhinoDoc doc)
        {
            var layer = doc.Layers.FindIndex(component.Attributes.First().LayerIndex);
            component.AddMember(new ComponentMember(
                new LayerInfo(Detailed, layer?.Color ?? component.ComponentType.LayerColor),
                new GeometryBase[] { brep },
                component.Attributes.First().Duplicate()));
            RHDoc.AddToRhinoDoc(component, doc, true);
        }
    }
}
