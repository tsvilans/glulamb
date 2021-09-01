#if RAWLAM

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;
using GluLamb;

namespace RawLam
{ 
    public class Lamella
    {
        public Guid GlulamId;
        public Glulam Glulam = null;

        private int stackPositionX;
        private int stackPositionY;
        public double Width;
        public double Thickness;
        public double Length;


        public int StackPositionX
        {
            get { return stackPositionX; }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException("Stack position cannot be negative.");
                stackPositionX = value;
            }
        }
        public int StackPositionY
        {
            get { return stackPositionY; }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException("Stack position cannot be negative.");
                stackPositionY = value;
            }
        }

        public Plane Plane; // Local lamella space to glulam stack space
        public Mesh Mesh;

        public Lamella() : this(Guid.NewGuid())
        {
        }

        public Lamella(Guid glulam_id) : this(glulam_id, null)
        {
        }

        public Lamella(Glulam glulam) : this(glulam.Id, glulam)
        {
        }

        public Lamella(Guid glulam_id, Glulam glulam, double thickness = 10.0, int spx = -1, int spy = -1)
        {
            Thickness = 10.0;
            stackPositionX = spx;
            stackPositionY = spy;
            Glulam = glulam;
            GlulamId = glulam_id;
        }
    }
}
#endif