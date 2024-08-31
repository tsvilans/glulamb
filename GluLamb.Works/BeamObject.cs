using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input.Custom;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb
{
    public class BeamObject : Rhino.DocObjects.Custom.CustomCurveObject
    {
        internal Beam m_beam;
        private Brep m_brep;

        public override GeometryBase Geometry => m_beam == null || m_beam.Centreline == null ? null : m_beam.Centreline;

        public BeamObject()
        {
            m_beam = new Beam() { Centreline = this.CurveGeometry, Width = 100, Height = 100, Orientation = new VectorOrientation(Vector3d.ZAxis) };
            if (this.CurveGeometry != null)
            {
                m_brep = m_beam.ToBrep();
            }
        }

        public BeamObject(Curve crv)
        {
            m_beam = new Beam() { Centreline = crv, Width = 100, Height = 100, Orientation = new VectorOrientation(Vector3d.ZAxis) };
            if (crv != null)
            {
                m_brep = m_beam.ToBrep();
            }
        }

        protected override void OnDuplicate(RhinoObject source)
        {
            if (source is BeamObject bobj)
            {
                if (bobj.m_beam != null)
                {
                    m_beam = new Beam()
                    {
                        Centreline = bobj.m_beam.Centreline.DuplicateCurve(),
                        Width = bobj.m_beam.Width,
                        Height = bobj.m_beam.Height,
                        OffsetX = bobj.m_beam.OffsetX,
                        OffsetY = bobj.m_beam.OffsetY,
                        Samples = bobj.m_beam.Samples,
                        Orientation = bobj.m_beam.Orientation.Duplicate()
                    };

                    if (m_beam.Centreline != null)
                        m_brep = m_beam.ToBrep();
                }
            }

            base.OnDuplicate(source);
        }
        /*
        protected override BoundingBox GetBoundingBox(RhinoViewport viewport)
        {
            if (m_beam != null)
            {
                if (m_brep != null)
                {
                    return m_brep.GetBoundingBox(false);
                }
                else if (m_beam.Centreline != null)
                {
                    return m_beam.Centreline.GetBoundingBox(false);
                }
            }
            return base.GetBoundingBox(viewport);
        }
        */

        protected override void OnTransform(Transform transform)
        {
            if (m_beam != null)
            {
                m_beam.Transform(transform);
                if (transform.IsRigid(RhinoDoc.ActiveDoc.ModelAbsoluteTolerance) == TransformRigidType.Rigid)
                {
                    m_brep.Transform(transform);
                }
                else
                {
                    m_brep = m_beam.ToBrep();
                }
            }
        }

        protected override void OnDraw(DrawEventArgs e)
        {
            var selected = IsSelected(false);
            var outlineColor = selected > 0 ? System.Drawing.Color.Cyan : System.Drawing.Color.Teal;

            if (m_beam != null && m_brep != null)
            {
                e.Display.DrawBrepWires(m_brep, outlineColor);
                e.Display.DrawCurve(m_beam.Centreline, System.Drawing.Color.Red);
            }
            //base.OnDraw(e);
        }

        protected override IEnumerable<ObjRef> OnPick(PickContext context)
        {
            return base.OnPick(context);
        }

        public override int CreateMeshes(MeshType meshType, MeshingParameters parameters, bool ignoreCustomParameters)
        {
            return base.CreateMeshes(meshType, parameters, ignoreCustomParameters);
        }

        public override Mesh[] GetMeshes(MeshType meshType)
        {
            if (m_brep != null)
            {
                return Mesh.CreateFromBrep(m_brep, MeshingParameters.Default);
            }
            return base.GetMeshes(meshType);
        }
    }

}
