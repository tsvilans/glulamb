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
using System.Windows.Forms;
using Rhino;

namespace GluLamb.GH
{

    public class GH_Beam : GH_GeometricGoo<Beam>, /*IGH_PreviewData,*/ GH_ISerializable
    {
        #region Members
        //protected Mesh DisplayMesh = null;
        #endregion

        #region Constructors
        //public GH_Glulam(GH_Glulam goo) { this.Value = goo.Value; this.DisplayMesh = goo.DisplayMesh.DuplicateMesh(); }
        //public GH_Glulam(Glulam native) { this.Value = native; this.DisplayMesh = native.GetBoundingMesh(0, Value.Data.InterpolationType); }
        public GH_Beam(Beam native) { this.Value = native; }
        public GH_Beam() { this.Value = null; }
        public GH_Beam(GH_Beam goo) { this.Value = goo.Value?.Duplicate(); }

        public override IGH_Goo Duplicate()
        {
            return new GH_Beam(this);
        }
        #endregion

        public static Beam ParseGlulam(object obj)
        {
            if (obj is GH_Beam)
                return (obj as GH_Beam).Value;
            else
                return obj as Glulam;
        }

        public override string TypeName => "Beam";
        public override string TypeDescription => "Beam";
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
                case Beam glulam:
                    Value = glulam;
                    return true;
                case GH_Beam gh_glulam:
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
            /*
            if (typeof(Q).IsAssignableFrom(typeof(GH_Mesh)))
            {
                object mesh = new GH_Mesh(Value.ToMesh());

                target = (Q)mesh;
                return true;
            }
            */
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

            GH_CrossSectionOrientation.Write(writer, Value.Orientation);

            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            double m_scale = RhinoMath.UnitScale(UnitSystem.Meters, RhinoDoc.ActiveDoc.ModelUnitSystem);

            if (!reader.ItemExists("guide"))
            {
                Value = null;
                throw new Exception("Couldn't retrieve 'guide'.");
            }

            Curve guide = GH_Convert.ByteArrayToCommonObject<Curve>(reader.GetByteArray("guide"));
            if (guide == null)
                throw new Exception("Failed to convert 'guide'.");

            GH_CrossSectionOrientation.Read(reader, out CrossSectionOrientation ori);

            double width = 0.1 * m_scale, height = 0.2 * m_scale;
            reader.TryGetDouble("width", ref width);
            reader.TryGetDouble("height", ref height);

            Value = new Beam() { Centreline = guide, Orientation = ori, Width = width, Height = height };

            return base.Read(reader);
        }

        public override IGH_GeometricGoo DuplicateGeometry()
        {
            return new GH_Beam(Value.Duplicate());
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
            return new GH_Beam(Value);
        }

        public override IGH_GeometricGoo Morph(SpaceMorph xmorph)
        {
            throw new NotImplementedException();
        }
        #endregion

    }


}