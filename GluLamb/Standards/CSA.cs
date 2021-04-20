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
    /// The CSA standard (CSA). 
    /// </summary>

    public class CSA : StandardBase<CSA>
    {
        /// <summary>
        /// From CAN/CSA-O122-16 (R2021) TODO
        /// </summary>
        /// <param name="curvature">Maximum curvature on inner face.</param>
        /// <returns>Maximum lamination thickness.</returns>
        public override double CalculateLaminationThickness(double curvature)
        {
            throw new NotImplementedException();
        }
    }
}
