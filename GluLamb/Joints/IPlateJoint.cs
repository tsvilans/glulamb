﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry;

namespace GluLamb.Joints
{
    public interface IPlateJoint
    {

        //Plane PlatePlane { get; set; }
        double MaxFilletRadius { get; set; }
        Brep CreatePlate();

        //Plane GetPlatePlane();

        //Polyline[] GetPlateOutlines();

        ConnectorPlate GetConnectorPlate();
    }
}
