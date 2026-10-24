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

using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;

using GluLamb.Joints;

namespace GluLamb.Blanks
{
    /// <summary>
    /// A group of near-identical beams that can be made from one blank.
    /// </summary>
    public class BeamCluster
    {
        /// <summary>
        /// The blank, in the position of the reference member: the reference's centreline,
        /// extended to cover every member (and its extensions), and a constant section
        /// big enough for every member.
        /// </summary>
        public Glulam Blank;

        /// <summary>
        /// Index of the reference member (the longest).
        /// </summary>
        public int Reference;

        /// <summary>
        /// Indices of the members, reference first.
        /// </summary>
        public List<int> Members = new List<int>();

        /// <summary>
        /// How far the blank extends the reference's centreline at its start and end.
        /// </summary>
        public BeamExtension Extension;
    }

    public class BeamClusterResult
    {
        public List<BeamCluster> Clusters = new List<BeamCluster>();

        /// <summary>
        /// Per input beam: the cluster (blank) it is made from, or -1 for a beam that couldn't be sampled.
        /// </summary>
        public int[] BlankIndex;

        /// <summary>
        /// Per input beam: transform from its blank to the beam.
        /// </summary>
        public Transform[] Transforms;

        /// <summary>
        /// Per input beam: the largest point deviation from its blank's reference after alignment.
        /// </summary>
        public double[] Deviation;

        /// <summary>
        /// Per input beam: whether it is a shorter beam cut from a longer blank.
        /// </summary>
        public bool[] Partial;

        /// <summary>
        /// Per input beam: a copy of its blank moved onto it (keeping the blank's id, so beams
        /// that share a blank have copies with the same id), or null for a beam without a blank.
        /// </summary>
        public Glulam[] PlacedBlanks;

        /// <summary>
        /// Number of pairs that were aligned (after pruning), for diagnostics.
        /// </summary>
        public long Alignments;

        /// <summary>
        /// How long clustering took.
        /// </summary>
        public TimeSpan Elapsed;

        /// <summary>
        /// Number of beams that couldn't be sampled (no centreline or orientation, or zero length)
        /// and so have no blank.
        /// </summary>
        public int Skipped => BlankIndex.Count(x => x < 0);

        public override string ToString() => $"{BlankIndex.Length - Skipped} beams -> {Clusters.Count} blanks";
    }
}
