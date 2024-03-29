﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;

namespace GluLamb.Joints
{
    [Serializable]
    public class ConnectorPlate
    {
        public double Thickness = 21;
        public Polyline[] Outlines;
        public Polyline OutlineTop { get { return Outlines[0]; } set { Outlines[0] = value; } }
        public Polyline OutlineBottom { get { return Outlines[1]; } set { Outlines[1] = value; } }
        public Plane Plane;
        public Brep Geometry;

        public string Name;

        public List<Dowel> Dowels;

        public ConnectorPlate(string name = "ConnectorPlate")
        {
            Outlines = new Polyline[2];
            Dowels = new List<Dowel>();
            Name = name;
        }
    }

    [Serializable]
    public class Dowel
    {
        public double Diameter = 16;
        public Line Axis;
        public double DrillDepth;

        public Dowel(Line axis, double diameter=16, double depth=0)
        {
            Axis = axis;
            Diameter = diameter;
            DrillDepth = depth > 0? depth : axis.Length;
        }

        public void Transform(Transform xform)
        {
            Axis.Transform(xform);
        }
    }
}
