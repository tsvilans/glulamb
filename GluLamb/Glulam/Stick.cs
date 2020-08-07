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

namespace GluLamb
{
    public class Stick
    {
        public string Species = "Spruce";
        public Guid Reference;

        public Stick(string species = "Spruce")
        {
            Species = species;
            Reference = Guid.Empty;
        }

        public Stick(Guid reference, string species = "Spruce")
        {
            if (!string.IsNullOrWhiteSpace(species))
                Species = species;
            Reference = reference;
        }
    }
}
