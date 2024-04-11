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
using Rhino.Collections;

using GluLamb.Standards;

namespace GluLamb
{
    [Serializable]
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
        public static int MaxNumWidth = 40;
        public static int MaxNumHeight = 40;

        /// <summary>
        /// Number of laminations in the width (X) direction.
        /// </summary>
        public int NumWidth { get { return Lamellae.GetLength(0); } }

        /// <summary>
        /// Number of laminations in the height (Y) direction.
        /// </summary>
        public int NumHeight { get { return Lamellae.GetLength(1); } }

        /// <summary>
        /// Width of the laminations.
        /// </summary>
        public double LamWidth;
        /// <summary>
        /// Thickness of the laminations
        /// </summary>
        public double LamHeight;
        public int Samples;

        public Interpolation InterpolationType = Interpolation.LINEAR;
        public CrossSectionPosition SectionAlignment = CrossSectionPosition.MiddleCentre;

        /// <summary>
        /// Reference to the individual laminations.
        /// </summary>
        public Stick[,] Lamellae;

        public static GlulamData Default
        { get { return new GlulamData(); } }

        public GlulamData(int num_width = 4, int num_height = 4, 
            double lam_width = 20.0, double lam_height = 20.0, 
            int samples = 50, CrossSectionPosition alignment = CrossSectionPosition.MiddleCentre)
        {
            Lamellae = new Stick[num_width, num_height];

            LamWidth = lam_width;
            LamHeight = lam_height;
            Samples = samples;
            SectionAlignment = alignment;
        }
        
        public GlulamData(Beam bb, double width, double height, Standard standard = Standard.Eurocode, int curve_samples = 0, int glulam_samples = 50)
        {
            Compute(bb, standard, curve_samples);
            LamWidth = width / NumWidth;
            LamHeight = height / NumHeight;
            Samples = glulam_samples;
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

        public void Compute(Beam beam, Standard standard = Standard.Eurocode, int k_samples = 100)
        {
            // Store the beam dimensions because setting the beam values changes
            // the dimensions
            double beamWidth = beam.Width;
            double beamHeight = beam.Height;

            if (beamWidth <= 0.0 || beamHeight <= 0.0)
                throw new Exception("Beam dimensions cannot be 0.");

            double tempLamWidth, tempLamHeight;

            double maxKX = 0.0;
            double maxKY = 0.0;

            if (!beam.Centreline.IsLinear())
            {
                if (k_samples < 3) k_samples = DefaultCurvatureSamples;

                double[] tt = beam.Centreline.DivideByCount(k_samples, false);

                double maxK = 0.0;
                int index = 0;
                Vector3d kvec = Vector3d.Unset;
                Vector3d tVec;


                double dotKX, dotKY;
                Plane tPlane;

                for (int i = 0; i < tt.Length; ++i)
                {
                    tVec = beam.Centreline.CurvatureAt(tt[i]);
                    tPlane = beam.GetPlane(tt[i]);

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

                // Decrease the radius of curvature by half of the beam
                // dimensions to get the curvature at the inner face.
                // Could be made redundant if CalculateLaminationThickness
                // used radius of curvature instead.
                maxKX = 1 / ((1 / maxKX) - beamWidth / 2);
                maxKY = 1 / ((1 / maxKY) - beamHeight / 2);
            }

            switch (standard)
            {
                case (Standard.Eurocode):
                    tempLamWidth = Eurocode.Instance.CalculateLaminationThickness(maxKX);
                    tempLamHeight = Eurocode.Instance.CalculateLaminationThickness(maxKY);
                    break;
                case (Standard.APA):
                    tempLamWidth = ANSI.Instance.CalculateLaminationThickness(maxKX);
                    tempLamHeight = ANSI.Instance.CalculateLaminationThickness(maxKY);
                    break;
                case (Standard.CSA):
                    tempLamWidth = CSA.Instance.CalculateLaminationThickness(maxKX);
                    tempLamHeight = CSA.Instance.CalculateLaminationThickness(maxKY);
                    break;
                default:
                    tempLamWidth = NoStandard.Instance.CalculateLaminationThickness(maxKX);
                    tempLamHeight = NoStandard.Instance.CalculateLaminationThickness(maxKY);
                    break;
            }

            // Check that the lamination dimensions don't exceed the total dimensions
            tempLamWidth = Math.Min(tempLamWidth, beamWidth);
            tempLamHeight = Math.Min(tempLamHeight, beamHeight);

            int tempNumWidth = Math.Min(MaxNumWidth, (int)Math.Ceiling(beamWidth / tempLamWidth));
            int tempNumHeight = Math.Min(MaxNumHeight, (int)Math.Ceiling(beamHeight / tempLamHeight));

            // Recalculate number of laminations needed to make up the total dimensions
            // Lamellae.ResizeArray(tempNumWidth, tempNumHeight);
            Lamellae = new Stick[tempNumWidth, tempNumHeight];

            // Resize the laminations so that they add up to the total dimensions
            LamWidth = beamWidth / tempNumWidth;
            LamHeight = beamHeight / tempNumHeight;

            // After this you would run it through AdjustLaminationDimensions(GluLamb.Factory.LamellaFactory factory) to constrain the lamella dimensions
            // to available sizes. 
        }

        public void AdjustLaminationDimensions(GluLamb.Factory.LamellaFactory factory)
        {
            // Get current dimensions of cross-section
            double width = LamWidth * NumWidth;
            double height = LamHeight * NumHeight;

            // Get closest available lamination dimensions
            double lamination_width = factory.GetWidth(LamWidth);
            double lamination_height = factory.GetHeight(LamHeight);

            int tempNumWidth = Math.Min(MaxNumWidth, (int)Math.Ceiling(width / lamination_width));
            int tempNumHeight = Math.Min(MaxNumHeight, (int)Math.Ceiling(height / lamination_height));

            // Adjust number of laminations to cover current cross-section dimensions
            //Lamellae.ResizeArray((int)Math.Ceiling(width / lamination_width), (int)Math.Ceiling(height / lamination_height));
            Lamellae = new Stick[tempNumWidth, tempNumHeight];
            LamWidth = lamination_width;
            LamHeight = lamination_height;
        }

        public string Species
        {
            get
            {
                bool init = false;
                string species = "Homogeneous";

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

        public override string ToString()
        {
            return $"GlulamData [ lw {LamWidth} lh {LamHeight} nw {NumHeight} nh {NumHeight} s {Samples}]";
        }


#if OBSOLETE

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
#endif
    }
}
