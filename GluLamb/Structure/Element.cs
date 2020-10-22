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
        public Glulam Beam;
        public List<Connection> Connections;
        private Plane m_plane;

        public Element(Glulam g, string name = "")
        {
            Beam = g;
            Connections = new List<Connection>();
            Name = name;
            m_plane = Beam.GetPlane(Beam.Centreline.Domain.Mid);
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

        public Polyline Discretize(double length)
        {
            var t = Beam.Centreline.DivideByLength(length, true).ToList();
            foreach (var conn in Connections)
            {
                if (conn.ElementA == this)
                    t.Add(conn.ParameterA);
                else if (conn.ElementB == this)
                    t.Add(conn.ParameterB);
            }

            t.Sort();

            return new Polyline(t.Select(x => Beam.Centreline.PointAt(x)));
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

}
