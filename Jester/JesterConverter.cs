using System;
using System.IO;

namespace x0.Jester
{
    public interface IMembersAware
    {
    }

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


        public abstract void Write(BinaryWriter writer, T source, Type type, SerializationContext ctx);

        internal override void Write(BinaryWriter writer, object source, Type type, SerializationContext ctx)
            => Write(writer, (T) source, type, ctx);


        public abstract void Read(BinaryReader reader, ref T target, Type type, DeserializationContext ctx);

        internal override void Read(BinaryReader reader, ref object target, Type type, DeserializationContext ctx)
        {
            var val = typeof(T).IsValueType ? default : (T) target;
            Read(reader, ref val, type, ctx);
            target = val;
        }
    }
}
