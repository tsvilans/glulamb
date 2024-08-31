using GluLamb.Forms;
using Rhino;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb
{
    public class BeamSelectionCommand : Rhino.Commands.Command
    {
        public override string EnglishName => "SelectTimberBeam";

        protected override Rhino.Commands.Result RunCommand(RhinoDoc doc, Rhino.Commands.RunMode mode)
        {
            GluLambPlugin.Instance.Model.ClearBeams();


            var form = new BeamSelectionForm();
            form.Closed += (sender, e) => form.Dispose();
            form.Show();

            GluLambPlugin.Instance.Model.LoadBeams(RhinoDoc.ActiveDoc);

            //SampleCsDrawMeshConduit conduit = new SampleCsDrawMeshConduit();
            //conduit.Enabled = true;
            RhinoDoc.ActiveDoc.Views.Redraw();

            return Rhino.Commands.Result.Success;
        }
    }



}
