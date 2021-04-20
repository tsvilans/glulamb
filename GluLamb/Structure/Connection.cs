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
            eleA.Connections.Add(connection);
            eleB.Connections.Add(connection);

            return connection;
        }

        public string Name;
        public Element ElementA, ElementB;
        public double ParameterA, ParameterB;
        public Plane Plane; // Optional plane for precise positioning

        public Connection(Element eleA, Element eleB, double positionA, double positionB, string name = "")
        {
            ElementA = eleA;
            ElementB = eleB;
            ParameterA = positionA;
            ParameterB = positionB;
            Name = name;
            Plane = Plane.Unset;
        }

        public static void Disconnect(Connection conn)
        {
            conn.ElementA.Connections.Remove(conn);
            conn.ElementB.Connections.Remove(conn);
        }

        public Line Discretize()
        {
            return new Line(
              ElementA.GetConnectionPoint(ParameterA),
              ElementB.GetConnectionPoint(ParameterB));
        }
    }
}
