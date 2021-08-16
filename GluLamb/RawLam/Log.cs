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
        public string Name;
        public List<Polyline> Top;
        public Polyline Centre;
        public List<Polyline> Bottom;
        public Plane Plane;
        public double Thickness;

        public Board(string name = "Board")
        {
            Name = name;
            Top = new List<Polyline>();
            Centre = new Polyline();
            Bottom = new List<Polyline>();
            Plane = Plane.WorldXY;
            Thickness = 0;


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
                Thickness = Thickness

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


    public class Log
    {
        public string Name;
        public Mesh Mesh;
        public Plane BasePlane;

        public List<Board> Boards;

        public Log()
        {
            Boards = new List<Board>();
            BasePlane = Plane.WorldXY;
        }
        
        public void Transform(Transform xform)
        {
            Mesh.Transform(xform);
            BasePlane.Transform(xform);
            for (int i = 0; i < Boards.Count; ++i)
            {
                Boards[i].Transform(xform);
            }
        }

        public Log Duplicate()
        {
            var log = new Log() { Name = Name, BasePlane = BasePlane, Mesh = Mesh.DuplicateMesh() };
            for (int i = 0; i < Boards.Count; ++i)
            {
                log.Boards.Add(Boards[i].Duplicate());
            }

            return log;
        }
    }
}
#endif
