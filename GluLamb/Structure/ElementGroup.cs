﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GluLamb
{
    [Serializable]
    public class ElementGroup: List<Element>
    {
        public string Name;
        //private List<Element> m_elements;

        public ElementGroup(string name = "ElementGroup")
        {
            Name = name;
            //m_elements = new List<Element>();
        }

        public ElementGroup Duplicate()
        {
            // TODO
            return this;
        }

        /*
        public int Count
        {
            get { return m_elements.Count; }
        }

        public Element this[int key]
        {
            get { return m_elements[key]; }
            set { m_elements[key] = value; }
        }

        public void Add(Element ele)
        {
            if (!m_elements.Contains(ele))
                m_elements.Add(ele);
        }

        public void RemoveAt(int index)
        {
            m_elements.RemoveAt(index);
        }

        public void Clear()
        {
            m_elements.Clear();
        }
        */
    }
}
