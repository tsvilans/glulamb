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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;
using Rhino.Collections;

namespace GluLamb
{

    public abstract partial class Glulam
    {
        protected Glulam()
        {
            Id = Guid.NewGuid();
            CornerGenerator = GenerateCorners;
        }

        static public Glulam CreateGlulam(Curve curve, CrossSectionOrientation orientation, GlulamData data)
        {
            Glulam glulam;
            if (curve.IsLinear(Tolerance))
            {
                glulam = new StraightGlulam(curve, orientation, data);
            }
            else if (curve.IsPlanar(Tolerance))
            {
                /*
                if (data.NumHeight < 2)
                {
                    data.Lamellae.ResizeArray(data.NumWidth, 2);
                    data.LamHeight /= 2;
                }
                */

                glulam = new SingleCurvedGlulam(curve, orientation, data);
            }
            else
            {
                /*
                if (data.NumHeight < 2)
                {
                    data.Lamellae.ResizeArray(data.NumWidth, 2);
                    data.LamHeight /= 2;
                }

                if (data.NumWidth < 2)
                {
                    data.Lamellae.ResizeArray(2, data.NumHeight);
                    data.LamWidth /= 2;
                }
                */

                glulam = new DoubleCurvedGlulam(curve, orientation, data);

            }

            return glulam;
        }

        /// <summary>
        /// Glulam factory methods.
        /// </summary>
        /// <param name="curve">Input curve.</param>
        /// <param name="planes">Input orientation planes.</param>
        /// <param name="data">Input glulam data.</param>
        /// <returns>New glulam.</returns>
        static public Glulam Create(Curve curve, Plane[] planes = null, GlulamData data = null)
        {
            //if (data == null) data = GlulamData.FromCurveLimits(curve);
            if (data == null) data = GlulamData.Default;


            Glulam glulam;
            if (planes == null || planes.Length < 1)
            // if there are no planes defined, create defaults
            {
                Plane p;
                if (curve.IsLinear(Tolerance))
                {
                    curve.PerpendicularFrameAt(curve.Domain.Min, out p);
                    glulam = new StraightGlulam(curve, new Plane[] { p });
                }
                else if (curve.IsPlanar(Tolerance))
                {
                    curve.TryGetPlane(out p, Tolerance);
                    glulam = new SingleCurvedGlulam(curve, new Plane[]
                    {
                        new Plane(
                            curve.PointAtStart,
                            p.ZAxis,
                            Vector3d.CrossProduct(
                                curve.TangentAtStart, p.ZAxis
                                )
                            ),
                        new Plane(
                            curve.PointAtEnd,
                            p.ZAxis,
                            Vector3d.CrossProduct(
                                curve.TangentAtEnd, p.ZAxis
                                )
                            )

                    });
                }
                else
                {
                    Plane start, end;
                    curve.PerpendicularFrameAt(curve.Domain.Min, out start);
                    curve.PerpendicularFrameAt(curve.Domain.Max, out end);
                    glulam = new DoubleCurvedGlulam(curve, new Plane[] { start, end });
                }
            }
            else // if there are planes defined
            {
                if (curve.IsLinear(Tolerance))
                {
                    if (planes.Length == 1)
                        glulam = new StraightGlulam(curve, planes);
                    else
                    {
                        glulam = new StraightGlulam(curve, planes, true);
                        // glulam = new StraightGlulamWithTwist(curve, planes);
                        Console.WriteLine("Not implemented...");
                    }
                }
                else if (curve.IsPlanar(Tolerance))
                {
                    Plane crv_plane;
                    curve.TryGetPlane(out crv_plane);

                    /*
                     * Are all the planes perpendicular to the curve normal?
                     *    Yes: basic SC Glulam
                     * Are all the planes consistently aligned from the curve normal?
                     *    Yes: SC Glulam with rotated cross-section
                     * SC Glulam with twisting
                     */

                    bool HasTwist = false;

                    foreach (Plane p in planes)
                    {
                        if (Math.Abs(p.XAxis * crv_plane.ZAxis) > Tolerance)
                        {
                            HasTwist = true;
                        }
                    }
                    if (HasTwist)
                        glulam = new DoubleCurvedGlulam(curve, planes);
                    else
                    {

                        Plane first = new Plane(curve.PointAtStart, crv_plane.ZAxis, Vector3d.CrossProduct(curve.TangentAtStart, crv_plane.ZAxis));
                        Plane last = new Plane(curve.PointAtEnd, crv_plane.ZAxis, Vector3d.CrossProduct(curve.TangentAtEnd, crv_plane.ZAxis));
                        glulam = new SingleCurvedGlulam(curve, new Plane[] { first, last });
                    }
                }
                else
                {
                    Plane temp;
                    double t;
                    bool Twisted = false;
                    curve.PerpendicularFrameAt(curve.Domain.Min, out temp);

                    double Angle = Vector3d.VectorAngle(planes[0].YAxis, temp.YAxis);

                    for (int i = 0; i < planes.Length; ++i)
                    {
                        curve.ClosestPoint(planes[i].Origin, out t);
                        curve.PerpendicularFrameAt(t, out temp);

                        if (Math.Abs(Vector3d.VectorAngle(planes[0].YAxis, temp.YAxis) - Angle) > AngleTolerance)
                        {
                            // Twisting Glulam
                            Twisted = true;
                            break;
                        }
                    }
                    /*
                     * Are all the planes consistently aligned from some plane?
                     *    Yes: DC Glulam with constant cross-section
                     * Are all the planes at a consistent angle from the perpendicular frame of the curve?
                     *    Yes: DC Glulam with minimal twisting
                     * DC Glulam with twisting
                     */

                    if (Twisted)
                        // TODO: differentiate between DC Glulam with minimal twist, and DC Glulam with twist
                        glulam = new DoubleCurvedGlulam(curve, planes);
                    else
                        glulam = new DoubleCurvedGlulam(curve, planes);
                }
            }

            //glulam.ValidateFrames();

            int nh = data.NumHeight;
            int nw = data.NumWidth;

            if (glulam is DoubleCurvedGlulam)
            {
                if (data.NumHeight < 2)
                {
                    nh = 2;
                    data.LamHeight /= 2;
                }

                if (data.NumWidth < 2)
                {
                    nw = 2;
                    data.LamWidth /= 2;
                }

                data.Lamellae.ResizeArray(nw, nh);
            }
            else if (glulam is SingleCurvedGlulam)
            {
                if (data.NumHeight < 2)
                {
                    nh = 2;
                    data.LamHeight /= 2;
                }
                data.Lamellae.ResizeArray(nw, nh);
            }

            glulam.Data = data;

            return glulam;
        }

        /*
        /// <summary>
        /// Create glulam with frames that are aligned with a Brep. The input curve does not
        /// necessarily have to lie on the Brep.
        /// </summary>
        /// <param name="curve">Input centreline of the glulam.</param>
        /// <param name="brep">Brep to align the glulam orientation to.</param>
        /// <param name="num_samples">Number of orientation frames to use for alignment.</param>
        /// <returns>New Glulam oriented to the brep.</returns>
        static public Glulam CreateGlulamNormalToSurface(Curve curve, Brep brep, int num_samples = 20, GlulamData data = null)
        {
            Plane[] frames = Utility.FramesNormalToSurface(curve, brep, num_samples);
            return Glulam.Create(curve, frames, data);
        }
        */

        /*
        /// <summary>
        /// Create a Glulam from arbitrary geometry and a guide curve. The curve describes the fibre direction of the Glulam. This will
        /// create a Glulam which completely envelops the input geometry and whose centreline is offset from the input guide curve, to 
        /// preserve the desired fibre orientation. 
        /// </summary>
        /// <param name="curve">Guide curve to direct the Glulam.</param>
        /// <param name="beam">Beam geometry as Mesh.</param>
        /// <param name="extra">Extra material tolerance to leave on Glulam (the width and height of the Glulam will be 
        /// increased by this much).</param>
        /// <returns>A new Glulam which envelops the input beam geometry, plus an extra tolerance as defined above.</returns>
        static public Glulam CreateGlulamFromBeamGeometry(Curve curve, Mesh beam, out double true_width, out double true_height, out double true_length, double extra = 0.0)
        {
            double t, l;
            Plane cp = Plane.Unset;
            Plane cpp;
            Polyline ch;
            Mesh m = new Mesh();

            List<Plane> frames = new List<Plane>();
            double[] tt = curve.DivideByCount(20, true);

            if (curve.IsLinear())
            {
                m = beam.DuplicateMesh();
                curve.PerpendicularFrameAt(curve.Domain.Min, out cp);
                m.Transform(Rhino.Geometry.Transform.PlaneToPlane(cp, Plane.WorldXY));
                m.Faces.Clear();

                Plane twist = Plane.WorldXY;
                m = m.FitToAxes(Plane.WorldXY, out ch, ref twist);

                double angle = Vector3d.VectorAngle(Vector3d.XAxis, twist.XAxis);
                int sign = Math.Sign(twist.YAxis * Vector3d.XAxis);

                cp.Transform(Rhino.Geometry.Transform.Rotation(angle * sign, cp.ZAxis, cp.Origin));
                frames.Add(cp);
            }
            else if (curve.TryGetPlane(out cpp, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance))
            {
                for (int i = 0; i < tt.Length; ++i)
                {
                    Vector3d xaxis = Vector3d.CrossProduct(cpp.ZAxis, curve.TangentAt(tt[i]));
                    cp = new Plane(curve.PointAt(tt[i]), xaxis, cpp.ZAxis);
                    frames.Add(cp);
                }
                for (int i = 0; i < beam.Vertices.Count; ++i)
                {
                    Point3d p = new Point3d(beam.Vertices[i]);
                    curve.ClosestPoint(p, out t);
                    l = curve.GetLength(new Interval(curve.Domain.Min, t));
                    Vector3d xaxis = Vector3d.CrossProduct(cpp.ZAxis, curve.TangentAt(t));
                    cp = new Plane(curve.PointAt(t), xaxis, cpp.ZAxis);
                    p.Transform(Rhino.Geometry.Transform.PlaneToPlane(cp, Plane.WorldXY));
                    p.Z = l;
                    m.Vertices.Add(p);
                }
            }
            else
            {
                for (int i = 0; i < beam.Vertices.Count; ++i)
                {
                    Point3d p = new Point3d(beam.Vertices[i]);
                    curve.ClosestPoint(p, out t);
                    l = curve.GetLength(new Interval(curve.Domain.Min, t));
                    curve.PerpendicularFrameAt(t, out cp);
                    p.Transform(Rhino.Geometry.Transform.PlaneToPlane(cp, Plane.WorldXY));
                    p.Z = l;

                    m.Vertices.Add(p);
                }

                Plane twist = Plane.WorldXY;
                m = m.FitToAxes(Plane.WorldXY, out ch, ref twist);
                double angle = Vector3d.VectorAngle(Vector3d.XAxis, twist.XAxis);
                int sign = Math.Sign(twist.YAxis * Vector3d.XAxis);

                for (int i = 0; i < tt.Length; ++i)
                {
                    curve.PerpendicularFrameAt(tt[i], out cp);
                    cp.Transform(Rhino.Geometry.Transform.Rotation(angle * sign, cp.ZAxis, cp.Origin));
                    frames.Add(cp);
                }
            }

            m.Faces.AddFaces(beam.Faces);
            m.FaceNormals.ComputeFaceNormals();

            BoundingBox bb = m.GetBoundingBox(true);

            double offsetX = bb.Center.X;
            double offsetY = bb.Center.Y;

            Brep bb2 = bb.ToBrep();
            bb2.Transform(Rhino.Geometry.Transform.PlaneToPlane(Plane.WorldXY, cp));

            true_width = bb.Max.X - bb.Min.X + extra;
            true_height = bb.Max.Y - bb.Min.Y + extra;

            // Now we create the glulam...

            //tasTools.Lam.Glulam glulam = tasTools.Lam.Glulam.CreateGlulam(curve, frames.ToArray());
            Beam temp_beam = new Beam(curve, null, frames.ToArray());
            int samples = (int)(curve.GetLength() / GlulamData.DefaultSampleDistance);
            Curve new_curve = temp_beam.CreateOffsetCurve(offsetX, offsetY, samples, true);
            new_curve = new_curve.Extend(CurveEnd.Both, 5.0 + extra, CurveExtensionStyle.Smooth);

            GlulamData data = GlulamData.FromCurveLimits(new_curve, frames.ToArray());

            if (new_curve.IsPlanar())
            {
                data.LamHeight = Math.Ceiling(true_height);
                data.Lamellae = new Stick[(int)(Math.Ceiling(true_width / data.LamWidth)), 1];

            }
            else if (new_curve.IsLinear())
            {
                data.LamHeight = Math.Ceiling(true_height);
                data.LamWidth = Math.Ceiling(true_width);
                data.Lamellae = new Stick[1, 1];

            }
            else
            {
                data.Lamellae = new Stick[(int)(Math.Ceiling(true_width / data.LamWidth)), (int)(Math.Ceiling(true_height / data.LamHeight))];

            }


            Glulam glulam = Create(new_curve, frames.ToArray(), data);

            true_length = new_curve.GetLength();

            return glulam;
        }
        
        static public Glulam FromBeamGeometry2(Curve curve, Mesh beam, out double true_width, out double true_height, out double true_length, double extra = 0.0)
        {

            Mesh mm = curve.MapToCurveSpace(beam);

            BoundingBox bb = mm.GetBoundingBox(true);

            double x = bb.Center.X;
            double y = bb.Center.Y;

            double tmin, tmax;
            curve.LengthParameter(bb.Min.Z, out tmin);
            curve.LengthParameter(bb.Max.Z, out tmax);

            Plane twist = Plane.WorldXY;
            Polyline ch;
            mm = mm.FitToAxes(Plane.WorldXY, out ch, ref twist);

            bb = mm.GetBoundingBox(true);
            double dx = bb.Max.X - bb.Min.X;
            double dy = bb.Max.Y - bb.Min.Y;

            Plane cp;
            curve.PerpendicularFrameAt(tmin, out cp);


            double angle = Vector3d.VectorAngle(Vector3d.XAxis, twist.XAxis);
            int sign = Math.Sign(twist.YAxis * Vector3d.XAxis);

            Curve[] segments = curve.Split(new double[] { tmin, tmax });
            if (segments.Length == 3)
                curve = segments[1];
            else
                curve = segments[0];

            Beam b = new Beam(curve, null, new Plane[] { cp });

            //curve = b.CreateOffsetCurve(-x, -y);
            curve = b.CreateOffsetCurve(x, y);
            curve = curve.Extend(CurveEnd.Both, extra, CurveExtensionStyle.Smooth);

            cp.Transform(Rhino.Geometry.Transform.Rotation(angle * sign, cp.ZAxis, cp.Origin));

            GlulamData data = GlulamData.FromCurveLimits(curve, dx + extra * 2, dy + extra * 2, new Plane[] { cp });

            true_length = curve.GetLength();
            true_width = dx + extra * 2;
            true_height = dy + extra * 2;

            return Create(curve, new Plane[] { cp }, data);
        }
        
        static public Glulam FromBeamGeometry(Curve curve, Mesh beam, double extra = 0.0)
        {
            double w, h, l;
            return FromBeamGeometry2(curve, beam, out w, out h, out l, extra);
        }
        */
    }
}
