using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb
{
    public class Metadata
    {
        string Name = "";
        string Description = "";

        public Metadata()
        {
        }
    }

    public class LeanGraph
    {
        public Metadata Metadata
        { get { return mMetadata; } }

        Metadata mMetadata;

        public Dictionary<int, object> Nodes
        { get { return mNodes; } }

        Dictionary<int, object> mNodes;

        public Dictionary<int, int[]> Edges
        { get { return mEdges; } }

        Dictionary<int, int[]> mEdges;

        public LeanGraph(Metadata md = null)
        {
            mNodes = new Dictionary<int, object>();
            mEdges = new Dictionary<int, int[]>();

            mMetadata = md;
        }
    }
}
