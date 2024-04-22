/*
 * GluLamb
 * A constrained glulam modelling toolkit.
 * Copyright 2024 Tom Svilans
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
using GluLamb;
using GH_IO.Types;

namespace GluLamb.GH
{

    public class GH_Joint : GH_GeometricGoo<JointX>, /*IGH_PreviewData,*/ GH_ISerializable
    {
        #region Members
        //protected Mesh DisplayMesh = null;
        #endregion

        #region Constructors
        //public GH_Glulam(GH_Glulam goo) { this.Value = goo.Value; this.DisplayMesh = goo.DisplayMesh.DuplicateMesh(); }
        //public GH_Glulam(Glulam native) { this.Value = native; this.DisplayMesh = native.GetBoundingMesh(0, Value.Data.InterpolationType); }
        public GH_Joint(JointX native) { this.Value = native; }
        public GH_Joint() { this.Value = null; }
        public GH_Joint(GH_Joint goo) { this.Value = goo.Value?.DuplicateJoint(); }

        public override IGH_Goo Duplicate()
        {
            return new GH_Joint(this);
        }
        #endregion

        public override string TypeName => "Joint";
        public override string TypeDescription => "Joint";
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
                var bb = BoundingBox.Empty;
                foreach (var part in Value.Parts)
                {
                    foreach(var geo in part.Geometry)
                    {
                        bb.Union(geo.GetBoundingBox(true));
                    }
                }
                return bb;
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
                case JointX joint:
                    Value = joint;
                    return true;
                case GH_Joint ghJoint:
                    Value = ghJoint.Value;
                    return true;

            }

            return false;
        }

        public override bool CastTo<Q>(ref Q target)
        {
            if (Value == null) return false;

            if (typeof(Q).IsAssignableFrom(typeof(GH_Joint)))
            {
                object blank = new GH_Joint(Value.DuplicateJoint());

                target = (Q)blank;
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(JointX)))
            {
                object cl = Value.DuplicateJoint();
                target = (Q)cl;
                return true;
            }
            return false;
        }

        #endregion

        #region Serialization
        public override bool Write(GH_IWriter writer)
        {
            if (Value == null) throw new Exception("JointParameter.Value is null.");
            writer.SetString("Type", Value.ToString());
            writer.SetInt32("NumParts", Value.Parts.Count);
            writer.SetPoint3D("Position", new GH_Point3D(Value.Position.X, Value.Position.Y, Value.Position.Z));

            for (int i = 0; i < Value.Parts.Count; ++i)
            {
                writer.SetInt32($"{i} Case", Value.Parts[i].Case);
                writer.SetInt32($"{i} ElementIndex", Value.Parts[i].ElementIndex);
                writer.SetInt32($"{i} JointIndex", Value.Parts[i].JointIndex);
                writer.SetPoint3D($"{i} Direction", new GH_Point3D(Value.Parts[i].Direction.X, Value.Parts[i].Direction.Y, Value.Parts[i].Direction.Z));
                writer.SetInt32($"{i} NumGeo", Value.Parts[i].Geometry.Count);

                for (int j = 0; j < Value.Parts[i].Geometry.Count; ++j)
                {
                    writer.SetByteArray($"{i} {j} Geometry", GH_Convert.CommonObjectToByteArray(Value.Parts[i].Geometry[j]));
                }
            }

            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            string type = "Joint";
            reader.TryGetString("Type", ref type);
            
            int numParts = 0;
            reader.TryGetInt32("NumParts", ref numParts);

            GH_Point3D ghPosition = new GH_Point3D();
            reader.TryGetPoint3D("Position", ref ghPosition);
            var position = new Point3d(ghPosition.x, ghPosition.y, ghPosition.z);

            var parts = new List<JointPartX>();

            for (int i = 0; i < numParts; ++i)
            {
                int c = -1;
                reader.TryGetInt32($"{i} Case", ref c);

                int elementIndex = -1;
                reader.TryGetInt32($"{i} ElementIndex", ref elementIndex);

                int jointIndex = -1;
                reader.TryGetInt32($"{i} JointIndex", ref jointIndex);

                double parameter = 0;
                reader.TryGetDouble($"{i} Parameter", ref parameter);

                GH_Point3D ghDirection = new GH_Point3D();
                reader.TryGetPoint3D("{i} Direction", ref ghDirection);

                int numGeo = 0;
                reader.TryGetInt32($"{i} NumGeo", ref numGeo);

                var geometry = new List<Brep>();

                for (int j = 0; j < numGeo; ++j)
                {
                    Brep brep = GH_Convert.ByteArrayToCommonObject<Brep>(reader.GetByteArray($"{i} {j} Geometry"));
                    if (brep != null)
                        geometry.Add(brep);
                }

                var jointPart = new JointPartX()
                {
                    Case = c,
                    ElementIndex =
                    elementIndex,
                    JointIndex = jointIndex,
                    Parameter = parameter,
                    Direction = new Vector3d(ghDirection.x, ghDirection.y, ghDirection.z),
                    Geometry = geometry
                };

                parts.Add(jointPart);
            }

            // TO DO: classify joint based on the Type string
            var joint = new JointX(parts, position);

            return base.Read(reader);
        }

        public override IGH_GeometricGoo DuplicateGeometry()
        {
            return new GH_Joint(Value.DuplicateJoint());
        }

        public override BoundingBox GetBoundingBox(Transform xform)
        {
            var bb = Boundingbox;
            if (bb.IsValid) bb.Transform(xform);
            return bb;

        }

        public override IGH_GeometricGoo Transform(Transform xform)
        {
            var newJoint = new GH_Joint(Value);

            newJoint.Value.Position.Transform(xform);
            for (int i = 0; i < newJoint.Value.Parts.Count; ++i)
            {
                newJoint.Value.Parts[i].Direction.Transform(xform);
                for (int j = 0; j < newJoint.Value.Parts[i].Geometry.Count; ++j)
                {
                    newJoint.Value.Parts[i].Geometry[j].Transform(xform);
                }
            }
            return newJoint;
        }

        public override IGH_GeometricGoo Morph(SpaceMorph xmorph)
        {
            throw new NotImplementedException();
        }
        #endregion

    }


}