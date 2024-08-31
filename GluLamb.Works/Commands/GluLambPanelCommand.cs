using Rhino;
using Rhino.Commands;
using Rhino.UI;

using GluLamb.Views;

namespace GluLamb.Commands
{
    public class SampleCsWizardPanelCommand : Rhino.Commands.Command
    {
        public SampleCsWizardPanelCommand()
        {
            Instance = this;

            Panels.RegisterPanel(PlugIn, typeof(SampleCsEtoPanel), LOC.STR("GluLamb"), null);
        }

        public static SampleCsWizardPanelCommand Instance
        {
            get; private set;
        }

        public override string EnglishName => "GluLamb";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            Panels.OpenPanel(typeof(SampleCsEtoPanel).GUID);
            return Rhino.Commands.Result.Success;
        }
    }
}
