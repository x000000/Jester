using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace x0.Jester
{
    internal interface IPrimitiveConverter
    {
        bool IsFixedSize { get; }
    }

    internal abstract class PrimitiveConverter<T> : JesterConverter, IPrimitiveConverter
    {
        public override bool IsFixedSize => true;

        bool IPrimitiveConverter.IsFixedSize => true;

        protected PrimitiveConverter() : base(typeof(T)) { }
    }

    internal class Int32Converter : PrimitiveConverter<int>
    {
        internal override void Write(BinaryWriter writer, object source, Type type, SerializationContext ctx)
            => writer.Write((int) source);

        internal override void Read(BinaryReader reader, ref object target, Type type, DeserializationContext ctx)
            => target = reader.ReadInt32();
    }

    internal class UInt32Converter : PrimitiveConverter<uint>
    {
        internal override void Write(BinaryWriter writer, object source, Type type, SerializationContext ctx)
            => writer.Write((uint) source);

        internal override void Read(BinaryReader reader, ref object target, Type type, DeserializationContext ctx)
            => target = reader.ReadUInt32();
    }

    internal class Int64Converter : PrimitiveConverter<long>
    {
        internal override void Write(BinaryWriter writer, object source, Type type, SerializationContext ctx)
            => writer.Write((long) source);

        internal override void Read(BinaryReader reader, ref object target, Type type, DeserializationContext ctx)
            => target = reader.ReadInt64();
    }

    internal class UInt64Converter : PrimitiveConverter<ulong>
    {
        internal override void Write(BinaryWriter writer, object source, Type type, SerializationContext ctx)
            => writer.Write((ulong) source);

        internal override void Read(BinaryReader reader, ref object target, Type type, DeserializationContext ctx)
            => target = reader.ReadUInt64();
    }

    internal class Int16Converter : PrimitiveConverter<short>
    {
        internal override void Write(BinaryWriter writer, object source, Type type, SerializationContext ctx)
            => writer.Write((short) source);

        internal override void Read(BinaryReader reader, ref object target, Type type, DeserializationContext ctx)
            => target = reader.ReadInt16();
    }

    internal class UInt16Converter : PrimitiveConverter<ushort>
    {
        internal override void Write(BinaryWriter writer, object source, Type type, SerializationContext ctx)
            => writer.Write((ushort) source);

        internal override void Read(BinaryReader reader, ref object target, Type type, DeserializationContext ctx)
            => target = reader.ReadUInt16();
    }

    internal class Int8Converter : PrimitiveConverter<sbyte>
    {
        internal override void Write(BinaryWriter writer, object source, Type type, SerializationContext ctx)
            => writer.Write((sbyte) source);

        internal override void Read(BinaryReader reader, ref object target, Type type, DeserializationContext ctx)
            => target = reader.ReadSByte();
    }

    internal class UInt8Converter : PrimitiveConverter<byte>
    {
        internal override void Write(BinaryWriter writer, object source, Type type, SerializationContext ctx)
            => writer.Write((byte) source);

        internal override void Read(BinaryReader reader, ref object target, Type type, DeserializationContext ctx)
            => target = reader.ReadByte();
    }

    internal class Float32Converter : PrimitiveConverter<float>
    {
        internal override void Write(BinaryWriter writer, object source, Type type, SerializationContext ctx)
            => writer.Write((float) source);

        internal override void Read(BinaryReader reader, ref object target, Type type, DeserializationContext ctx)
            => target = reader.ReadSingle();
    }

    internal class Float64Converter : PrimitiveConverter<double>
    {
        internal override void Write(BinaryWriter writer, object source, Type type, SerializationContext ctx)
            => writer.Write((double) source);

        internal override void Read(BinaryReader reader, ref object target, Type type, DeserializationContext ctx)
            => target = reader.ReadDouble();
    }

    internal class BoolConverter : PrimitiveConverter<bool>
    {
        internal override void Write(BinaryWriter writer, object source, Type type, SerializationContext ctx)
            => writer.Write((bool) source);

        internal override void Read(BinaryReader reader, ref object target, Type type, DeserializationContext ctx)
            => target = reader.ReadBoolean();
    }

    internal class DateTimeConverter : PrimitiveConverter<DateTime>
    {
        internal override void Write(BinaryWriter writer, object source, Type type, SerializationContext ctx)
            => writer.Write(((DateTimeOffset) (DateTime) source).ToUniversalTime().ToUnixTimeSeconds());

        internal override void Read(BinaryReader reader, ref object target, Type type, DeserializationContext ctx)
            => target = DateTimeOffset.FromUnixTimeSeconds(reader.ReadInt64()).UtcDateTime;
    }

    internal class StringConverter : JesterConverter
    {
        public override bool IsFixedSize => false;

        public StringConverter() : base(typeof(string))
        {
        }

        internal override void Write(BinaryWriter writer, object source, Type type, SerializationContext ctx)
            => writer.Write(Encoding.UTF8.GetBytes((string) source));

        internal override void Read(BinaryReader reader, ref object target, Type type, DeserializationContext ctx)
            => target = Encoding.UTF8.GetString(reader.ReadBytes(ctx.ObjectHeader.DataLength));
    }


    internal class ArrayConverter : JesterConverter
    {
        public override bool IsFixedSize => false;

        public ArrayConverter() : base(typeof(IEnumerable))
        {
        }

        internal override void Write(BinaryWriter writer, object source, Type type, SerializationContext ctx)
            => ctx.Serializer.WriteCollection(writer, (IEnumerable) source, type, ctx);

        internal override void Read(BinaryReader reader, ref object target, Type type, DeserializationContext ctx)
            => ctx.Deserializer.ReadCollection(reader, ref target, type, ctx);
    }

    internal class ArrayConverter<T> : JesterConverter
    {
        public override bool IsFixedSize => false;

        public ArrayConverter() : base(typeof(IEnumerable))
        {
        }

        internal override void Write(BinaryWriter writer, object source, Type type, SerializationContext ctx)
            => ctx.Serializer.WriteCollection(writer, (IEnumerable<T>) source, type, ctx);

        internal override void Read(BinaryReader reader, ref object target, Type type, DeserializationContext ctx)
            => ctx.Deserializer.ReadCollection<T>(reader, ref target, type, ctx);
    }

    internal interface IDictionaryConverter
    {
        Type KeyType   { get; }
        Type ValueType { get; }
    }

    internal class DictionaryConverter : JesterConverter, IDictionaryConverter
    {
        public override bool IsFixedSize => false;

        public Type KeyType   { get; } = typeof(object);
        public Type ValueType { get; } = typeof(object);

        public DictionaryConverter() : base(typeof(IDictionary))
        {
        }

        internal override void Write(BinaryWriter writer, object source, Type type, SerializationContext ctx)
            => ctx.Serializer.WriteDictionary(writer, (IDictionary) source, type, ctx);

        internal override void Read(BinaryReader reader, ref object target, Type type, DeserializationContext ctx)
        {
            throw new NotImplementedException();
        }
    }

    internal class DictionaryConverter<TK, TV> : JesterConverter, IDictionaryConverter
    {
        public override bool IsFixedSize => false;

        public Type KeyType   { get; } = typeof(TK);
        public Type ValueType { get; } = typeof(TV);

        public DictionaryConverter() : base(typeof(IDictionary))
        {
        }

        internal override void Write(BinaryWriter writer, object source, Type type, SerializationContext ctx)
            => ctx.Serializer.WriteDictionary(writer, (IEnumerable<KeyValuePair<TK, TV>>) source, type, ctx);

        internal override void Read(BinaryReader reader, ref object target, Type type, DeserializationContext ctx)
        {
            throw new NotImplementedException();
        }
    }
}
