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

        /// <summary>
        /// Whether the lamella count hit MaxNumWidth or MaxNumHeight (a guard against huge counts
        /// from a tiny or bad curve): in Compute, the lamellae are then thicker than the standard
        /// allows for the curvature; in AdjustLaminationDimensions, they don't make up the whole section.
        /// </summary>
        public bool LamellaCountCapped;

        /// <summary>
        /// Strength class of the glulam, e.g. GL24h or GL28c (empty: not given).
        /// </summary>
        public string StrengthClass = "";

        /// <summary>
        /// Extra material for machining.
        /// </summary>
        public MachiningAllowance Allowance;

        /// <summary>
        /// How the lamella grades are laid up (null: lamellae keep whatever grades they have). Setting
        /// it fills the grades, and they are filled again whenever the lamella count changes.
        /// </summary>
        public Layup Layup
        {
            get => m_layup;
            set { m_layup = value; Populate(); }
        }
        Layup m_layup;

        /// <summary>
        /// Fill empty lamellae with default ones and apply the layup.
        /// </summary>
        void Populate()
        {
            if (Lamellae == null) return;
            for (int i = 0; i < Lamellae.GetLength(0); ++i)
                for (int j = 0; j < Lamellae.GetLength(1); ++j)
                    if (Lamellae[i, j] == null) Lamellae[i, j] = new Stick();
            m_layup?.Apply(this);
        }

        /// <summary>
        /// Set the species of every lamella, keeping their grades.
        /// </summary>
        public void SetSpecies(string species)
        {
            if (string.IsNullOrWhiteSpace(species)) return;
            Populate();
            foreach (Stick lamella in Lamellae)
                lamella.Species = species.Trim();
        }

        /// <summary>
        /// The lamellae's grade if they are all the same, "Mixed" if not, or empty if none is given.
        /// </summary>
        public string Grade
        {
            get
            {
                var grades = Lamellae.Cast<Stick>().Where(x => x != null).Select(x => x.Grade ?? "").Distinct().ToList();
                return grades.Count == 1 ? grades[0] : grades.Count == 0 ? "" : "Mixed";
            }
        }

        /// <summary>
        /// The largest lamella width and height the standard allows for the curvature, from Compute
        /// (0: not computed).
        /// </summary>
        public double MaxLamWidth, MaxLamHeight;

        /// <summary>
        /// The smallest lamella dimension, in model units (default 5 mm). Compute doesn't go below it
        /// (the lamellae are then thicker than the standard allows), and FitSection rejects thinner lamellae.
        /// </summary>
        public static double MinLamellaSize
        {
            get => m_minLamellaSize ?? Globals.FromMillimeters(5.0);
            set => m_minLamellaSize = value;
        }
        static double? m_minLamellaSize;

        /// <summary>
        /// Whether the lamellae are thicker than the standard allows for the curvature (see MaxLamWidth, MaxLamHeight).
        /// </summary>
        public bool ExceedsStandard =>
            (MaxLamWidth > 0 && LamWidth > MaxLamWidth * (1 + 1e-9)) || (MaxLamHeight > 0 && LamHeight > MaxLamHeight * (1 + 1e-9));

        /// <summary>
        /// Problems with the lamellae, as messages: the count cap, thicker than the standard allows,
        /// thinner than the minimum.
        /// </summary>
        public List<string> Warnings()
        {
            var warnings = new List<string>();
            if (LamellaCountCapped)
                warnings.Add($"The lamella count is capped at {MaxNumWidth} x {MaxNumHeight} (a guard against a tiny or bad curve).");
            if (ExceedsStandard)
                warnings.Add($"Lamellae {LamWidth:0.###} x {LamHeight:0.###} are thicker than the standard allows for the curvature " +
                    $"({(MaxLamWidth > 0 ? MaxLamWidth.ToString("0.###") : "-")} x {(MaxLamHeight > 0 ? MaxLamHeight.ToString("0.###") : "-")}).");
            if (LamWidth < MinLamellaSize || LamHeight < MinLamellaSize)
                warnings.Add($"Lamellae {LamWidth:0.###} x {LamHeight:0.###} are thinner than the minimum {MinLamellaSize:0.###} (GlulamData.MinLamellaSize).");
            return warnings;
        }

        /// <summary>
        /// Make the section at least width x height from whole lamellae of the current lamella size:
        /// the lamella counts are rounded up (and capped at MaxNumWidth, MaxNumHeight). Lamellae keep
        /// their data where they still exist; new ones copy their nearest neighbour.
        /// </summary>
        /// <returns>The section's width and height.</returns>
        public (double Width, double Height) FitSection(double width, double height)
        {
            if (!(LamWidth >= MinLamellaSize) || !(LamHeight >= MinLamellaSize))
                throw new ArgumentOutOfRangeException(nameof(LamWidth),
                    $"Lamellae {LamWidth:0.###} x {LamHeight:0.###} are thinner than the minimum {MinLamellaSize:0.###} (GlulamData.MinLamellaSize).");

            // A hair under a whole lamella doesn't add another
            int needWidth = Math.Max(1, (int)Math.Ceiling(width / LamWidth - 1e-9));
            int needHeight = Math.Max(1, (int)Math.Ceiling(height / LamHeight - 1e-9));
            LamellaCountCapped = needWidth > MaxNumWidth || needHeight > MaxNumHeight;
            Resize(Math.Min(MaxNumWidth, needWidth), Math.Min(MaxNumHeight, needHeight));

            return (LamWidth * NumWidth, LamHeight * NumHeight);
        }

        /// <summary>
        /// Change the lamella counts, keeping the lamellae that still exist; new ones copy their nearest neighbour.
        /// </summary>
        void Resize(int numWidth, int numHeight)
        {
            var old = Lamellae;
            Lamellae = new Stick[numWidth, numHeight];

            if (old != null && old.Length > 0)
                for (int i = 0; i < numWidth; ++i)
                    for (int j = 0; j < numHeight; ++j)
                        Lamellae[i, j] = old[Math.Min(i, old.GetLength(0) - 1), Math.Min(j, old.GetLength(1) - 1)]?.Duplicate();
            Populate();
        }

        public static GlulamData Default
        { get { return new GlulamData(); } }

        public GlulamData(int num_width = 4, int num_height = 4, 
            double lam_width = 20.0, double lam_height = 20.0, 
            int samples = 50, CrossSectionPosition alignment = CrossSectionPosition.MiddleCentre)
        {
            Lamellae = new Stick[num_width, num_height];
            Populate();

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
            data.LamellaCountCapped = LamellaCountCapped;
            data.MaxLamWidth = MaxLamWidth;
            data.MaxLamHeight = MaxLamHeight;
            data.StrengthClass = StrengthClass;
            data.Allowance = Allowance;
            data.m_layup = m_layup;
            // New lamellae, so that changing one copy's species doesn't change the other's
            data.Lamellae = new Stick[NumWidth, NumHeight];
            for (int i = 0; i < NumWidth; ++i)
                for (int j = 0; j < NumHeight; ++j)
                    data.Lamellae[i, j] = Lamellae[i, j]?.Duplicate();

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

            // What the standard allows; lamellae aren't made thinner than the minimum
            MaxLamWidth = tempLamWidth;
            MaxLamHeight = tempLamHeight;
            tempLamWidth = Math.Max(tempLamWidth, MinLamellaSize);
            tempLamHeight = Math.Max(tempLamHeight, MinLamellaSize);

            // Check that the lamination dimensions don't exceed the total dimensions
            tempLamWidth = Math.Min(tempLamWidth, beamWidth);
            tempLamHeight = Math.Min(tempLamHeight, beamHeight);

            int needWidth = (int)Math.Ceiling(beamWidth / tempLamWidth);
            int needHeight = (int)Math.Ceiling(beamHeight / tempLamHeight);
            int tempNumWidth = Math.Min(MaxNumWidth, needWidth);
            int tempNumHeight = Math.Min(MaxNumHeight, needHeight);
            LamellaCountCapped = needWidth > MaxNumWidth || needHeight > MaxNumHeight;

            // Recalculate number of laminations needed to make up the total dimensions
            Resize(tempNumWidth, tempNumHeight);

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

            int needWidth = (int)Math.Ceiling(width / lamination_width);
            int needHeight = (int)Math.Ceiling(height / lamination_height);
            int tempNumWidth = Math.Min(MaxNumWidth, needWidth);
            int tempNumHeight = Math.Min(MaxNumHeight, needHeight);
            LamellaCountCapped = needWidth > MaxNumWidth || needHeight > MaxNumHeight;

            // Adjust number of laminations to cover current cross-section dimensions
            Resize(tempNumWidth, tempNumHeight);
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
            return $"GlulamData [ lw {LamWidth} lh {LamHeight} nw {NumWidth} nh {NumHeight} s {Samples}" +
                (string.IsNullOrEmpty(StrengthClass) ? "" : $" {StrengthClass}") + (m_layup == null ? "" : $" {m_layup}") + "]";
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
