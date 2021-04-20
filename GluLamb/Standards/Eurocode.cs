/*
 * GluLamb
 * A constrained glulam modelling toolkit.
 * Copyright 2021 Tom Svilans
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

namespace GluLamb.Standards
{
    /// <summary>
    /// The Eurocode 5 standard.
    /// </summary>
    public class Eurocode : StandardBase<Eurocode>
    {
        private double MaxThickness = 80.0;
        /// <summary>
        /// From Eurocode EN 14080:2013 section Annex I.5.1
        /// </summary>
        /// <param name="curvature">Maximum curvature on inner face.</param>
        /// <returns>Maximum lamination thickness.</returns>
        public override double CalculateLaminationThickness(double curvature)
        {
            if (curvature <= 0) return MaxThickness;

            double radius = 1 / curvature;

            double t = radius / 200;
            return Math.Min(t, MaxThickness);
        }


    }
}
