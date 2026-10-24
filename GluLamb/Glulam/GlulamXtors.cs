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
using Rhino.Collections;

namespace GluLamb
{

    public abstract partial class Glulam
    {
        protected Glulam()
        {
            CornerGenerator = GenerateCorners;
        }
        /// <summary>
        /// Create a glulam from a beam, with lamellae sized by the standard for the beam's curvature.
        /// The glulam gets a copy of the orientation, and keeps the beam's id, frames and section offsets.
        /// </summary>
        static public Glulam CreateGlulam(Beam beam, CrossSectionOrientation orientation, Standards.Standard standard= Standards.Standard.None)
        {
            Glulam glulam;
            orientation = (orientation ?? beam.Orientation)?.Duplicate();

            if (beam.Centreline.IsLinear(Tolerance))
            {
                glulam = new StraightGlulam { Centreline = beam.Centreline.DuplicateCurve(), Orientation = orientation, Data = new GlulamData() };
                glulam.Data.Compute(beam, standard);
            }
            else if (beam.Centreline.IsPlanar(Tolerance))
            {
                glulam = new SingleCurvedGlulam { Centreline = beam.Centreline.DuplicateCurve(), Orientation = orientation, Data = new GlulamData() };
                glulam.Data.Compute(beam, standard);
            }
            else
            {
                glulam = new DoubleCurvedGlulam { Centreline = beam.Centreline.DuplicateCurve(), Orientation = orientation, Data = new GlulamData() };
                glulam.Data.Compute(beam, standard);
            }

            glulam.Id = beam.Id;
            glulam.Frames = beam.Frames == null ? new Dictionary<string, Plane>() : new Dictionary<string, Plane>(beam.Frames);
            glulam.OffsetX = beam.OffsetX;
            glulam.OffsetY = beam.OffsetY;
            return glulam;
        }

        static public Glulam CreateGlulam(Curve curve, CrossSectionOrientation orientation, GlulamData data)
        {
            Glulam glulam;
            if (curve.IsLinear(Tolerance))
            {
                glulam = new StraightGlulam { Centreline = curve.DuplicateCurve(), Orientation = orientation, Data = data.Duplicate() };
            }
            else if (curve.IsPlanar(Tolerance))
            {
                /*
                if (data.NumHeight < 2)
                {
                    data.Lamellae.ResizeArray(data.NumWidth, 2);
                    data.LamHeight /= 2;
                }
                */
                glulam = new SingleCurvedGlulam { Centreline = curve.DuplicateCurve(), Orientation = orientation, Data = data.Duplicate() };
            }
            else
            {
                /*
                if (data.NumHeight < 2)
                {
                    data.Lamellae.ResizeArray(data.NumWidth, 2);
                    data.LamHeight /= 2;
                }

                if (data.NumWidth < 2)
                {
                    data.Lamellae.ResizeArray(2, data.NumHeight);
                    data.LamWidth /= 2;
                }
                */

                //glulam = new DoubleCurvedGlulam(curve, orientation, data);
                glulam = new DoubleCurvedGlulam { Centreline = curve.DuplicateCurve(), Orientation = orientation, Data = data.Duplicate() };
            }

            return glulam;
        }

    }
}
