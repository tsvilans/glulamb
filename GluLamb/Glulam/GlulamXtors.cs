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
            Id = Guid.NewGuid();
            CornerGenerator = GenerateCorners;
        }
        static public Glulam CreateGlulam(Beam beam, CrossSectionOrientation orientation, Standards.Standard standard= Standards.Standard.None)
        {
            Glulam glulam;

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
