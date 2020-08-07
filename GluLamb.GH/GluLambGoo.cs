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
using System.Linq;
using System.Collections.Generic;
using Rhino.Geometry;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using System.Drawing;
using GH_IO.Serialization;
using GH_IO;

namespace GluLamb.GH
{

    public class GH_Glulam : GH_Goo<Glulam>//, IGH_PreviewData, GH_ISerializable
    {
        #region Members
        //protected Mesh DisplayMesh = null;
        #endregion

        #region Constructors
        public GH_Glulam() : this(null) { }
        //public GH_Glulam(GH_Glulam goo) { this.Value = goo.Value; this.DisplayMesh = goo.DisplayMesh.DuplicateMesh(); }
        //public GH_Glulam(Glulam native) { this.Value = native; this.DisplayMesh = native.GetBoundingMesh(0, Value.Data.InterpolationType); }
        public GH_Glulam(Glulam native) { this.Value = native; }

        public override IGH_Goo Duplicate()
        {
            if (Value == null)
                return new GH_Glulam();
            else
                return new GH_Glulam(Value.Duplicate());
        }
        #endregion

        public static Glulam ParseGlulam(object obj)
        {
            if (obj is GH_Glulam)
                return (obj as GH_Glulam).Value;
            else
                return obj as Glulam;
        }
        public override string ToString()
        {
            if (Value == null) return "Null glulam";
            return Value.ToString();
        }

        public override string TypeName => "GlulamGoo";
        public override string TypeDescription => "GlulamGoo";
        public override object ScriptVariable() => Value;
        //public BoundingBox ClippingBox => DisplayMesh.GetBoundingBox(true);

        public override bool IsValid
        {
            get
            {
                if (Value == null) return false;
                return true;
            }
        }
        public override string IsValidWhyNot
        {
            get
            {
                if (Value == null) return "No data";
                return string.Empty;
            }
        }

        #region Casting
        public override bool CastFrom(object source)
        {
            if (source == null) return false;
            if (source is Glulam glulam)
            {
                Value = glulam;
                //DisplayMesh = Value.GetBoundingMesh(0, Value.Data.InterpolationType);
                return true;
            }
            if (source is GH_Glulam ghGlulam)
            {
                Value = ghGlulam.Value;
                //DisplayMesh = ghGlulam.DisplayMesh;
                return true;
            }
            return false;
        }

        //public static implicit operator Glulam(GH_Glulam g) => g.Value;
        //public static implicit operator GH_Glulam(Glulam g) => new GH_Glulam(g);

        public override bool CastTo<Q>(ref Q target)
        {
            if (Value == null) return false;

            if (typeof(Q).IsAssignableFrom(typeof(GH_Mesh)))
            {
                object mesh = new GH_Mesh(Value.ToMesh());

                target = (Q)mesh;
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(GH_Brep)))
            {
                object blank = new GH_Brep(Value.ToBrep());

                target = (Q)blank;
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(GH_Curve)))
            {
                object cl = new GH_Curve(Value.Centreline);
                target = (Q)cl;
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(Glulam)))
            {
                object blank = Value;
                target = (Q)blank;
                return true;
            }

            return false;
        }

        #endregion
        /*
        public void DrawViewportMeshes(GH_PreviewMeshArgs args)
        {
            if (DisplayMesh != null)
                args.Pipeline.DrawMeshShaded(DisplayMesh, args.Material);
        }

        public void DrawViewportWires(GH_PreviewWireArgs args)
        {
            if (DisplayMesh != null)
                args.Pipeline.DrawMeshWires(DisplayMesh, args.Color);
        }
        */
        #region Serialization

        public override bool Write(GH_IWriter writer)
        {
            if (Value == null) return false;
            byte[] centrelineBytes = GH_Convert.CommonObjectToByteArray(Value.Centreline);
            writer.SetByteArray("guide", centrelineBytes);
            writer.SetInt32("lcx", Value.Data.NumWidth);
            writer.SetInt32("lcy", Value.Data.NumHeight);
            writer.SetDouble("lsx", Value.Data.LamWidth);
            writer.SetDouble("lsy", Value.Data.LamHeight);
            writer.SetInt32("interpolation", (int)Value.Data.InterpolationType);
            writer.SetInt32("samples", Value.Data.Samples);


            return true;
        }

        public override bool Read(GH_IReader reader)
        {
            if (!reader.ItemExists("guide"))
            {
                Value = null;
                throw new Exception("Couldn't retrieve 'guide'.");
            }

            byte[] rawGuide = reader.GetByteArray("guide");

            Curve guide = GH_Convert.ByteArrayToCommonObject<Curve>(rawGuide);
            if (guide == null)
                throw new Exception("Failed to convert 'guide'.");

            int N = reader.GetInt32("num_frames");
            Plane[] frames = new Plane[N];

            for (int i = 0; i < N; ++i)
            {
                var gp = reader.GetPlane("frames", i);
                frames[i] = new Plane(
                    new Point3d(
                        gp.Origin.x,
                        gp.Origin.y,
                        gp.Origin.z),
                    new Vector3d(
                        gp.XAxis.x,
                        gp.XAxis.y,
                        gp.XAxis.z),
                    new Vector3d(
                        gp.YAxis.x,
                        gp.YAxis.y,
                        gp.YAxis.z)
                        );
            }

            int lcx = reader.GetInt32("lcx");
            int lcy = reader.GetInt32("lcy");
            double lsx = reader.GetDouble("lsx");
            double lsy = reader.GetDouble("lsy");
            int interpolation = reader.GetInt32("interpolation");
            int samples = reader.GetInt32("samples");

            GlulamData data = new GlulamData(lcx, lcy, lsx, lsy, samples);
            data.InterpolationType = (GlulamData.Interpolation)interpolation;

            Value = Glulam.CreateGlulam(guide, null , data);

            if (Value == null)
                throw new Exception("What in the...");

            return true;
        }
        #endregion

    }

    public class GH_GlulamData : GH_Goo<GlulamData>
    {
        public GH_GlulamData() { this.Value = null; }
        public GH_GlulamData(GH_GlulamData goo) { this.Value = goo.Value; }
        public GH_GlulamData(GlulamData native) { this.Value = native; }
        public override IGH_Goo Duplicate() => new GH_GlulamData(this);
        public override bool IsValid => true;
        public override string TypeName => "GlulamDataGoo";
        public override string TypeDescription => "GlulamDataGoo";
        public override string ToString() => Value.ToString();
        public override object ScriptVariable() => Value;

        #region Serialization

        public override bool Write(GH_IWriter writer)
        {
            if (Value == null) return false;

            writer.SetInt32("lcx", Value.NumWidth);
            writer.SetInt32("lcy", Value.NumHeight);
            writer.SetDouble("lsx", Value.LamWidth);
            writer.SetDouble("lsy", Value.LamHeight);
            writer.SetInt32("interpolation", (int)Value.InterpolationType);
            writer.SetInt32("samples", Value.Samples);

            return true;
        }

        public override bool Read(GH_IReader reader)
        {
            int lcx = reader.GetInt32("lcx");
            int lcy = reader.GetInt32("lcy");
            double lsx = reader.GetDouble("lsx");
            double lsy = reader.GetDouble("lsy");
            int interpolation = reader.GetInt32("interpolation");
            int samples = reader.GetInt32("samples");

            GlulamData data = new GlulamData(lcx, lcy, lsx, lsy, samples);
            data.InterpolationType = (GlulamData.Interpolation)interpolation;

            Value = data;

            if (Value == null)
                throw new Exception("What in the Lord's name...");

            return true;
        }
        #endregion
    }
    /*
    public class GH_GlulamWorkpiece : GH_Goo<GlulamWorkpiece>, IGH_PreviewData
    {
        public GH_GlulamWorkpiece() { this.Value = null; }
        public GH_GlulamWorkpiece(GH_GlulamWorkpiece goo) { this.Value = goo.Value; }
        public GH_GlulamWorkpiece(GlulamWorkpiece native) { this.Value = native; }
        public override IGH_Goo Duplicate() => new GH_GlulamWorkpiece(this);
        public override bool IsValid => true;
        public override string TypeName => "GluLamb Workpiece";
        public override string TypeDescription => "GluLamb Workpiece";
        public override string ToString() => Value.ToString(); //this.Value.ToString();
        public override object ScriptVariable() => Value;

        public override bool CastFrom(object source)
        {
            if (source is GlulamWorkpiece)
            {
                Value = source as GlulamWorkpiece;
                return true;
            }
            if (source is GH_GlulamWorkpiece)
            {
                Value = (source as GH_GlulamWorkpiece).Value;
                return true;
            }
            if (source is Glulam)
            {
                Value = new GlulamWorkpiece(new BasicAssembly(source as Glulam));
                return true;
            }
            if (source is GH_Glulam)
            {
                Value = new GlulamWorkpiece(new BasicAssembly((source as GH_Glulam).Value));
                return true;
            }

            return false;
        }

        public override bool CastTo<Q>(ref Q target)
        {
            if (typeof(Q).IsAssignableFrom(typeof(GH_Mesh)))
            {
                Mesh[] meshes = Value.GetMesh();
                Mesh m = new Mesh();
                for (int i = 0; i < meshes.Length; ++i)
                {
                    m.Append(meshes[i]);
                }
                object mesh = new GH_Mesh(m);

                target = (Q)mesh;
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(GlulamWorkpiece)))
            {
                object blank = Value;
                target = (Q)blank;
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(GH_Brep)))
            {
                Brep[] breps = Value.GetBrep();
                Brep b = new Brep();
                for (int i = 0; i < breps.Length; ++i)
                {
                    b.Append(breps[i]);
                }
                object brep = new GH_Brep(b);
                target = (Q)brep;
                return true;
            }
            //if (typeof(Q).IsAssignableFrom(typeof(GH_)))
            if (typeof(Q).IsAssignableFrom(typeof(GH_Curve)))
            {
                Curve[] crvs = Value.Blank.GetAllGlulams().Select(x => x.Centreline).ToArray();
                //target = crvs.Select(x => new GH_Curve(x)).ToList() as Q;
                object crv = new GH_Curve(crvs.FirstOrDefault());
                target = (Q)(crv);
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(List<GH_Curve>)))
            {
                Curve[] crvs = Value.Blank.GetAllGlulams().Select(x => x.Centreline).ToArray();
                //target = crvs.Select(x => new GH_Curve(x)).ToList() as Q;
                object crv = crvs.Select(x => new GH_Curve(x)).ToList();
                target = (Q)(crv);
                return true;
            }

            return base.CastTo<Q>(ref target);
        }

        BoundingBox IGH_PreviewData.ClippingBox
        {
            get
            {
                BoundingBox box = BoundingBox.Empty;

                Mesh[] meshes = Value.GetMesh();

                for (int i = 0; i < meshes.Length; ++i)
                {
                    box.Union(meshes[i].GetBoundingBox(true));
                }
                return box;
            }
        }

        public void DrawViewportMeshes(GH_PreviewMeshArgs args)
        {
            //args.Pipeline.DrawMeshShaded(Value.GetBoundingMesh(), args.Material);
        }

        public void DrawViewportWires(GH_PreviewWireArgs args)
        {
        }
    }
    */
}