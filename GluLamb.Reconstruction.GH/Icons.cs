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

namespace GluLamb.Reconstruction.GH
{
    /// <summary>
    /// Component icons, embedded from Resources/*.png.
    /// </summary>
    internal static class Icons
    {
        static readonly Dictionary<string, Bitmap> Cache = new Dictionary<string, Bitmap>();

        public static Bitmap Load(string name)
        {
            lock (Cache)
            {
                if (Cache.TryGetValue(name, out Bitmap cached)) return cached;

                var assembly = typeof(Icons).Assembly;
                using var stream = assembly.GetManifestResourceStream($"GluLamb.Reconstruction.GH.Resources.{name}");
                var bitmap = stream == null ? null : new Bitmap(stream);
                Cache[name] = bitmap;
                return bitmap;
            }
        }
    }
}
