using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb
{
    public class TimberBeam : IEquatable<TimberBeam>
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Length { get; set; }
        public Plane Plane { get; set; }
        public BoundingBox Bounds { get; set; }
        public TimberBeamUserData UserData { get; internal set; }

        public override string ToString() => Name;

        public override bool Equals(object obj)
        {
            if (obj is TimberBeam other)
            {
                return this.Id == other.Id;// && this.Width == other.Width && this.Height == other.Height && this.Length == other.Length;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }

        public bool Equals(TimberBeam other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.Id == other.Id;// && this.Width == other.Width && this.Height == other.Height && this.Length == other.Length;
        }
    }
}
