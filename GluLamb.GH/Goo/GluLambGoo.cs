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

    public class GH_Glulam : GH_GeometricGoo<Glulam>, /*IGH_PreviewData,*/ GH_ISerializable
    {
        #region Members
        //protected Mesh DisplayMesh = null;
        #endregion

        #region Constructors
        //public GH_Glulam(GH_Glulam goo) { this.Value = goo.Value; this.DisplayMesh = goo.DisplayMesh.DuplicateMesh(); }
        //public GH_Glulam(Glulam native) { this.Value = native; this.DisplayMesh = native.GetBoundingMesh(0, Value.Data.InterpolationType); }
        public GH_Glulam(Glulam native) { this.Value = native; }
        public GH_Glulam() { this.Value = null; }
        public GH_Glulam(GH_Glulam goo) { this.Value = goo.Value?.Duplicate(); }

        public override IGH_Goo Duplicate()
        {
            return new GH_Glulam(this);
        }
        #endregion

        public static Glulam ParseGlulam(object obj)
        {
            if (obj is GH_Glulam)
                return (obj as GH_Glulam).Value;
            else
                return obj as Glulam;
        }

        public override string TypeName => "GlulamGoo";
        public override string TypeDescription => "GlulamGoo";
        public override object ScriptVariable() => Value;
        //public BoundingBox ClippingBox => DisplayMesh.GetBoundingBox(true);

        public override bool IsValid => true;
        /*
    {

        get
        {
            if (Value == null) return false;
            return true;
        }

    }*/
        public override string IsValidWhyNot => "You tell me.";

        public override BoundingBox Boundingbox
        {
            get
            {
                return Value.Centreline.GetBoundingBox(true);
            }
        }

        /*{
get
{
if (Value == null) return "No data";
return string.Empty;
}
}*/
        public override string ToString() => this.Value?.ToString();

        #region Casting
        public override bool CastFrom(object source)
        {
            switch (source)
            {
                case Glulam glulam:
                    Value = glulam;
                    return true;
                case GH_Glulam gh_glulam:
                    Value = gh_glulam.Value;
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
            if (Value == null) throw new Exception("GlulamParameter.Value is null.");

            writer.SetByteArray("guide", GH_Convert.CommonObjectToByteArray(Value.Centreline));

            GH_Glulam.WriteCrossSectionOrientation(writer, Value.Orientation);
            GH_GlulamData.WriteGlulamData(writer, Value.Data);

            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (!reader.ItemExists("guide"))
            {
                Value = null;
                throw new Exception("Couldn't retrieve 'guide'.");
            }

            Curve guide = GH_Convert.ByteArrayToCommonObject<Curve>(reader.GetByteArray("guide"));
            if (guide == null)
                throw new Exception("Failed to convert 'guide'.");

            CrossSectionOrientation ori = GH_Glulam.ReadCrossSectionOrientation(reader);
            GlulamData data = GH_GlulamData.ReadGlulamData(reader);

            Value = Glulam.CreateGlulam(guide, ori, data);

            return base.Read(reader);
        }

        public static void WriteCrossSectionOrientation(GH_IWriter writer, CrossSectionOrientation ori)
        {
            writer.SetString("orientation", ori.ToString());

            switch (ori)
            {
                case RmfOrientation rmf:
                    return;
                case PlanarOrientation plan:
                    var plane = plan.Plane;
                    writer.SetPlane("orientation_plane", new GH_IO.Types.GH_Plane(
                        plane.Origin.X, plane.Origin.Y, plane.Origin.Z,
                        plane.XAxis.X, plane.XAxis.Y, plane.XAxis.Z,
                        plane.YAxis.X, plane.YAxis.Y, plane.YAxis.Z
                        
                        ));
                    return;
                case VectorOrientation vec:
                    var v = (Vector3d)vec.GetDriver();
                    writer.SetPoint3D("orientation_vector", new GH_IO.Types.GH_Point3D(v.X, v.Y, v.Z));
                    return;
                case SurfaceOrientation srf:
                    writer.SetByteArray("orientation_surface", GH_Convert.CommonObjectToByteArray(srf.GetDriver() as Brep));
                    return;
                case VectorListOrientation vlist:
                    writer.SetInt32("orientation_num_vectors", vlist.Vectors.Count);
                    writer.SetByteArray("orientation_guide", GH_Convert.CommonObjectToByteArray(vlist.GetCurve()));
                    for (int i = 0; i < vlist.Parameters.Count; ++i)
                    {
                        writer.SetDouble("orientation_parameter", i, vlist.Parameters[i]);
                        writer.SetPoint3D("orientation_vector", i, new GH_IO.Types.GH_Point3D(
                            vlist.Vectors[i].X, vlist.Vectors[i].Y, vlist.Vectors[i].Z));
                    }
                    return;
                default:
                    return;
            }
        }

        public static CrossSectionOrientation ReadCrossSectionOrientation(GH_IReader reader)
        {
            var type = reader.GetString("orientation");

            switch (type)
            {
                case "RmfOrientation":
                    return new RmfOrientation();
                case "PlanarOrientation":
                    var plane = reader.GetPlane("orientation_plane");

                    return new PlanarOrientation(new Plane(
                        new Point3d(plane.Origin.x, plane.Origin.y, plane.Origin.z), 
                        new Vector3d(plane.XAxis.x, plane.XAxis.y, plane.XAxis.z),
                        new Vector3d(plane.YAxis.x, plane.YAxis.y, plane.YAxis.z)));
                case "VectorOrientation":
                    var pt = reader.GetPoint3D("orientation_vector");
                    return new VectorOrientation(new Vector3d(pt.x, pt.y, pt.z));
                case "SurfaceOrientation":
                    var srf_bytes = reader.GetByteArray("orientation_surface");
                    var srf = GH_Convert.ByteArrayToCommonObject<Brep>(srf_bytes);
                    return new SurfaceOrientation(srf);
                case "VectorListOrientation":
                    var vlguide = reader.GetByteArray("orientation_guide");

                    var num_vecs = reader.GetInt32("orientation_num_vectors");
                    List<Vector3d> vectors = new List<Vector3d>();
                    List<double> parameters = new List<double>();

                    for (int i = 0; i < num_vecs; ++i)
                    {
                        var v = reader.GetPoint3D("orientation_vector", i);
                        var t = reader.GetDouble("orientation_parameter", i);
                        vectors.Add(new Vector3d(v.x, v.y, v.z));
                    }
                    return new VectorListOrientation(GH_Convert.ByteArrayToCommonObject<Curve>(vlguide), parameters, vectors);
                default:
                    return new RmfOrientation();
            }
        }

        public override IGH_GeometricGoo DuplicateGeometry()
        {
            return new GH_Glulam(Value.Duplicate());
        }

        public override BoundingBox GetBoundingBox(Transform xform)
        {
            var bb = Boundingbox;
            if (bb.IsValid) bb.Transform(xform);
            return bb;

        }

        public override IGH_GeometricGoo Transform(Transform xform)
        {
            Value.Transform(xform);
            return new GH_Glulam(Value);
        }

        public override IGH_GeometricGoo Morph(SpaceMorph xmorph)
        {
            throw new NotImplementedException();
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

        public static void WriteGlulamData(GH_IWriter writer, GlulamData data)
        {
            writer.SetInt32("data_NumWidth", data.NumWidth);
            writer.SetInt32("data_NumHeight", data.NumHeight);
            writer.SetDouble("data_LamWidth", data.LamWidth);
            writer.SetDouble("data_LamHeight", data.LamHeight);
            writer.SetInt32("data_Samples", data.Samples);
            writer.SetInt32("data_Interpolation", (int)data.InterpolationType);
            writer.SetInt32("data_SectionAlignment", (int)data.SectionAlignment);
        }

        public static GlulamData ReadGlulamData(GH_IReader reader)
        {
            var data = new GlulamData(
                reader.GetInt32("data_NumWidth"),
                reader.GetInt32("data_NumHeight"),
                reader.GetDouble("data_LamWidth"),
                reader.GetDouble("data_LamHeight")
                );

            data.Samples = reader.GetInt32("data_Samples");
            data.InterpolationType = (GlulamData.Interpolation)reader.GetInt32("data_Interpolation");
            data.SectionAlignment = (GlulamData.CrossSectionPosition)reader.GetInt32("data_SectionAlignment");

            return data;
        }

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

    public class GH_Element : GH_Goo<Element>
    {
        #region Constructors
        public GH_Element() : this(null) { }
        public GH_Element(Element native) { this.Value = native; }

        public override IGH_Goo Duplicate()
        {
            if (Value == null)
                return new GH_Element();
            else
                return new GH_Element(Value.Duplicate());
        }
        #endregion

        public static Element ParseGlulam(object obj)
        {
            if (obj is GH_Element)
                return (obj as GH_Element).Value;
            else
                return obj as Element;
        }
        public override string ToString()
        {
            if (Value == null) return "Null glulam element";
            return Value.ToString();
        }

        public override string TypeName => "GlulamElementGoo";
        public override string TypeDescription => "GlulamElementGoo";
        public override object ScriptVariable() => Value;

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
            if (source is Element glulam)
            {
                Value = glulam;
                return true;
            }
            if (source is GH_Element ghGlulam)
            {
                Value = ghGlulam.Value;
                return true;
            }
            return false;
        }

        public override bool CastTo<Q>(ref Q target)
        {
            if (Value == null) return false;

            return false;
        }

        #endregion

    }

   

 
}