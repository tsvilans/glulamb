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
    /// The ANSI APA standard.
    /// </summary>
    public class ANSI : StandardBase<ANSI>
    {
        /// <summary>
        /// From ANSI 117-2020, section 3.8 Shapes
        /// </summary>
        /// <param name="curvature">Maximum curvature on inner face.</param>
        /// <returns>Maximum lamination thickness.</returns>
        public override double CalculateLaminationThickness(double curvature)
        {
            if (curvature <= 0) return 38.0;

            double radius = 1 / curvature;

            // For curved members manufactured with nominal 2-inch thickness laminations,
            // the recommended minimum radius of curvature (at the inside face) is 18 feet
            // (5.5m) for southern pine and 27 feet 6 inches (8.4) for other softwood species.
            // Until species are implemented, we assume the greater value.
            if (radius >= 8400)
            {
                // Nominal 2" lamination
                return 38;
            }
            // Minimum radius for nominal 1" laminations for Southern Pine is 7'0" (~2.1 m)
            // Minimum radius for nominal 1" laminations for all other softwood species is 9'4" (~2.8 m)
            // Until species are implemented, we assume the greater value.
            else if (radius >= 2800)
            {
                // Nominal 1" lamination
                return 19;
            }
            // For thin laminations, the radius should not be less than 100 times the lamination 
            // thickness for southern pine or 125 times the lamination thickness for other softwoods.
            // Until species are implemented, we assume the greater value.
            else
            {
                return radius / 125;
            }
        }
    }
}
