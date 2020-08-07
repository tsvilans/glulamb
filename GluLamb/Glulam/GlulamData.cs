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
    public class GlulamData
    {
        public enum Interpolation
        {
            LINEAR = 0,
            HERMITE = 1,
            CUBIC = 2
        }

        public enum CrossSectionPosition
        {
            TopLeft,
            TopCentre,
            TopRight,
            MiddleLeft,
            MiddleCentre,
            MiddleRight,
            BottomLeft,
            BottomCentre,
            BottomRight
        }

        public static double DefaultWidth = 80.0;
        public static double DefaultHeight = 80.0;
        public static double DefaultSampleDistance = 50.0;
        public static int DefaultCurvatureSamples = 40;

        public int NumWidth { get { return Lamellae.GetLength(0); } }
        public int NumHeight { get { return Lamellae.GetLength(1); } }
        public double LamWidth, LamHeight;
        public int Samples;
        public Interpolation InterpolationType = Interpolation.LINEAR;
        public CrossSectionPosition SectionAlignment = CrossSectionPosition.MiddleCentre;
        public Stick[,] Lamellae;


        public static GlulamData Default
        { get { return new GlulamData(); } }


        public GlulamData(int num_width = 4, int num_height = 4, double lam_width = 20.0, double lam_height = 20.0, int samples = 50, CrossSectionPosition alignment = CrossSectionPosition.MiddleCentre)
        {
            Lamellae = new Stick[num_width, num_height];

            LamWidth = lam_width;
            LamHeight = lam_height;
            Samples = samples;
            SectionAlignment = alignment;
        }

        
        public GlulamData(BeamBase bb, double width, double height, int glulam_samples = 50, int curve_samples = 0)
        {
            GlulamData.GetLamellaSizes(bb, out double lw, out double lh, curve_samples);

            lw = Math.Min(lw, width);
            lh = Math.Min(lh, height);

            int num_width = (int)Math.Ceiling(width / lw);
            int num_height = (int)Math.Ceiling(height / lh);

            Lamellae = new Stick[num_width, num_height];

            LamWidth = width / num_width;
            LamHeight = height / num_height;

            Samples = glulam_samples;
        }
        

        
        public static GlulamData FromBeam(BeamBase bb, double width, double height, int k_samples = 0)
        {
            return new GlulamData(bb, width, height, (int)(bb.Centreline.GetLength() / DefaultSampleDistance), k_samples);
        }


#if OBSOLETE
        public static GlulamData FromCurveLimits(Curve c, double width, double height, Plane[] frames = null)
        {
            Beam beam = new Lam.Beam(c, null, frames);

            double[] tt = beam.Centreline.DivideByCount(100, true);
            double maxK = 0.0;
            int index = 0;
            Vector3d kvec = Vector3d.Unset;
            Vector3d temp;

            for (int i = 0; i < tt.Length; ++i)
            {
                temp = beam.Centreline.CurvatureAt(tt[i]);
                if (temp.Length > maxK)
                {
                    index = i;
                    kvec = temp;
                    maxK = temp.Length;
                }
            }
            Plane frame = beam.GetPlane(tt[index]);

            if (frame == null)
                throw new Exception("Frame is null!");

            //double max_lam_width = Math.Ceiling(width);
            //double max_lam_height = Math.Ceiling(height);

            double max_lam_width = width;
            double max_lam_height = height;

            //double lam_width = Math.Ceiling(Math.Min(1 / (Math.Abs(kvec * frame.XAxis) * Glulam.RadiusMultiplier), max_lam_width));
            //double lam_height = Math.Ceiling(Math.Min(1 / (Math.Abs(kvec * frame.YAxis) * Glulam.RadiusMultiplier), max_lam_height));

            double lam_width = Math.Min(1 / (Math.Abs(kvec * frame.XAxis) * Glulam.RadiusMultiplier), max_lam_width);
            double lam_height = Math.Min(1 / (Math.Abs(kvec * frame.YAxis) * Glulam.RadiusMultiplier), max_lam_height);

            if (lam_width == 0.0) lam_width = max_lam_width;
            if (lam_height == 0.0) lam_height = max_lam_height;

            GlulamData data = new GlulamData();

            data.LamHeight = lam_height;
            data.LamWidth = lam_width;
            int num_height = (int)(Math.Ceiling(height / lam_height));
            int num_width = (int)(Math.Ceiling(width / lam_width));

            data.Lamellae = new Stick[num_width, num_height];

            data.Samples = (int)(c.GetLength() / DefaultSampleDistance);

            // I forget why this is here... 
            if (data.NumHeight * data.LamHeight - height > 20.0)
                data.LamHeight = Math.Ceiling((height + 10.0) / data.NumHeight);
            if (data.NumWidth * data.LamWidth - width > 20.0)
                data.LamWidth = Math.Ceiling((width + 10.0) / data.NumWidth);

            return data;
        }
#endif

        /// <summary>
        /// Get lamella widths and heights from input curve and cross-section guide frames.
        /// </summary>
        /// <param name="c">Centreline curve.</param>
        /// <param name="lamella_width">Maximum lamella width.</param>
        /// <param name="lamella_height">Maximum lamella height</param>
        /// <param name="frames">Guide frames.</param>
        /// <param name="k_samples">Number of curvature samples to use.</param>
        /// <returns>A pair of doubles for maximum curvature in X and Y directions.</returns>
        public static double[] GetLamellaSizes(BeamBase bb, out double lamella_width, out double lamella_height, int k_samples = 0)
        {
            if (bb.Centreline.IsLinear())
            {
                lamella_width = double.MaxValue;
                lamella_height = double.MaxValue;
                return new double[] { 0, 0 };
            }

            if (k_samples < 3) k_samples = DefaultCurvatureSamples;

            double[] tt = bb.Centreline.DivideByCount(k_samples, false);

            double maxK = 0.0;
            int index = 0;
            Vector3d kvec = Vector3d.Unset;
            Vector3d tVec;

            double maxKX = 0.0;
            double maxKY = 0.0;
            double dotKX, dotKY;
            Plane tPlane;

            for (int i = 0; i < tt.Length; ++i)
            {
                tVec = bb.Centreline.CurvatureAt(tt[i]);
                tPlane = bb.GetPlane(tt[i]);

                dotKX = Math.Abs(tVec * tPlane.XAxis);
                dotKY = Math.Abs(tVec * tPlane.YAxis);

                maxKX = Math.Max(dotKX, maxKX);
                maxKY = Math.Max(dotKY, maxKY);

                if (tVec.Length > maxK)
                {
                    index = i;
                    kvec = tVec;
                    maxK = tVec.Length;
                }
            }

            if (maxKX == 0.0)
                lamella_width = double.MaxValue;
            else
                lamella_width = 1 / maxKX / Glulam.RadiusMultiplier;

            if (maxKY == 0.0)
                lamella_height = double.MaxValue;
            else
                lamella_height = 1 / maxKY / Glulam.RadiusMultiplier;

            return new double[] { maxKX, maxKY };

        }
        public string Species
        {
            get
            {
                bool init = false;
                string species = "None";

                foreach (Stick lamella in Lamellae)
                {
                    if (lamella == null) continue;
                    if (!init)
                    {
                        species = lamella.Species;
                        init = true;
                    }
                    else if (lamella.Species != species)
                        return "Composite";
                }
                return species;
            }
        }

        public List<string> AllSpecies
        {
            get
            {
                HashSet<string> species = new HashSet<string>();
                foreach (Stick lamella in Lamellae)
                {
                    if (lamella == null) continue;
                    species.Add(lamella.Species);
                }

                return species.ToList();
            }
        }

        public void PopulateLamellae(string species)
        {
            for (int i = 0; i < Lamellae.GetLength(0); ++i)
            {
                for (int j = 0; j < Lamellae.GetLength(1); ++j)
                {
                    Lamellae[i, j] = new Stick(species);
                }
            }
        }

        public GlulamData Duplicate()
        {
            GlulamData data = new GlulamData();
            data.LamHeight = LamHeight;
            data.LamWidth = LamWidth;
            data.Samples = Samples;
            data.SectionAlignment = SectionAlignment;
            data.InterpolationType = InterpolationType;
            data.Lamellae = new Stick[NumWidth, NumHeight];
            Array.Copy(Lamellae, data.Lamellae, Lamellae.Length);

            return data;
        }

        public byte[] ToByteArray()
        {
            List<byte> b = new List<byte>();
            b.AddRange(BitConverter.GetBytes((Int32)NumHeight));
            b.AddRange(BitConverter.GetBytes((Int32)NumWidth));
            b.AddRange(BitConverter.GetBytes(LamHeight));
            b.AddRange(BitConverter.GetBytes(LamWidth));
            b.AddRange(BitConverter.GetBytes((Int32)Samples));
            b.AddRange(BitConverter.GetBytes((Int32)SectionAlignment));
            b.AddRange(BitConverter.GetBytes((Int32)InterpolationType));

            return b.ToArray();
        }

        public static GlulamData FromByteArray(byte[] b)
        {
            if (b.Length != 28)
                throw new Exception("Byte array is wrong size for GlulamData!");

            GlulamData data = new GlulamData();
            int num_height = BitConverter.ToInt32(b, 0);
            int num_width = BitConverter.ToInt32(b, 4);
            data.LamHeight = BitConverter.ToDouble(b, 8);
            data.LamWidth = BitConverter.ToDouble(b, 16);
            data.Samples = BitConverter.ToInt32(b, 24);
            data.SectionAlignment = (GlulamData.CrossSectionPosition)BitConverter.ToInt32(b, 32);
            data.InterpolationType = (GlulamData.Interpolation)BitConverter.ToInt32(b, 40);
            data.Lamellae = new Stick[num_width, num_height];

            return data;
        }
    }

}
