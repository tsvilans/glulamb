using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb
{
    public class Structure
    {
        public List<Connection> Connections;
        public List<Element> Elements;

        public Structure()
        {
            Connections = new List<Connection>();
            Elements = new List<Element>();
        }

        public List<object> Discretize(double length)
        {
            var segments = new List<object>();

            foreach (var ele in Elements)
                segments.Add(ele.Discretize(length));
            foreach (var conn in Connections)
                segments.Add(conn.Discretize());

            return segments;
        }
    }
}
