#if RAWLAM

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;

namespace RawLam
{
    public class Board
    {
        public Guid BoardId;
        public Guid LogId;

        public Log Log = null;

        public string Name;
        public List<Polyline> Top;
        public Polyline Centre;
        public List<Polyline> Bottom;
        public Plane Plane;
        public double Thickness;

        public Board(string name = "Board") : this(null, name)
        {
        }

        public Board(Log log, string name = "Board", double thickness = 24.0)
        {
            Log = log;
            //LogId = log.LogId;
            Thickness = thickness;
            Name = name;

            Top = new List<Polyline>();
            Centre = new Polyline();
            Bottom = new List<Polyline>();
            Plane = Plane.WorldXY;

            BoardId = Guid.NewGuid();
        }

        public void Transform(Transform xform)
        {
            for (int i = 0; i < Top.Count; ++i)
            {
                Top[i].Transform(xform);
            }
            Centre.Transform(xform);

            for (int i = 0; i < Bottom.Count; ++i)
            {
                Bottom[i].Transform(xform);
            }
            Plane.Transform(xform);
        }

        public Board Duplicate()
        {
            var board = new Board()
            {
                Name = Name,
                Centre = Centre.Duplicate(),
                Plane = Plane,
                Thickness = Thickness,
                Log = Log

            };

            for (int i = 0; i < Top.Count; ++i)
            {
                board.Top.Add(Top[i].Duplicate());
            }

            for (int i = 0; i < Bottom.Count; ++i)
            {
                board.Bottom.Add(Bottom[i].Duplicate());
            }

            return board;
        }
    }
}
#endif
