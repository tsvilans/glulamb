/*
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
using System.Linq;
using System.Collections.Generic;

using Rhino.Display;
using System.Drawing;
using Rhino.Geometry;
using System.ComponentModel;
using System.Collections;

namespace GluLamb
{
    public static class IO
    {
        public static Mesh ReadBinarySTL(string filePath)
        {
            return ReadBinarySTL(filePath, out List<string> log);
        }

        public static Mesh ReadBinarySTL(string filePath, out List<string> log)
        {
            var mesh = new Mesh();
            log = new List<string>();

            var buffer = System.IO.File.ReadAllBytes(filePath);

            var header = System.Text.Encoding.UTF8.GetString(buffer, 0, 80);
            log.Add($"{header}");

            var nFacets = System.BitConverter.ToInt32(buffer, 80);
            log.Add($"nFacets {nFacets}");

            var index = 84;

            for (int i = 0; i < nFacets; ++i)
            {
                var j = index + (i * 50);

                var nX = System.BitConverter.ToSingle(buffer, j + 0);
                var nY = System.BitConverter.ToSingle(buffer, j + 4);
                var nZ = System.BitConverter.ToSingle(buffer, j + 8);

                var v0X = System.BitConverter.ToSingle(buffer, j + 12 + 0);
                var v0Y = System.BitConverter.ToSingle(buffer, j + 12 + 4);
                var v0Z = System.BitConverter.ToSingle(buffer, j + 12 + 8);

                var v1X = System.BitConverter.ToSingle(buffer, j + 12 + 12);
                var v1Y = System.BitConverter.ToSingle(buffer, j + 12 + 16);
                var v1Z = System.BitConverter.ToSingle(buffer, j + 12 + 20);

                var v2X = System.BitConverter.ToSingle(buffer, j + 12 + 24);
                var v2Y = System.BitConverter.ToSingle(buffer, j + 12 + 28);
                var v2Z = System.BitConverter.ToSingle(buffer, j + 12 + 32);

                var a = mesh.Vertices.Add(v0X, v0Y, v0Z);
                var b = mesh.Vertices.Add(v1X, v1Y, v1Z);
                var c = mesh.Vertices.Add(v2X, v2Y, v2Z);

                mesh.Faces.AddFace(a, b, c);
                mesh.FaceNormals.AddFaceNormal(nX, nY, nZ);
            }

            log.Add($"Pre-weld:  {mesh.Vertices.Count} vertices, {mesh.Faces.Count} faces");

            mesh.Weld(Math.PI);

            log.Add($"Post-weld: {mesh.Vertices.Count} vertices, {mesh.Faces.Count} faces");

            return mesh;
        }
    }
}