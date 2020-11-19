using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;

using System.Threading.Tasks;

namespace GluLamb
{
    public class Element
    {
        public string Name;
        public List<Connection> Connections;
        protected Plane m_plane;
        public GeometryBase Geometry;

        public Element(string name = "")
        {
            Connections = new List<Connection>();
            Name = name;
            m_plane = Plane.WorldXY;
            Geometry = null;
        }

        public Element(Plane handle, string name = "")
        {
            Connections = new List<Connection>();
            Name = name;
            m_plane = handle;
        }

        public Element Duplicate()
        {
            // TODO
            return this;
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

        public virtual GeometryBase Discretize(double length)
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
    }

    public class BeamElement : Element
    {
        public BeamBase Beam;

        public BeamElement(BeamBase beam)
        {
            Beam = beam;
            m_plane = Beam.GetPlane(Beam.Centreline.Domain.Mid);
        }

        public override GeometryBase Discretize(double length)
        {
            var t = Beam.Centreline.DivideByLength(length, false).ToList();

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
    }

}
