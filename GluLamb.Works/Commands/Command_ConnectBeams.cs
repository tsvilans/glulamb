using Rhino;
using Rhino.Commands;
using Rhino.UI;

using GluLamb.Views;
using Rhino.DocObjects;
using Rhino.Input.Custom;
using Rhino.Input;
using GluLamb.Joints;
using System.Collections.Generic;

namespace GluLamb.Commands
{
    public class ConnectBeamsCommand : Rhino.Commands.Command
    {
        public ConnectBeamsCommand()
        {
            Instance = this;
        }

        public static ConnectBeamsCommand Instance
        {
            get; private set;
        }

        public override string EnglishName => "GluLambConnectBeams";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            ObjRef[] objRefs = null;
            BeamObject beamObject0 = null, beamObject1 = null;

            using (GetObject go = new GetObject())
            {
                go.SetCommandPrompt("Select first beam");
                go.GeometryFilter = ObjectType.Curve;

                var goRes0 = go.Get();

                if (goRes0 != GetResult.Object) return Result.Failure;

                var rhinoObject = go.Object(0).Object();

                if (!(rhinoObject is BeamObject)) return Result.Failure;

                beamObject0 = rhinoObject as BeamObject;
            }

            using (GetObject go = new GetObject())
            {
                go.SetCommandPrompt("Select first beam");
                go.GeometryFilter = ObjectType.Curve;

                var goRes0 = go.Get();

                if (goRes0 != GetResult.Object) return Result.Failure;

                var rhinoObject = go.Object(0).Object();

                if (!(rhinoObject is BeamObject)) return Result.Failure;

                beamObject1 = rhinoObject as BeamObject;
            }

            var jointX = JointUtil.Connect(beamObject0.m_beam, 0, beamObject1.m_beam,1, -1);

            jointX.Construct(new Dictionary<int, Beam> { { 0, beamObject0.m_beam }, { 1, beamObject1.m_beam } });

            return Rhino.Commands.Result.Success;
        }
    }
}