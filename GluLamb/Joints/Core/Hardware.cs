using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;

namespace GluLamb.Joints
{
    /// <summary>
    /// A separate part a joint needs, such as a dowel or a connector plate, as opposed to a
    /// cut in a beam (a Feature). Used for material take-offs.
    /// </summary>
    public abstract class HardwareItem
    {
        /// <summary>
        /// Beams the item connects (Beam.Id).
        /// </summary>
        public List<string> BeamIds = new List<string>();

        /// <summary>
        /// Category for take-offs, e.g. "Dowel" or "Plate".
        /// </summary>
        public abstract string Category { get; }

        /// <summary>
        /// Specification that items are grouped by in take-offs, e.g. "Ø16" or "t=21".
        /// </summary>
        public abstract string Specification { get; }

        /// <summary>
        /// Amount used for the take-off in model units: length for dowels, area for plates.
        /// </summary>
        public abstract double Amount { get; }

        /// <summary>
        /// Unit dimension of Amount: 1 for length, 2 for area, 0 for a plain count.
        /// </summary>
        public abstract int AmountDimension { get; }

        public abstract GeometryBase GetGeometry();

        public override string ToString() => $"{Category} {Specification}";
    }

    /// <summary>
    /// A dowel. Axis is the dowel itself, not the drilled hole (see the Drilling feature).
    /// </summary>
    public class DowelItem : HardwareItem
    {
        public Line Axis;
        public double Diameter;

        public DowelItem(Line axis, double diameter, params string[] beamIds)
        {
            Axis = axis;
            Diameter = diameter;
            BeamIds.AddRange(beamIds);
        }

        public override string Category => "Dowel";
        public override string Specification => $"Ø{Diameter:0.##}";
        public override double Amount => Axis.Length;
        public override int AmountDimension => 1;

        public override GeometryBase GetGeometry() =>
            new Cylinder(new Circle(new Plane(Axis.From, Axis.Direction), Diameter * 0.5), Axis.Length).ToBrep(true, true);
    }

    /// <summary>
    /// A flat connector plate.
    /// </summary>
    public class PlateItem : HardwareItem
    {
        public string Name = "Plate";
        public double Thickness;
        public Plane Plane = Plane.Unset;
        public Curve Outline;
        public Brep Geometry;

        public override string Category => Name;
        public override string Specification => $"t={Thickness:0.##}";

        public override double Amount
        {
            get
            {
                if (Outline == null) return 0;
                var amp = AreaMassProperties.Compute(Outline);
                return amp == null ? 0 : amp.Area;
            }
        }

        public override int AmountDimension => 2;

        public override GeometryBase GetGeometry() => Geometry;
    }

    /// <summary>
    /// A rectangular key or wedge, e.g. the key driven into a keyed scarf.
    /// </summary>
    public class KeyItem : HardwareItem
    {
        public Box Box;

        public KeyItem(Box box, params string[] beamIds)
        {
            Box = box;
            BeamIds.AddRange(beamIds);
        }

        public override string Category => "Key";
        public override string Specification => $"{Box.X.Length:0.##}×{Box.Y.Length:0.##}";
        public override double Amount => Box.Z.Length;
        public override int AmountDimension => 1;

        public override GeometryBase GetGeometry() => Box.ToBrep();
    }

    /// <summary>
    /// One line of a take-off: all items of a category and specification.
    /// </summary>
    public class TakeOffLine
    {
        public string Category;
        public string Specification;
        public int Count;
        public double Amount;
        public int AmountDimension;

        /// <summary>
        /// Group hardware by category and specification.
        /// </summary>
        public static List<TakeOffLine> Create(IEnumerable<HardwareItem> items) =>
            items.Where(x => x != null)
                .GroupBy(x => (x.Category, x.Specification, x.AmountDimension))
                .Select(g => new TakeOffLine
                {
                    Category = g.Key.Category,
                    Specification = g.Key.Specification,
                    AmountDimension = g.Key.AmountDimension,
                    Count = g.Count(),
                    Amount = g.Sum(x => x.Amount)
                })
                .OrderBy(x => x.Category).ThenBy(x => x.Specification)
                .ToList();

        /// <summary>
        /// Format with the amount converted by unitScale (model units to metres, for example).
        /// </summary>
        public string ToString(double unitScale, string unit)
        {
            switch (AmountDimension)
            {
                case 1: return $"{Category} {Specification}: {Count} pcs, {Amount * unitScale:0.###} {unit}";
                case 2: return $"{Category} {Specification}: {Count} pcs, {Amount * unitScale * unitScale:0.###} {unit}²";
                default: return $"{Category} {Specification}: {Count} pcs";
            }
        }
    }
}
