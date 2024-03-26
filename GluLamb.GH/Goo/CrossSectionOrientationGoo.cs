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

    public class GH_CrossSectionOrientation : GH_GeometricGoo<CrossSectionOrientation>, /*IGH_PreviewData,*/ GH_ISerializable
    {
        #region Members
        //protected Mesh DisplayMesh = null;
        #endregion

        #region Constructors
        //public GH_Glulam(GH_Glulam goo) { this.Value = goo.Value; this.DisplayMesh = goo.DisplayMesh.DuplicateMesh(); }
        //public GH_Glulam(Glulam native) { this.Value = native; this.DisplayMesh = native.GetBoundingMesh(0, Value.Data.InterpolationType); }
        public GH_CrossSectionOrientation(CrossSectionOrientation native) { this.Value = native; }
        public GH_CrossSectionOrientation() { this.Value = null; }
        public GH_CrossSectionOrientation(GH_CrossSectionOrientation goo) { this.Value = goo.Value?.Duplicate(); }

        public override IGH_Goo Duplicate()
        {
            return new GH_CrossSectionOrientation(this);
        }
        #endregion

        public static CrossSectionOrientation ParseCrossSectionOrientation(object obj)
        {
            if (obj is GH_CrossSectionOrientation)
                return (obj as GH_CrossSectionOrientation).Value;
            else
                return obj as CrossSectionOrientation;
        }

        public override string TypeName => "CrossSectionOrientationGoo";
        public override string TypeDescription => "CrossSectionOrientationGoo";
        public override object ScriptVariable() => Value;
        //public BoundingBox ClippingBox => DisplayMesh.GetBoundingBox(true);

        public override bool IsValid => true;
        public override string IsValidWhyNot => "You tell me.";

        public override BoundingBox Boundingbox => BoundingBox.Empty;

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
                case CrossSectionOrientation ori:
                    Value = ori;
                    return true;
                case GH_CrossSectionOrientation gh_ori:
                    Value = gh_ori.Value;
                    return true;
            }

            return false;
        }

        //public static implicit operator Glulam(GH_Glulam g) => g.Value;
        //public static implicit operator GH_Glulam(Glulam g) => new GH_Glulam(g);

        public override bool CastTo<Q>(ref Q target)
        {
            if (Value == null) return false;
            return false;
        }

        #endregion


        #region Serialization
        public override bool Write(GH_IWriter writer)
        {
            if (Value == null) throw new Exception("GH_CrossSectionOrientation.Value is null.");
            Write(writer, Value);

            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            
            Read(reader, out CrossSectionOrientation orientation);
            Value = orientation;
            return base.Read(reader);
        }

        public static void Write(GH_IWriter writer, CrossSectionOrientation ori)
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

        public static void Read(GH_IReader reader, out CrossSectionOrientation orientation)
        {
            string type = "RmfOrientation";
            reader.TryGetString("orientation", ref type);

            switch (type)
            {
                //case "RmfOrientation":
                //    break;
                case "PlanarOrientation":
                    var plane = reader.GetPlane("orientation_plane");

                    orientation = new PlanarOrientation(new Plane(
                        new Point3d(plane.Origin.x, plane.Origin.y, plane.Origin.z),
                        new Vector3d(plane.XAxis.x, plane.XAxis.y, plane.XAxis.z),
                        new Vector3d(plane.YAxis.x, plane.YAxis.y, plane.YAxis.z)));
                    break;
                case "VectorOrientation":
                    var pt = reader.GetPoint3D("orientation_vector");
                    orientation = new VectorOrientation(new Vector3d(pt.x, pt.y, pt.z));
                    break;
                case "SurfaceOrientation":
                    var srf_bytes = reader.GetByteArray("orientation_surface");
                    var srf = GH_Convert.ByteArrayToCommonObject<Brep>(srf_bytes);
                    orientation = new SurfaceOrientation(srf);
                    break;
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
                    orientation = new VectorListOrientation(GH_Convert.ByteArrayToCommonObject<Curve>(vlguide), parameters, vectors);
                    break;
                default:
                    orientation = new RmfOrientation();

                return;
            }
        }

        public override IGH_GeometricGoo DuplicateGeometry()
        {
            return new GH_CrossSectionOrientation(Value.Duplicate());
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
            return new GH_CrossSectionOrientation(Value);
        }

        public override IGH_GeometricGoo Morph(SpaceMorph xmorph)
        {
            throw new NotImplementedException();
        }
        #endregion

    }


}