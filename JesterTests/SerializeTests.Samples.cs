using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using x0.Jester;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedType.Local

namespace x0.JesterTests
{
    [ExcludeFromCodeCoverage]
    public static class SerializeTestsSamples
    {
        public class CustomList<T> : List<T>
        {
            public CustomList()
            {
            }

            public CustomList(IEnumerable<T> collection) : base(collection)
            {
            }
        }

        public class CustomDict<TK, TV> : Dictionary<TK, TV>
        {
        }

        public abstract class BaseEntity<T>
        {
            [JesterProperty(Read = false, Write = false)]
            public readonly T ItemType;

            protected BaseEntity(T itemType) => ItemType = itemType;
        }

        public class EntityA : BaseEntity<string>
        {
            public int IntField;

            public EntityA(string itemType) : base(itemType)
            {
            }
        }

        public class EntityB : BaseEntity<string>
        {
            public long LongField;

            public EntityB(string itemType) : base(itemType)
            {
            }
        }

        public class EntityC : BaseEntity<byte>
        {
            public int IntField;

            public EntityC(byte itemType) : base(itemType)
            {
            }
        }

        public class EntityD : BaseEntity<byte>
        {
            public long LongField;

            public EntityD(byte itemType) : base(itemType)
            {
            }
        }

        public class SampleObject
        {
            public const string IntFieldName = "AltIntField";

            public string StringField;
            [JesterProperty(Name = IntFieldName)]
            public int    IntField;
            public IDictionary ImplicitDictField;
            public IEnumerable ImplicitArrayField1;
            public IEnumerable<string> ImplicitArrayField2;
            public string[] ExplicitArrayField;
        }

        public class SampleDataObject
        {
            public string   StringField;
            public sbyte    SbyteField;
            public byte     ByteField;
            public short    ShortField;
            public ushort   UshortField;
            public int      IntField;
            public uint     UintField;
            public long     LongField;
            public ulong    UlongField;
            public float    FloatField;
            public double   DoubleField;
            public bool     BoolField;
            public DateTime DateTimeField;

            public int[]  IntArray;
            public long[] LongArray;

            public List<int>  IntList;
            public List<long> LongList;
            public List<string> StringList;

            public IList<int>  IntIList;
            public IList<long> LongIList;
            public IList<string> StringIList;

            public IReadOnlyList<int>  IntRoList;
            public IReadOnlyList<long> LongRoList;
            public IReadOnlyList<string> StringRoList;

            public CustomList<int>    CustomList;
            public IList<int>         CustomIList;
            public IReadOnlyList<int> CustomRoList;

            public CustomList<string>    CustomStringList;
            public IList<string>         CustomStringIList;
            public IReadOnlyList<string> CustomStringRoList;

            public CustomList<object>    CustomObjectList;
            public IList<object>         CustomObjectIList;
            public IReadOnlyList<object> CustomObjectRoList;

            public Color CustomFixedConvObject;
            public Path  CustomDynamicConvObject;

            public IntEnum   IntEnumField;
            public IntFlags  IntFlagsField;
            public ByteEnum  ByteEnumField;
            public ByteFlags ByteFlagsField;
        }

        public class SampleDataObjectWithInjection1
        {
            public string StringField;
            public ulong  UlongField;
            public readonly int IntField;

            [JesterCreator]
            public SampleDataObjectWithInjection1(int intField)
            {
                IntField = intField;
            }
        }

        public class SampleDataObjectWithInjection2
        {
            public string StringField;
            public ulong  UlongField;
            public readonly int IntField;

            [JesterCreator]
            public SampleDataObjectWithInjection2(int IntField)
            {
                this.IntField = IntField;
            }
        }

        public class SampleDataObjectWithInjection3
        {
            public string StringField;
            public ulong  UlongField;
            [JesterProperty(Name = "numField")]
            public readonly int IntField;

            [JesterCreator]
            public SampleDataObjectWithInjection3(int numField)
            {
                IntField = numField;
            }
        }

        [JesterFactory(typeof(SampleDataObjectWithInjection4))]
        public class SampleDataObjectWithInjection4
        {
            public string StringField;
            public readonly ulong  UlongField;
            public readonly int IntField;

            public SampleDataObjectWithInjection4(int intField, ulong ulongField)
            {
                IntField   = intField;
                UlongField = ulongField;
            }

            [JesterCreator]
            public static SampleDataObjectWithInjection4 Create(int intField, ulong ulongField)
                => new SampleDataObjectWithInjection4(intField, ulongField);
        }

        [JesterFactory(typeof(SampleDataObjectWithInjection5))]
        public class SampleDataObjectWithInjection5
        {
            public string StringField;
            public readonly ulong  UlongField;
            [JesterProperty(Name = "numField")]
            public readonly int IntField;

            public SampleDataObjectWithInjection5(int intField, ulong ulongField)
            {
                IntField   = intField;
                UlongField = ulongField;
            }

            [JesterCreator]
            public static SampleDataObjectWithInjection5 Create(int numField, ulong ulongField)
                => new SampleDataObjectWithInjection5(numField, ulongField);
        }

        public class SampleDataObjectWithBrokenInjection
        {
            public string StringField;
            public ulong  UlongField;
            public readonly int IntField;

            [JesterCreator]
            public SampleDataObjectWithBrokenInjection(int intFieldWithWrongName)
            {
                IntField = intFieldWithWrongName;
            }
        }

        public class SimpleWrapper
        {
            public int IntField = 13;
            [JesterProperty(WriteDefaultValue = true)]
            public string StringField = "pre-populated value";
            [JesterProperty(WriteDefaultValue = true)]
            public IEnumerable<int> ArrayField = Array.Empty<int>();
        }

        public struct Color
        {
            public byte R;
            public byte G;
            public byte B;

            public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
        }

        public class Path
        {
            public IEnumerable<string> Items { get; }

            public Path(IEnumerable<string> items) => Items = new List<string>(items);

            public override string ToString() => string.Join(' ', Items);

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) {
                    return false;
                }
                if (ReferenceEquals(this, obj)) {
                    return true;
                }
                if (obj.GetType() != GetType()) {
                    return false;
                }
                return Equals(ToString(), obj.ToString());
            }

            public override int GetHashCode() => Items?.GetHashCode() ?? 0;
        }

        public enum ByteEnum : byte
        {
            None, First, Second
        }

        public enum IntEnum
        {
            None, One, Two
        }

        [Flags]
        public enum ByteFlags : byte
        {
            Item0 = 0,
            Item1 = 0b0001,
            Item2 = 0b0010,
            Item3 = 0b0100,
            Item4 = 0b1000,
        }

        [Flags]
        public enum IntFlags
        {
            Item0 = 0,
            Item1 = 0b0001,
            Item2 = 0b0010,
            Item3 = 0b0100,
            Item4 = 0b1000,
        }
    }
}
