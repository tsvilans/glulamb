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

        public Plane Plane;

        internal Beam m_beam;
        private Brep m_brep;
        private BoundingBox m_bounds = BoundingBox.Unset;

        public Brep Brep
        {
            get { return m_brep; }
            set
            {
                m_brep = value;
                CalculateBoundingBox();
            }
        }

        public BoundingBox BoundingBox
        {
            get
            {
                if (!m_bounds.IsValid)
                {
                    CalculateBoundingBox();
                }
                return m_bounds;
            }

            private set
            {
                m_bounds = value;
            }
        }

        public override GeometryBase Geometry => m_beam == null || m_beam.Centreline == null ? null : m_beam.Centreline;

        public BeamObject()
        {
            //Plane = new Plane(CurveGeometry.PointAtStart, CurveGeometry.TangentAtStart, Vector3d.CrossProduct(Vector3d.ZAxis, CurveGeometry.TangentAtStart));

            //m_beam = new Beam() { Centreline = CurveGeometry, Width = 100, Height = 200, Orientation = new PlanarOrientation(Plane) };
            //if (this.CurveGeometry != null)
            //{
            //    m_brep = m_beam.ToBrep();
            //}
        }

        public BeamObject(Curve curve)
        {
            if (curve != null)
            {
                Plane = new Plane(curve.PointAtStart, curve.TangentAtStart, Vector3d.ZAxis);
                m_beam = new Beam() { Centreline = curve, Width = 100, Height = 200, Orientation = new PlanarOrientation(Plane) };

                m_brep = m_beam.ToBrep();
                CalculateBoundingBox();
            }
        }

        /// <summary>
        /// Returns plane located at the lower extremities of the beam geometry.
        /// Could probably be called something better.
        /// </summary>
        /// <returns>Plane at the bottom left corner of the beam geometry.</returns>
        public Plane GetHandle()
        {
            var origin = Plane.PointAt(m_bounds.Min.X, m_bounds.Min.Y, m_bounds.Min.Z);

            return new Plane(
                origin,
                Plane.XAxis,
                Plane.YAxis);
        }

        private void CalculateBoundingBox()
        {
            if (m_brep != null)
            {
                m_bounds = m_brep.GetBoundingBox(Plane);
            }
        }

        protected override void OnDuplicate(RhinoObject source)
        {
            if (source is BeamObject beam)
            {
                if (beam.m_beam != null)
                {
                    m_beam = new Beam()
                    {
                        Centreline = beam.m_beam.Centreline.DuplicateCurve(),
                        Width = beam.m_beam.Width,
                        Height = beam.m_beam.Height,
                        OffsetX = beam.m_beam.OffsetX,
                        OffsetY = beam.m_beam.OffsetY,
                        Samples = beam.m_beam.Samples,
                        Orientation = beam.m_beam.Orientation.Duplicate()
                    };

                    if (m_beam.Centreline != null)
                        Brep = m_beam.ToBrep();
                }

                Plane = beam.Plane;
            }

            base.OnDuplicate(source);
        }
        
        //protected override BoundingBox GetBoundingBox(RhinoViewport viewport)
        //{
        //    return BoundingBox;

        //    if (m_beam != null)
        //    {
        //        if (m_brep != null)
        //        {
        //            return m_brep.GetBoundingBox(false);
        //        }
        //        else if (m_beam.Centreline != null)
        //        {
        //            return m_beam.Centreline.GetBoundingBox(false);
        //        }
        //    }
        //    return base.GetBoundingBox(viewport);
        //}
        

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
                    Brep = m_beam.ToBrep();
                }
            }

            Plane.Transform(transform);
        }

        protected override void OnDraw(DrawEventArgs e)
        {
            var selected = IsSelected(false);
            var outlineColor = selected > 0 ? System.Drawing.Color.Cyan : System.Drawing.Color.Teal;

            if (m_beam != null && m_brep != null)
            {
                e.Display.DrawBrepWires(m_brep, outlineColor);
                e.Display.DrawCurve(m_beam.Centreline, System.Drawing.Color.Red);

                if (GWorks.Globals.ShowBeamAxes)
                {
                    e.Display.DrawLine(new Line(Plane.Origin, Plane.XAxis, GWorks.Globals.BeamAxisLength), System.Drawing.Color.Red);
                    e.Display.DrawLine(new Line(Plane.Origin, Plane.YAxis, GWorks.Globals.BeamAxisLength), System.Drawing.Color.Lime);
                }
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
