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

using Rhino;
using Rhino.Geometry;

namespace GluLamb.Reconstruction
{
    /// <summary>
    /// Feature edges of a mesh or Brep, as polylines.
    /// </summary>
    public static class Edges
    {
        /// <summary>
        /// Sharp edges of a mesh, joined into polylines. An edge is sharp if the normals of its two
        /// faces differ by more than sharpAngle; sharp edges that meet at a vertex and run within
        /// joinAngle of each other are joined. The mesh is not changed.
        /// </summary>
        /// <param name="mesh">Mesh, e.g. from an IFC, OBJ or STL file.</param>
        /// <param name="sharpAngle">Smallest angle (radians) between face normals for a sharp edge.</param>
        /// <param name="joinAngle">Largest angle (radians) between two sharp edges that are joined.</param>
        /// <param name="weldAngle">Angle (radians) to weld the mesh's vertices at first, so that
        /// unwelded meshes (e.g. STL) have a topology.</param>
        public static List<Polyline> SharpMeshEdges(Mesh mesh, double sharpAngle, double joinAngle, double weldAngle)
        {
            var m = mesh.DuplicateMesh();
            m.Weld(weldAngle);
            m.FaceNormals.ComputeFaceNormals();

            var topology = m.TopologyEdges;
            var sharp = new List<int>();
            for (int e = 0; e < topology.Count; ++e)
            {
                var faces = topology.GetConnectedFaces(e);
                if (faces.Length != 2) continue;
                if (Vector3d.VectorAngle(m.FaceNormals[faces[0]], m.FaceNormals[faces[1]]) > sharpAngle)
                    sharp.Add(e);
            }

            // Join sharp edges that share a vertex and run in about the same direction
            int n = sharp.Count;
            var directions = new Vector3d[n];
            var byVertex = new Dictionary<int, List<int>>();
            for (int i = 0; i < n; ++i)
            {
                var line = topology.EdgeLine(sharp[i]);
                directions[i] = line.Direction;
                directions[i].Unitize();

                var ends = topology.GetTopologyVertices(sharp[i]);
                foreach (var v in new[] { ends.I, ends.J })
                {
                    if (!byVertex.TryGetValue(v, out var list)) byVertex[v] = list = new List<int>();
                    list.Add(i);
                }
            }

            var parent = Enumerable.Range(0, n).ToArray();
            int Find(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }

            double cosJoin = Math.Cos(joinAngle);
            foreach (var list in byVertex.Values)
                for (int a = 0; a < list.Count; ++a)
                    for (int b = a + 1; b < list.Count; ++b)
                        if (Math.Abs(directions[list[a]] * directions[list[b]]) > cosJoin)
                            parent[Find(list[a])] = Find(list[b]);

            var groups = new Dictionary<int, List<Curve>>();
            for (int i = 0; i < n; ++i)
            {
                int root = Find(i);
                if (!groups.TryGetValue(root, out var curves)) groups[root] = curves = new List<Curve>();
                curves.Add(new LineCurve(topology.EdgeLine(sharp[i])));
            }

            var polylines = new List<Polyline>();
            foreach (var curves in groups.Values)
                foreach (var joined in Curve.JoinCurves(curves))
                    if (joined.TryGetPolyline(out Polyline polyline) && polyline.Count > 1)
                        polylines.Add(polyline);

            return polylines;
        }

        /// <summary>
        /// The edges of a Brep as polylines: straight edges as two points, others divided into
        /// segments.
        /// </summary>
        public static List<Polyline> BrepEdges(Brep brep, int segments = 20)
        {
            var polylines = new List<Polyline>();
            foreach (var edge in brep.Edges)
                polylines.Add(ToPolyline(edge, segments));
            return polylines;
        }

        public static Polyline ToPolyline(Curve curve, int segments = 20)
        {
            if (curve.IsLinear(RhinoMath.ZeroTolerance * 1e3))
                return new Polyline(new[] { curve.PointAtStart, curve.PointAtEnd });
            if (curve.TryGetPolyline(out Polyline polyline))
                return polyline;

            var tt = curve.DivideByCount(segments, true);
            return new Polyline(tt.Select(t => curve.PointAt(t)));
        }
    }
}
