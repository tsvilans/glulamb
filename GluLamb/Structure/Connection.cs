using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry;

namespace GluLamb
{
    public class Connection
    {
        public static Connection Connect(Element eleA, Element eleB, double posA, double posB, string name = "")
        {
            var connection = new Connection(eleA, eleB, posA, posB, name);
            if (eleA != null)
                eleA.Connections.Add(connection);
            if (eleB != null)
                eleB.Connections.Add(connection);

            return connection;
        }

        public string Name;
        public Element ElementA, ElementB;
        public double ParameterA, ParameterB;
        public Plane Plane; // Optional plane for precise positioning
        public List<Brep> Geometry; // Optional geometry to associate with this connection
        public List<string> GeometryTags;
        public List<object> Objects; // Optional list of associated objects -> i.e. Feature objects from tasMachine

        public Connection(Element eleA, Element eleB, double positionA, double positionB, string name = "")
        {
            ElementA = eleA;
            ElementB = eleB;
            ParameterA = positionA;
            ParameterB = positionB;
            Name = name;
            Plane = Plane.Unset;
            Geometry = new List<Brep>();
            GeometryTags = new List<string>();
            Objects = new List<object>();
        }

        public static void Disconnect(Connection conn)
        {
            if (conn.ElementA != null)
                conn.ElementA.Connections.Remove(conn);
            if (conn.ElementB != null)
                conn.ElementB.Connections.Remove(conn);
        }

        public Line Discretize(bool adaptive=true)
        {
            if (ElementA != null && ElementB != null)
                return new Line(
                  ElementA.GetConnectionPoint(ParameterA),
                  ElementB.GetConnectionPoint(ParameterB));
            return Line.Unset;
        }
    }
}
