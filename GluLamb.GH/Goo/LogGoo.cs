#if RAWLAM
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

    public class GH_Log : GH_Goo<RawLam.Log>, /*IGH_PreviewData,*/ GH_ISerializable
    {
        #region Members
        //protected Mesh DisplayMesh = null;
        #endregion

        #region Constructors
        public GH_Log() : this(null) { }
        public GH_Log(RawLam.Log native) { this.Value = native; }

        public override IGH_Goo Duplicate()
        {
            if (Value == null)
                return new GH_Log();
            else
                return new GH_Log(Value.Duplicate());
        }
        #endregion

        public static RawLam.Log ParseLog(object obj)
        {
            if (obj is GH_Log)
                return (obj as GH_Log).Value;
            else
                return obj as RawLam.Log;
        }
        public override string ToString()
        {
            if (Value == null) return "Null log";
            return Value.ToString();
        }

        public override string TypeName => "LogGoo";
        public override string TypeDescription => "LogGoo";
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
            if (source is RawLam.Log log)
            {
                Value = log;
                return true;
            }
            if (source is GH_Log ghLog)
            {
                Value = ghLog.Value;
                return true;
            }
            return false;
        }


        public override bool CastTo<Q>(ref Q target)
        {
            if (Value == null) return false;

            if (typeof(Q).IsAssignableFrom(typeof(GH_Mesh)))
            {
                object mesh = new GH_Mesh(Value.Mesh);

                target = (Q)mesh;
                return true;
            }

            return false;
        }

        #endregion

    }
}
#endif