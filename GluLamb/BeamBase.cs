/*
 * GluLamb
 * A constrained glulam modelling toolkit.
 * Copyright 2020 Tom Svilans
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;

namespace GluLamb
{
    public abstract class BeamBase
    {
        public Curve Centreline { get; protected set; }
        public CrossSectionOrientation Orientation;

        public Plane GetPlane(double t) => Utility.PlaneFromNormalAndYAxis(
                                                        Centreline.PointAt(t),
                                                        Centreline.TangentAt(t),
                                                        Orientation.GetOrientation(Centreline, t));
        public Plane GetPlane(Point3d pt)
        {
            Centreline.ClosestPoint(pt, out double t);
            return GetPlane(t);
        }
        public void Transform(Transform x)
        {
            Centreline.Transform(x);
            Orientation.Transform(x);
        }

    }
}
