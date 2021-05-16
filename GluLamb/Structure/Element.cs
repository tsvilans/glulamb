using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;
using Rhino.Collections;

using System.Threading.Tasks;

namespace GluLamb
{
    public class Element
    {
        public string Name;
        public List<Connection> Connections;
        public List<Plane> Handles;

        protected Plane m_plane;
        public GeometryBase m_geometry;
        public ArchivableDictionary UserDictionary;

        public Element(string name = "")
        {
            Connections = new List<Connection>();
            Handles = new List<Plane>();
            Name = name;
            m_plane = Plane.WorldXY;
            m_geometry = null;
            UserDictionary = new ArchivableDictionary();
        }

        public Element(Plane handle, string name = "")
        {
            Connections = new List<Connection>();
            Name = name;
            m_plane = handle;
            UserDictionary = new ArchivableDictionary();

        }

        public Element Duplicate()
        {
            // TODO
            return this;
        }

        public virtual GeometryBase Geometry
        {
            get
            {
                return m_geometry;
            }
            set
            {
                m_geometry = value;
            }
        }

        public Plane Handle
        {
            get
            {
                return m_plane;
            }
            set
            {
                m_plane = value;
            }
        }

        public virtual GeometryBase Discretize(double length, bool adaptive = true)
        {
            return null;
        }

        public virtual Point3d GetConnectionPoint(double t)
        {
            return m_plane.Origin;
        }

        public Element GetConnected(int index)
        {
            if (Connections.Count < 1)
                return null;

            if (index < -1 || index > (Connections.Count - 1))
            {
                throw new Exception("GetConnected index out of range.");
            }

            if (index == -1)
                index = Connections.Count - 1;

            var conn = Connections[index];
            if (conn.ElementA == this)
                return conn.ElementB;
            return conn.ElementA;
        }

        public virtual void Transform(Rhino.Geometry.Transform xform)
        {
            Geometry.Transform(xform);
            m_plane.Transform(xform);

            for (int i = 0; i < Handles.Count; ++i)
            {
                Handles[i].Transform(xform);
            }
        }
    }

    public class BeamElement : Element
    {
        public Beam Beam;

        public BeamElement(Beam beam, string name="BeamElement") : base(name)
        {
            Beam = beam;
            m_plane = Beam.GetPlane(Beam.Centreline.Domain.Mid);
        }

        public BeamElement(Beam beam, Plane handle, string name = "BeamElement") : base(handle, name)
        {
            Beam = beam;
        }

        public override GeometryBase Geometry
        {
            get
            {
                if (Beam is Glulam && m_geometry == null)
                    return (Beam as Glulam).ToBrep();
                return m_geometry;
            }
            set
            {
                m_geometry = value;
            }
        }

        public override GeometryBase Discretize(double length, bool adaptive=true)
        {
            List<double> t = new List<double>();
            if (!Beam.Centreline.IsLinear())
            {
                t.AddRange(Beam.Centreline.DivideByLength(length, false));
            }

            t.Insert(0, Beam.Centreline.Domain.Min);
            t.Add(Beam.Centreline.Domain.Max);

            foreach (var conn in Connections)
            {
                if (conn.ElementA == this)
                    t.Add(conn.ParameterA);
                else if (conn.ElementB == this)
                    t.Add(conn.ParameterB);
            }

                t.Sort();
            return new PolylineCurve(t.Select(x => Beam.Centreline.PointAt(x)));
            
        }

        public override Point3d GetConnectionPoint(double t)
        {
            return Beam.Centreline.PointAt(t);
        }
        public override void Transform(Rhino.Geometry.Transform xform)
        {
            base.Transform(xform);
            Beam.Transform(xform);
        }
    }

}
