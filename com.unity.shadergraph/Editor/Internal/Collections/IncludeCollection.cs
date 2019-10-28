﻿using System.Collections;
using System.Collections.Generic;

namespace UnityEditor.ShaderGraph.Internal
{
    public class IncludeCollection : IEnumerable<IncludeCollection.Item>
    {
        public class Item : IConditionalShaderString
        {        
            public IncludeDescriptor descriptor { get; }
            public FieldCondition[] fieldConditions { get; }
            public string value => $"#include \"{value}\"";

            public Item(IncludeDescriptor descriptor, FieldCondition[] fieldConditions)
            {
                this.descriptor = descriptor;
                this.fieldConditions = fieldConditions;
            }
        }

        readonly List<Item> m_Items;

        public IncludeCollection()
        {
            m_Items = new List<Item>();
        }

        public void Add(string include, IncludeLocation location)
        {
            m_Items.Add(new Item(new IncludeDescriptor() { value = include, location = location }, null));
        }

        public void Add(string include, IncludeLocation location, FieldCondition fieldCondition)
        {
            m_Items.Add(new Item(new IncludeDescriptor() { value = include, location = location }, new FieldCondition[]{ fieldCondition }));
        }

        public void Add(string include, IncludeLocation location, FieldCondition[] fieldConditions)
        {
            m_Items.Add(new Item(new IncludeDescriptor() { value = include, location = location }, fieldConditions));
        }

        public IEnumerator<Item> GetEnumerator()
        {
            return m_Items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
