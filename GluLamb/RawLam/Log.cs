#if RAWLAM

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;

namespace RawLam
{


    public class Log
    {
        public Guid Id;
        public string Name;
        public Mesh Mesh;
        public Plane Plane;

        public List<Board> Boards;

        public Log()
        {
            Id = Guid.NewGuid();
            Boards = new List<Board>();
            Plane = Plane.WorldXY;
        }
        
        public void Transform(Transform xform)
        {
            Mesh.Transform(xform);
            Plane.Transform(xform);
            for (int i = 0; i < Boards.Count; ++i)
            {
                Boards[i].Transform(xform);
            }
        }

        public Log Duplicate()
        {
            var log = new Log() { Name = Name, Plane = Plane, Mesh = Mesh.DuplicateMesh() };
            for (int i = 0; i < Boards.Count; ++i)
            {
                var brd = Boards[i].Duplicate();
                brd.Log = log;
                log.Boards.Add(brd);
            }

            return log;
        }
    }
}
#endif
