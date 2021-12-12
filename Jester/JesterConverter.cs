using System;
using System.IO;

namespace x0.Jester
{
    public abstract class JesterConverter
    {
        public abstract bool IsFixedSize { get; }

        public Type Type { get; }

        internal JesterConverter(Type type) => Type = type;

        internal abstract void Write(BinaryWriter writer, object source, Type type, SerializationContext ctx);
        internal abstract void Read(BinaryReader reader, ref object target, Type type, DeserializationContext ctx);

    }

    public abstract class JesterConverter<T> : JesterConverter
    {
        protected JesterConverter() : base(typeof(T))
        {
        }
    }
}
