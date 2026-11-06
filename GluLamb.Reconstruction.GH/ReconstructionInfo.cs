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

using System.Drawing;
using Grasshopper.Kernel;

namespace GluLamb.Reconstruction.GH
{
    public class ReconstructionInfo : GH_AssemblyInfo
    {
        public override string Name => "GluLamb Reconstruction";
        public override Bitmap Icon => null;
        public override string Description => "Beam and blank reconstruction tools for GluLamb.";
        public override Guid Id => new Guid("5e2c7a91-3f4d-4b8e-a6c0-9d1b7e3f2a64");
        public override string AuthorName => "Tom Svilans";
        public override string AuthorContact => "info@tomsvilans.com";
        public override GH_LibraryLicense License => GH_LibraryLicense.free;
    }

    internal static class UiNames
    {
        public static string ReconstructionSection = "6. Reconstruction";
    }
}
