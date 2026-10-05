using CsvHelper.Configuration;
using Rhino;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace G2PComponents
{
    public record BomLine(
        string Name,
        int Quantity,
        double Length,
        double Width,
        double Thickness)
    {
        // Volume conversion if units are different than length units (i.e., mm -> m3)
        public static double VolumeConversion = 1e-9;
        public double UnitVolume => Length * Width * Thickness * VolumeConversion;
        public double TotalVolume => Length * Width * Thickness * VolumeConversion * Quantity;
    }
    public sealed class BomLineMap : ClassMap<BomLine>
    {
        public BomLineMap()
        {
            Map(b => b.Name).Name("Name");
            Map(b => b.Thickness).Name("Thickness (mm)");
            Map(b => b.Width).Name("Width (mm)");
            Map(b => b.Length).Name("Length (mm)");
            Map(b => b.Quantity).Name("Quantity");
            Map(b => b.UnitVolume).Name("Unit volume (m3)").TypeConverterOption.Format("0.00000");
            Map(b => b.TotalVolume).Name("Total volume (m3)").TypeConverterOption.Format("0.00000");
        }
    }
}
