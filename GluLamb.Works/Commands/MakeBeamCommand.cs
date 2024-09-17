using Rhino;
using Rhino.Commands;
using Rhino.UI;

using GluLamb.Views;
using Rhino.DocObjects;
using Rhino.Input.Custom;
using Rhino.Input;

namespace GluLamb.Commands
{
    public class MakeBeamCommand : Rhino.Commands.Command
    {
        public MakeBeamCommand()
        {
            Instance = this;
        }

        public static MakeBeamCommand Instance
        {
            get; private set;
        }

        public override string EnglishName => "GluLambMakeBeam";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            ObjRef[] objRefs = null;

            using (GetObject go = new GetObject())
            {
                go.SetCommandPrompt("Select beam centreline");
                go.GeometryFilter = ObjectType.Curve;

                var goRes = go.GetMultiple(1, 0);
                if (goRes == GetResult.Object)
                {
                    objRefs = go.Objects();
                }
            }

            if(objRefs != null)
            {
                foreach (var objRef in objRefs)
                {
                    var curve = objRef.Curve();
                    if (curve == null) { continue; }

                    var beamObject = new BeamObject(curve);

                    doc.Objects.AddRhinoObject(beamObject, curve);
                }
            }

            return Rhino.Commands.Result.Success;
        }
    }
}