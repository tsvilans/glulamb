using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb
{
    public class Structure2
    {
        public string Name
        { get; set; }
        public Dictionary<int, Element2> Elements;
        public Dictionary<int, Joint> Joints;
        public Dictionary<int, Product> Blanks;

        public Dictionary<int, List<int>> EleGroups;
        public Dictionary<int, List<int>> JointGroups;

        private int NextEleId;
        private int NextJointId;

        public Structure2(string name = "Structure", int eleStart = 10000, int jntStart = 20000)
        {
            Name = name;
            NextEleId = eleStart;
            NextJointId = jntStart;

            Elements = new Dictionary<int, Element2>();
            Joints = new Dictionary<int, Joint>();
            Blanks = new Dictionary<int, Product>();

            EleGroups = new Dictionary<int, List<int>>();
            JointGroups = new Dictionary<int, List<int>>();
        }

        public void AddElement(Element2 ele)
        {
            Elements.Add(NextEleId, ele);
            NextEleId++;
        }

        public void AddJoint(Joint jnt)
        {
            Joints.Add(NextJointId, jnt);
            NextJointId++;
        }

        public int GetId(Element2 ele) => Elements.FirstOrDefault(x => x.Value == ele).Key;
        public int GetId(Joint jnt) => Joints.FirstOrDefault(x => x.Value == jnt).Key;
    }

    public class Node
    {
        public int Id;
        public string Name;

        public override bool Equals(object n)
        {
            return n is Node && Id == (n as Node).Id;
        }

        public bool Equals(Node obj)
        {
            return obj != null && obj.Id == this.Id;
        }

        public override int GetHashCode()
        {
            return Id;
        }
    }

    public class Element2 : Node
    { 
        public int Blank { get; set; }
        public List<int> Joints;

        public Element2()
        {
            Joints = new List<int>();
        }
    }

    public class Product : Node
    {
        public List<int> Children;

        public Product()
        {
            Children = new List<int>();
        }

    }
}
