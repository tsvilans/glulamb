using GluLamb.Projects.HHDAC22;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb.Cix
{
    public enum BeamSideType
    {
        End1,
        End2,
        Inside,
        Outside,
        Top,
        Bottom
    }

    public class BeamSide : ITransformable, ICix
    {
        public List<Operation> Operations;
        public BeamSideType SideType;

        public BeamSide(BeamSideType sideType)
        {
            SideType = sideType;
            Operations = new List<Operation>();
        }

        public void ToCix(StreamWriter writer, string prefix)
        {
            var cix = new List<string>();
            ToCix(cix, prefix);

            foreach (var line in cix)
            {
                writer.WriteLine(line);
            }
        }

        public void ToCix(List<string> cix, string prefix = "")
        {
            switch (SideType)
            {
                case (BeamSideType.Bottom):
                    break;
                case (BeamSideType.Top):
                    prefix = prefix + "TOP_";
                    break;
                case (BeamSideType.End1):
                    prefix = prefix + "E_1_";
                    break;
                case (BeamSideType.End2):
                    prefix = prefix + "E_2_";
                    break;
                case (BeamSideType.Inside):
                    prefix = prefix + "IN_";
                    break;
                case (BeamSideType.Outside):
                    prefix = prefix + "OUT_";
                    break;
            }

            for (int i = 0; i < Operations.Count; ++i)
            {
                cix.Add(string.Format("({0} ({1}))", Operations[i].Name, Operations[i].Id));
                Operations[i].ToCix(cix, prefix);
            }
        }

        public void Transform(Transform xform)
        {
            for (int i = 0; i < Operations.Count; ++i)
                Operations[i].Transform(xform);
        }
    }
}
