using System;
using System.Collections.Generic;
using System.Linq;

namespace x0.Jester
{
    public sealed class DataType
    {
        public readonly byte Id;
        public readonly int Size;

        // fixed size types
        public static readonly DataType Bool      = new DataType(1, 1);
        public static readonly DataType Int8      = new DataType(2, sizeof(sbyte));
        public static readonly DataType UInt8     = new DataType(3, sizeof(byte));
        public static readonly DataType Int16     = new DataType(4, sizeof(short));
        public static readonly DataType UInt16    = new DataType(5, sizeof(ushort));
        public static readonly DataType Int32     = new DataType(6, sizeof(int));
        public static readonly DataType UInt32    = new DataType(7, sizeof(uint));
        public static readonly DataType Int64     = new DataType(8, sizeof(long));
        public static readonly DataType UInt64    = new DataType(9, sizeof(ulong));
        public static readonly DataType Float32   = new DataType(10, sizeof(float));
        public static readonly DataType Float64   = new DataType(11, sizeof(double));
        public static readonly DataType DateTime  = new DataType(12, sizeof(long));
        // dynamic size types
        public static readonly DataType String    = new DataType(13);
        public static readonly DataType Array     = new DataType(14);
        public static readonly DataType Object    = new DataType(15);

        internal static readonly DataType LastBuiltInType = Object;
        internal static readonly byte LastBuiltInTypeId = LastBuiltInType.Id;

        private DataType(byte id, int size = 0)
        {
            Id   = id;
            Size = size;
        }

        private static readonly IReadOnlyDictionary<byte, int> Sizes = new [] {
            Bool,
            Int8,
            UInt8,
            Int16,
            UInt16,
            Int32,
            UInt32,
            Int64,
            UInt64,
            Float32,
            Float64,
            DateTime,
        }.ToDictionary(o => o.Id, o => o.Size);

        public static int SizeOf(byte id) => Sizes[id];
    }

    internal static class BuiltinTypes
    {
        private static readonly Dictionary<byte, TypeDescriptor> Types = new [] {
            new TypeDescriptor(DataType.Int8.Id,     new Int8Converter()),
            new TypeDescriptor(DataType.UInt8.Id,    new UInt8Converter()),
            new TypeDescriptor(DataType.Int16.Id,    new Int16Converter()),
            new TypeDescriptor(DataType.UInt16.Id,   new UInt16Converter()),
            new TypeDescriptor(DataType.Int32.Id,    new Int32Converter()),
            new TypeDescriptor(DataType.UInt32.Id,   new UInt32Converter()),
            new TypeDescriptor(DataType.Int64.Id,    new Int64Converter()),
            new TypeDescriptor(DataType.UInt64.Id,   new UInt64Converter()),
            new TypeDescriptor(DataType.Float32.Id,  new Float32Converter()),
            new TypeDescriptor(DataType.Float64.Id,  new Float64Converter()),
            new TypeDescriptor(DataType.Bool.Id,     new BoolConverter()),
            new TypeDescriptor(DataType.String.Id,   new StringConverter()),
            new TypeDescriptor(DataType.DateTime.Id, new DateTimeConverter()),
        }.ToDictionary(o => o.TypeId);

        private static readonly Dictionary<Type, TypeDescriptor> EnumTypes = new[] {
            Types[DataType.Int8.Id],
            Types[DataType.UInt8.Id],
            Types[DataType.Int16.Id],
            Types[DataType.UInt16.Id],
            Types[DataType.Int32.Id],
            Types[DataType.UInt32.Id],
            Types[DataType.Int64.Id],
            Types[DataType.UInt64.Id],
        }.ToDictionary(d => d.Converter.Type);

        public static IEnumerable<TypeDescriptor> List => Types.Values;

        public static bool TryGet(byte typeId, out TypeDescriptor desc) => Types.TryGetValue(typeId, out desc);

        public static TypeDescriptor GetEnum(Type underlyingType) => EnumTypes[underlyingType];
    }
}
