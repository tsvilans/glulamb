﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry;

namespace GluLamb
{
    [Serializable]
    public class Structure
    {
        public List<Joint> Joints;
        public List<Connection> Connections;
        public List<Element> Elements;
        public Dictionary<string, List<Element>> Groups;
        public string Name;

        public Structure(string name = "Structure")
        {
            Name = name;
            Connections = new List<Connection>();
            Elements = new List<Element>();
            Groups = new Dictionary<string, List<Element>>();
        }
        public Structure Duplicate()
        {
            // TODO
            return this;
        }
        public static Structure FromBeamElements(List<BeamElement> elements, double searchDistance, double overlapDistance)
        {
            int counter = 0;
            foreach (var ele in elements)
            {
                //ele.Name = string.Format("Element_{0:000}", counter);
                counter++;
            }

            var structure = new Structure();

            structure.Elements.AddRange(elements);

            for (int i = 0; i < elements.Count - 1; ++i)
            {
                for (int j = i + 1; j < elements.Count; ++j)
                {
                    var intersections = Rhino.Geometry.Intersect.Intersection.CurveCurve(elements[i].Beam.Centreline, elements[j].Beam.Centreline, searchDistance, overlapDistance);

                    foreach (var intersection in intersections)
                    {
                        structure.Connections.Add(
                          Connection.Connect(
                          elements[i], elements[j],
                          intersection.ParameterA, intersection.ParameterB,
                          string.Format("{0}-{1}", elements[i].Name, elements[j].Name))
                          );
                    }
                }
            }

            return structure;
        }
        public List<object> Discretize(double length, bool adaptive = true)
        {
            var segments = new List<object>();

            foreach (var ele in Elements)
                segments.Add(ele.Discretize(length, adaptive));
            foreach (var conn in Connections)
                segments.Add(conn.Discretize(adaptive));

            return segments;
        }

        public List<Brep> ToBrep()
        {
            var breps = new List<Brep>();

            foreach (var ele in Elements)
            {
                if (ele is BeamElement && (ele as BeamElement).Beam is Glulam)
                    breps.Add(((ele as BeamElement).Beam as Glulam).ToBrep());
            }

            return breps;
        }

        public List<Mesh> ToMesh()
        {
            var meshes = new List<Mesh>();

            foreach (var ele in Elements)
            {
                if (ele is BeamElement && (ele as BeamElement).Beam is Glulam)
                    meshes.Add(((ele as BeamElement).Beam as Glulam).ToMesh());
            }

            return meshes;
        }
    }
}
