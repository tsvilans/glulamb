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
using System.Threading.Tasks;

using Rhino.Geometry;

using Grasshopper.Kernel;
using Grasshopper;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Data;
using System.Linq;

using GluLamb.Raw;
using System.Runtime.Versioning;

namespace GluLamb.Raw.GH.Components
{
    public class Cmpt_FindCentreline : GH_Component
    {
        public Cmpt_FindCentreline()
          : base("Find Centreline", "CLine",
              "Find the estimated centreline for a beam-like object.",
              "GluLamb", UiNames.RawSection)
        {
        }


        protected override System.Drawing.Bitmap Icon => Properties.Resources.FindCentreline;
        public override Guid ComponentGuid => new Guid("d26a69d4-f3bc-4033-bc5d-0cdea2b1526c");
        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "Mesh for which to find a centreline."+
                "A good mesh has an even distribution of vertices.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Iterations", "I", "Number of iterations to perform.", GH_ParamAccess.item, 3);
            pManager.AddNumberParameter("EndOffset", "EO", "Sampling at the ends of elements might be difficult " +
                "due to joint geometries. This limits the range of sampling to avoid these areas.", GH_ParamAccess.item, 200);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Curve", "C", "Estimated centreline of beam-like object.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Width", "W", "Estimated width of beam-like object.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Height", "H", "Estimated height of beam-like object.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Mesh mesh = null;
            if (!DA.GetData("Mesh", ref mesh))
            {
                return;
            }
            
            // Get plane of best fit
            var pts = mesh.Vertices.ToPoint3dArray();
            int N = pts.Length;

            double[] X = new double[N], Y = new double[N], Z = new double[N];

            Point3d mean = Point3d.Origin;
            for (int i = 0; i < N; ++i)
            {
                X[i] = pts[i].X;
                Y[i] = pts[i].Y;
                Z[i] = pts[i].Z;

                mean += pts[i];
            }

            double[,] evec;
            double[] ev;

            GluLamb.Raw.Utility.GetEigenVectors(X, Y, Z, out ev, out evec);

            var vecs = new Vector3d[3];
            for (int i = 0; i < 3; ++i)
            {
                vecs[i] = new Vector3d(
                    evec[0, i] * ev[i],
                    evec[1, i] * ev[i],
                    evec[2, i] * ev[i]
                );
            }

            var plane = new Plane(mean, vecs[2], vecs[1]);

            // Estimate centreline

            var bb = mesh.GetBoundingBox(plane);
            var iterations = 5;
            double endOffset = 200.0;

            DA.GetData("Iterations", ref iterations);
            DA.GetData("EndOffset", ref endOffset);

            var line = new Line(
                new Point3d(bb.Min.X + endOffset, 0, 0),
                new Point3d(bb.Max.X - endOffset, 0, 0)
            );

            line.Transform(Transform.PlaneToPlane(Plane.WorldXY, plane));

            Curve curve = line.ToNurbsCurve();

            double extensionLengthStart = 0, extensionLengthEnd = 0;
            double segmentLength = 100;

            double maxSectionX = 0;
            double maxSectionY = 0;

            for (int i = 0; i < iterations; ++i)
            {
                maxSectionX = 0;
                maxSectionY = 0;
                var length = curve.GetLength();
                N = (int)Math.Ceiling(length / segmentLength);
                //Print($"N: {N}");

                var tt = curve.DivideByCount(N, true);

                var curvePoints = new List<Point3d>();

                //Print($"Entering loop ({tt.Length} params)...");

                for (int j = 0; j < tt.Length; ++j)
                {
                    //Print($"t[{j}] = {tt[j]}");
                    if (!curve.Domain.IncludesParameter(tt[j]))
                        continue;

                    var xaxis = Vector3d.CrossProduct(plane.ZAxis, curve.TangentAt(tt[j]));
                    var yaxis = Vector3d.CrossProduct(xaxis, curve.TangentAt(tt[j]));

                    var sectionPlane = new Plane(
                        curve.PointAt(tt[j]),
                        xaxis, yaxis
//                        curve.TangentAt(tt[j])
                        );


                    var res = Rhino.Geometry.Intersect.Intersection.MeshPlane(mesh, sectionPlane);

                    if (res == null || res.Length < 1) continue;

                    var sectionBb = BoundingBox.Empty;
                    foreach (var r in res)
                    {
                        foreach (var rpt in r)
                        {
                            sectionPlane.RemapToPlaneSpace(rpt, out Point3d planePt);
                            sectionBb.Union(planePt);
                        }
                    }

                    maxSectionX = Math.Max(maxSectionX, sectionBb.Max.X - sectionBb.Min.X);
                    maxSectionY = Math.Max(maxSectionY, sectionBb.Max.Y - sectionBb.Min.Y);

                    var amp = AreaMassProperties.Compute(res.Select(x => x.ToNurbsCurve()));
                    if (amp == null) continue;
                    curvePoints.Add(amp.Centroid);
                }

                if (curvePoints.Count < 2) throw new Exception("Failed to get any curve points.");

                //curve = Curve.CreateInterpolatedCurve(curvePoints, 3);
                curve = Curve.CreateControlPointCurve(curvePoints, 3);
                if (curve == null) throw new Exception("Failed to create curve.");

                var startPoint = curve.PointAtStart;
                var startTangent = -curve.TangentAtStart;

                var endPoint = curve.PointAtEnd;
                var endTangent = curve.TangentAtEnd;

                extensionLengthStart = 0;
                extensionLengthEnd = 0;

                double dot = 0;
                for (int j = 0; j < mesh.Vertices.Count; ++j)
                {
                    var vStart = mesh.Vertices[j] - startPoint;
                    dot = startTangent * vStart;
                    if (dot >= 0)
                        extensionLengthStart = Math.Max(dot, extensionLengthStart);

                    var vEnd = mesh.Vertices[j] - endPoint;
                    dot = endTangent * vEnd;
                    if (dot >= 0)
                        extensionLengthEnd = Math.Max(dot, extensionLengthEnd);

                }

                if (i < iterations - 1)
                {
                    extensionLengthStart *= 0.5;
                    extensionLengthEnd *= 0.5;
                }

                var extensionStyle = curve.IsLinear() ? CurveExtensionStyle.Line : CurveExtensionStyle.Smooth;
                extensionStyle = CurveExtensionStyle.Line;

                if (extensionLengthStart > 0)
                {
                    var temp = curve.Extend(CurveEnd.Start, extensionLengthStart, extensionStyle);
                    if (temp == null) throw new Exception("Failed to extend curve.");
                    curve = temp;
                }
                if (extensionLengthEnd > 0)
                {
                    var temp = curve.Extend(CurveEnd.End, extensionLengthEnd, extensionStyle);
                    if (temp == null) throw new Exception("Failed to extend curve.");
                    curve = temp;
                }

                segmentLength *= 1.0;
            }

            // Write your logic here
            DA.SetData("Curve", curve);
            DA.SetData("Width", maxSectionX);
            DA.SetData("Height", maxSectionY);
        }
    }
}