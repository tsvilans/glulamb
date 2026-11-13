/*
 * GluLamb
 * A constrained glulam modelling toolkit.
 * Copyright 2026 Tom Svilans
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

namespace GluLamb.Numerics
{
    public static class Sampling
    {
        /// <summary>
        /// n evenly spaced values from a to b, both included (numpy's linspace). One value: the
        /// middle.
        /// </summary>
        public static double[] Linspace(double a, double b, int n)
        {
            if (n < 1) return Array.Empty<double>();
            if (n == 1) return new[] { (a + b) * 0.5 };
            var values = new double[n];
            for (int i = 0; i < n; ++i) values[i] = a + (b - a) * i / (n - 1);
            return values;
        }
    }
}
