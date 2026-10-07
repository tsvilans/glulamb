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
