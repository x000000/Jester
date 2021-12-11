using System;
using static System.AttributeTargets;

namespace x0.Jester
{
    [AttributeUsage(Class | Struct | Interface, AllowMultiple = false, Inherited = true)]
    public class JesterSerializeAttribute : Attribute
    {
        public JesterSerializeMembers Serialize { get; set; } = JesterSerializeMembers.All;
    }

    [AttributeUsage(Property | Field, AllowMultiple = false, Inherited = true)]
    public class JesterPropertyAttribute : Attribute
    {
        public string Name { get; set; }
        public int Order { get; set; }
        public bool Read { get; set; } = true;
        public bool Write { get; set; } = true;
        public bool Ignore => !Read && !Write;
        public Type WriteAs { get; set; }
    }

    [AttributeUsage(Parameter, AllowMultiple = false, Inherited = false)]
    public class JesterItemsAttribute : Attribute
    {
    }

    [AttributeUsage(Constructor | Method, AllowMultiple = false, Inherited = true)]
    public class JesterCreatorAttribute : Attribute
    {
        public Type Type { get; set; }
        public bool RequireItems { get; set; }
    }

    [AttributeUsage(Class | Struct, AllowMultiple = false, Inherited = false)]
    public class JesterFactoryAttribute : Attribute
    {
        public Type FactoryClass { get; }

        public JesterFactoryAttribute(Type factoryClass) => FactoryClass = factoryClass;
    }

    [Flags]
    public enum JesterSerializeMembers
    {
        None       = 0,
        Fields     = 1,
        Properties = 2,
        All        = Fields | Properties,
    }
}
