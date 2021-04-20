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

using Rhino.Geometry;

namespace GluLamb.Factory
{
    /*
    public abstract class LamellaFactory<T> where T : class, new()
    {
        protected LamellaFactory() { }
        public static T instance;

        public static T Instance
        {
            get
            {
                if (instance == null)
                    instance = new T();
                return instance;
            }
        }
        public abstract double GetWidth(double width);
        public abstract double GetHeight(double height);
    }
    */
    
    /// <summary>
    /// Abstract class for determining actual lamella size from the required size
    /// </summary>
    public abstract class LamellaFactory
    {
        public abstract double GetWidth(double width, bool force = false);
        public abstract double GetHeight(double height, bool force = false);
    }
    

    /// <summary>
    /// LamellaFactory with no constraints on the lamella size. Returns whatever is
    /// put in.
    /// </summary>
    public class FreeLamella: LamellaFactory
    {
        public FreeLamella()
        {

        }
        public override double GetHeight(double height, bool force = false)
        {
            return height;
        }
        public override double GetWidth(double width, bool force = false)
        {
            return width;
        }
    }

    /// <summary>
    /// LamellaFactory that returns the closest smaller size from a list of 
    /// available widths and heights
    /// </summary>
    public class DimensionedLamella1 : LamellaFactory
    {
        List<double> Widths;
        List<double> Heights;

        public DimensionedLamella1() 
        {
            Widths = new List<double>();
            Heights = new List<double>();
        }

        public DimensionedLamella1(List<double> widths, List<double> heights)
        {
            Widths = new List<double>(widths);
            Heights = new List<double>(heights);
            Widths.Sort();
            Heights.Sort();

        }
        public override double GetHeight(double height, bool force = false)
        {
            int i = Heights.BinarySearch(height);
            if (i < 0)
            {
                i = ~i;
                i--;
            }
            if (i == 0 && height < Heights[0] && force)
                throw new Exception("DimensionedLamella: Lamella is too thin (height) for available sizes.");
            return Heights[Math.Max(0, i)];
        }
        public override double GetWidth(double width, bool force=false)
        {
            int i = Widths.BinarySearch(width);
            if (i < 0)
            {
                i = ~i;
                i--;
            }
            if (i == 0 && width < Widths[0] && force)
                throw new Exception("DimensionedLamella: Lamella is too thin (width) for available sizes.");
            return Widths[Math.Max(0, i)];
        }
    }
    

    public class BlankFactory
    {
        // Data for controlling the behaviour of BlankFactory
        // i.e. a range of lamella sizes to choose from

        public BlankFactory()
        {

        }

        public Glulam GlulamFromCurveMesh(Curve crv, Mesh mesh, GlulamType type = GlulamType.DoubleCurved, Standards.Standard standard = Standards.Standard.None, double tolerance=10.0)
        {
            double width, height;
            GlulamData data;
            Plane xform = new Plane(crv.PointAtStart, crv.PointAtEnd - crv.PointAtStart);
            Polyline convex_hull;

            if (crv.IsLinear()) type = GlulamType.Straight;
            else if (crv.IsPlanar()) type = GlulamType.SingleCurved;

            Beam beam;

            switch (type)
            {
                case GlulamType.Straight:
                    var line_curve = new Line(crv.PointAtStart, crv.PointAtEnd);
                    Plane plane = xform;
                    mesh.FitToAxes(plane, out convex_hull, ref xform);

                    height = convex_hull.BoundingBox.Max.Y - convex_hull.BoundingBox.Min.Y;
                    width = convex_hull.BoundingBox.Max.X - convex_hull.BoundingBox.Min.X;

                    data = new GlulamData(1, 1, width, height);

                    var orientation = new VectorOrientation(xform.YAxis);

                    return Glulam.CreateGlulam(line_curve.ToNurbsCurve(), orientation, data);

                case GlulamType.SingleCurved:
                    crv.TryGetPlane(out Plane project, tolerance);
                    mesh.FitToAxes(project, out convex_hull, ref xform);

                    var cbb = crv.GetBoundingBox(project);
                    height = (convex_hull.BoundingBox.Max.Y - convex_hull.BoundingBox.Min.Y) - (cbb.Max.Y - cbb.Min.Y);
                    width = (convex_hull.BoundingBox.Max.X - convex_hull.BoundingBox.Min.X) - (cbb.Max.X - cbb.Min.X);

                    beam = new Beam { Centreline=crv, Orientation=new PlanarOrientation(project), Width=width, Height=height };
                    return Glulam.CreateGlulam(beam, beam.Orientation, standard);

                case GlulamType.DoubleCurved:
                default:
                    beam = new Beam { Centreline = crv, Orientation = new RmfOrientation()};
                    var gmesh = beam.ToBeamSpace(mesh);

                    var bb = gmesh.GetBoundingBox(true);
                    beam.Height = bb.Max.Y - bb.Min.Y;
                    beam.Width = bb.Max.X - bb.Min.X;

                    return Glulam.CreateGlulam(beam, beam.Orientation, standard);

            }

        }

    }
}
