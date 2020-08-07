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

        public static List<string> ListProperties()
        {
            return new List<string>
            {
                "id",
                "centreline",
                "width",
                "height",
                "length",
                "lamella_width",
                "lamella_height",
                "lamella_count_width",
                "lamella_count_height",
                "volume",
                "samples",
                "max_curvature",
                "max_curvature_width",
                "max_curvature_height",
                "type",
                "type_id",
                "orientation"
            };
        }

        public object GetProperty(string key)
        {
            switch (key)
            {
                case ("id"):
                    return Id;
                case ("centreline"):
                    return Centreline;
                case ("width"):
                    return Width;
                case ("height"):
                    return Height;
                case ("length"):
                    return Centreline.GetLength();
                case ("lamella_width"):
                    return Data.LamWidth;
                case ("lamella_height"):
                    return Data.LamHeight;
                case ("lamella_count_width"):
                    return Data.NumWidth;
                case ("lamella_count_height"):
                    return Data.NumHeight;
                case ("volume"):
                    return GetVolume();
                case ("samples"):
                    return Data.Samples;
                case ("max_curvature"):
                    double max_kw = 0.0, max_kh = 0.0;
                    return GetMaxCurvature(ref max_kw, ref max_kh);
                case ("max_curvature_width"):
                    max_kw = 0.0; max_kh = 0.0;
                    GetMaxCurvature(ref max_kw, ref max_kh);
                    return max_kw;
                case ("max_curvature_height"):
                    max_kw = 0.0; max_kh = 0.0;
                    GetMaxCurvature(ref max_kw, ref max_kh);
                    return max_kh;
                case ("type"):
                    return ToString();
                case ("type_id"):
                    return (int)Type();
                case ("orientation"):
                    return Orientation;
                default:
                    return null;
            }
        }

        public Brep GetEndSurface(int side, double offset, double extra_width, double extra_height, bool flip = false)
        {
            side = side.Modulus(2);
            Plane endPlane = GetPlane(side == 0 ? Centreline.Domain.Min : Centreline.Domain.Max);

            if ((flip && side == 1) || (!flip && side == 0))
                endPlane = endPlane.FlipAroundYAxis();

            endPlane.Origin = endPlane.Origin + endPlane.ZAxis * offset;

            double hwidth = Data.LamWidth * Data.NumWidth / 2 + extra_width;
            double hheight = Data.LamHeight * Data.NumHeight / 2 + extra_height;
            Rectangle3d rec = new Rectangle3d(endPlane, new Interval(-hwidth, hwidth), new Interval(-hheight, hheight));

            return Brep.CreateFromCornerPoints(rec.Corner(0), rec.Corner(1), rec.Corner(2), rec.Corner(3), Tolerance);
        }

        public Brep GetGlulamFace(Side side)
        {
            Plane[] planes;
            double[] parameters;

            int N = Math.Max(Data.Samples, 6);

            GenerateCrossSectionPlanes(N, out planes, out parameters, Data.InterpolationType);

            double hWidth = this.Width / 2;
            double hHeight = this.Height / 2;
            double x1, y1, x2, y2;
            x1 = y1 = x2 = y2 = 0;
            Rectangle3d face;

            GetSectionOffset(out double offsetX, out double offsetY);

            switch (side)
            {
                case (Side.Back):
                    face = new Rectangle3d(planes.First(), new Interval(-hWidth + offsetX, hWidth + offsetX), new Interval(-hHeight + offsetY, hHeight + offsetY));
                    return Brep.CreateFromCornerPoints(face.Corner(0), face.Corner(1), face.Corner(2), face.Corner(3), 0.001);
                case (Side.Front):
                    face = new Rectangle3d(planes.Last(), new Interval(-hWidth + offsetX, hWidth + offsetX), new Interval(-hHeight + offsetY, hHeight + offsetY));
                    return Brep.CreateFromCornerPoints(face.Corner(0), face.Corner(1), face.Corner(2), face.Corner(3), 0.001);
                case (Side.Left):
                    x1 = hWidth + offsetX; y1 = hHeight + offsetY;
                    x2 = hWidth + offsetX; y2 = -hHeight + offsetY;
                    break;
                case (Side.Right):
                    x1 = -hWidth + offsetX; y1 = hHeight + offsetY;
                    x2 = -hWidth + offsetX; y2 = -hHeight + offsetY;
                    break;
                case (Side.Top):
                    x1 = hWidth + offsetX; y1 = hHeight + offsetY;
                    x2 = -hWidth + offsetX; y2 = hHeight + offsetY;
                    break;
                case (Side.Bottom):
                    x1 = hWidth + offsetX; y1 = -hHeight + offsetY;
                    x2 = -hWidth + offsetX; y2 = -hHeight + offsetY;
                    break;
            }

            Curve[] rules = new Curve[parameters.Length];
            for (int i = 0; i < planes.Length; ++i)
                rules[i] = new Line(
                    planes[i].Origin + planes[i].XAxis * x1 + planes[i].YAxis * y1,
                    planes[i].Origin + planes[i].XAxis * x2 + planes[i].YAxis * y2
                    ).ToNurbsCurve();

            Brep[] loft = Brep.CreateFromLoft(rules, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
            if (loft == null || loft.Length < 1) throw new Exception("Glulam::GetGlulamFace::Loft failed!");

            Brep brep = loft[0];

            return brep;
        }

        public Brep[] GetGlulamFaces(int mask)
        {
            bool[] flags = new bool[6];
            List<Brep> breps = new List<Brep>();

            for (int i = 0; i < 6; ++i)
            {
                if ((mask & (1 << i)) > 0)
                    breps.Add(GetGlulamFace((Side)(1 << i)));
            }

            return breps.ToArray();
        }

        public Brep GetSideSurface(int side, double offset, double width, double extension = 0.0, bool flip = false)
        {
            // TODO: Create access for Glulam ends, with offset (either straight or along Centreline).

            side = side.Modulus(2);
            double w2 = width / 2;

            Curve c = Centreline.DuplicateCurve();
            if (extension > 0.0)
                c = c.Extend(CurveEnd.Both, extension, CurveExtensionStyle.Smooth);

            int N = Math.Max(6, Data.Samples);
            GenerateCrossSectionPlanes(N, out Plane[] planes, out double[] parameters, Data.InterpolationType);

            Curve[] rules = new Curve[planes.Length];

            double offsetX, offsetY;
            GetSectionOffset(out offsetX, out offsetY);

            for (int i = 0; i < planes.Length; ++i)
            {
                Plane p = planes[i];
                if (side == 0)
                    rules[i] = new Line(
                        p.Origin + p.XAxis * (offset + offsetX) + p.YAxis * (w2 + offsetY),
                        p.Origin + p.XAxis * (offset + offsetX) - p.YAxis * (w2 - offsetY)
                        ).ToNurbsCurve();
                else
                    rules[i] = new Line(
                        p.Origin + p.YAxis * (offset + offsetY) + p.XAxis * (w2 + offsetX),
                        p.Origin + p.YAxis * (offset + offsetY) - p.XAxis * (w2 - offsetX)
                        ).ToNurbsCurve();

            }

            Brep[] loft = Brep.CreateFromLoft(rules, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
            if (loft == null || loft.Length < 1) throw new Exception("Glulam::GetSideSurface::Loft failed!");

            Brep brep = loft[0];

            Point3d pt = brep.Faces[0].PointAt(brep.Faces[0].Domain(0).Mid, brep.Faces[0].Domain(1).Mid);
            Vector3d nor = brep.Faces[0].NormalAt(brep.Faces[0].Domain(0).Mid, brep.Faces[0].Domain(1).Mid);

            double ct;
            Centreline.ClosestPoint(pt, out ct);
            Vector3d nor2 = Centreline.PointAt(ct) - pt;
            nor2.Unitize();

            if (nor2 * nor < 0.0)
            {
                brep.Flip();
            }

            if (flip)
                brep.Flip();

            return brep;
        }
    }
}