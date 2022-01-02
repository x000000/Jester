using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using x0.Jester;
using static x0.JesterTests.SerializeTestsSamples;

namespace x0.JesterTests
{
    using Path = SerializeTestsSamples.Path;

    public class SerializeTests
    {
        private static DateTime Today => DateTime.Today.ToUniversalTime();

        private TypeInspector _inspector;
        private Serializer    _serializer;
        private Deserializer  _deserializer;

        [SetUp]
        public void Setup()
        {
            var settings = new SerializerSettings()
                .AddConverter(new EntityConverter1())
                .AddConverter(new EntityConverter2())
                .AddConverter(new ColorConverter())
                .AddConverter(new PathConverter());

            _inspector    = new TypeInspector(settings);
            _serializer   = new Serializer(settings);
            _deserializer = new Deserializer(settings);
        }

        public static IEnumerable<object[]> TestSerializeSource()
        {
            var entityA = new EntityA("A") { IntField = 13 };
            var entityB = new EntityB("B") { LongField = 69 };
            var entityC = new EntityC(8)   { IntField = 51 };
            var entityD = new EntityD(3)   { LongField = 96 };

            yield return new object[] { CreateSampleData(), typeof(SampleDataObject) } ;
            foreach (var type in new [] { typeof(object), null }) {
                yield return new object[] {
                    new Color { R = 16, G = 41, B = 189 },
                    type ?? typeof(Color),
                };
                yield return new object[] {
                    new Path(new [] { "M 0 0", "L 2 3" }),
                    type ?? typeof(Path),
                };

                yield return new object[] {
                    entityA,
                    type ?? typeof(EntityA),
                };
                yield return new object[] {
                    entityB,
                    type ?? typeof(EntityB),
                };
                yield return new object[] {
                    entityC,
                    type ?? typeof(EntityC),
                };
                yield return new object[] {
                    entityD,
                    type ?? typeof(EntityD),
                };
                yield return new object[] {
                    new BaseEntity<string>[] { entityA, entityB },
                    type ?? typeof(BaseEntity<string>[]),
                };
                yield return new object[] {
                    new BaseEntity<byte>[] { entityC, entityD },
                    type ?? typeof(BaseEntity<byte>[]),
                };
                yield return new object[] {
                    new int[] { 13, 69 },
                    type ?? typeof(int[]),
                };
                yield return new object[] {
                    new object[] { 19, 96 },
                    type ?? typeof(object[]),
                };
                yield return new object[] {
                    new object[] { 21, 88L },
                    type ?? typeof(object[]),
                };
            }

            yield return new object[] {
                new object[] { entityA, entityB, entityC, entityD },
                typeof(object),
            };
        }

        [Test]
        [TestCaseSource(nameof(TestSerializeSource))]
        public void TestSerialize(object source, Type type)
        {
            var bytes  = _serializer.Serialize(source);
            var target = _deserializer.Deserialize(bytes, type);
            AssertEquals(source, target, new ValuePath(type));

            var collectionType = source.GetType()
                .GetInterfaces()
                .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            if (collectionType != null) {
                Assert.That(target, Is.AssignableTo(collectionType));
            }
        }

        [Test]
        public void TestDefaultValueReset([Values] bool knownType)
        {
            var source = new SimpleWrapper {
                IntField    = 69,
                StringField = null,
                ArrayField  = null,
            };
            var type   = knownType ? source.GetType() : typeof(object);
            var bytes  = _serializer.Serialize(source);
            var target = _deserializer.Deserialize(bytes, type);
            AssertEquals(knownType ? source : (object) ObjectToDict(source), target, new ValuePath(type));
        }

        public static IEnumerable<object[]> TestSerializeWithInjectionSource()
        {
            yield return new object[] {
                false, new SampleDataObjectWithInjection1(13) { StringField = "string value", UlongField  = 420 }
            };
            yield return new object[] {
                false, new SampleDataObjectWithInjection2(13) { StringField = "string value", UlongField  = 420 }
            };
            yield return new object[] {
                false, new SampleDataObjectWithInjection3(13) { StringField = "string value", UlongField  = 420 }
            };
            yield return new object[] {
                false, new SampleDataObjectWithInjection4(13, 420) { StringField = "string value" }
            };
            yield return new object[] {
                false, new SampleDataObjectWithInjection5(13, 420) { StringField = "string value" }
            };
            yield return new object[] {
                true, new SampleDataObjectWithBrokenInjection(13) { StringField = "string value", UlongField  = 420 }
            };
        }

        [Test]
        [TestCaseSource(nameof(TestSerializeWithInjectionSource))]
        public void TestSerializeWithInjection(bool throws, object source)
        {
            var bytes = _serializer.Serialize(source);
            if (throws) {
                var ex = Assert.Throws<JesterReadException>(() => _deserializer.Deserialize(bytes, source.GetType()));
                Assert.True(Regex.IsMatch(ex.Message, "Property '.+?' not found", RegexOptions.Compiled));
            } else {
                var target = _deserializer.Deserialize(bytes, source.GetType());
                AssertEquals(source, target, new ValuePath(source.GetType()));
            }
        }

        [Test]
        public void TestSerializeCollection([ValueSource(nameof(CollectionsValueSource))] object source)
        {
            var bytes  = _serializer.Serialize(source);
            var target = _deserializer.Deserialize<object>(bytes);
            AssertEquals(source, target, new ValuePath(typeof(object)));
        }

        [Test]
        public void TestSerializeDictionary([ValueSource(nameof(DictionariesValueSource))] IDictionary source)
        {
            var bytes  = _serializer.Serialize(source);
            var target = _deserializer.Deserialize(bytes, typeof(object));
            AssertEquals(source, target, new ValuePath(typeof(object)));
        }

        [Test]
        public void TestSerializeDictionary(
            [ValueSource(nameof(DictionaryLikeValueSource))] object source,
            [Values(typeof(object), typeof(string), typeof(int))] Type keyType,
            [Values(typeof(object), typeof(string), typeof(int))] Type valType,
            [Values(typeof(Dictionary<,>), typeof(CustomDict<,>), typeof(IDictionary<,>), typeof(IReadOnlyDictionary<,>))] Type dictType
        )
        {
            var bytes = _serializer.Serialize(source);

            string msg = null;
            if (source is IDictionary dict) {
                foreach (DictionaryEntry entry in dict) {
                    if (keyType == typeof(int) && !int.TryParse(entry.Key.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) {
                        msg = $"Unable to convert key from string to {keyType}:";
                        break;
                    }

                    if (!valType.IsInstanceOfType(entry.Value)) {
                        msg = "Incompatible value type:";
                        break;
                    }
                }
            }
            // is in non-dictionary source (SampleObject)
            else {
                if (keyType == typeof(int)) {
                    msg = $"Unable to convert key from string to {keyType}:";
                }
                else if (!valType.IsInstanceOfType(typeof(object))) {
                    msg = "Incompatible value type:";
                }
            }

            var targetType = dictType.MakeGenericType(keyType, valType);

            if (msg != null) {
                var ex = Assert.Throws<JesterReadException>(() => _deserializer.Deserialize(bytes, targetType));
                Assert.True(ex.Message.StartsWith(msg));
                return;
            }

            var target = _deserializer.Deserialize(bytes, targetType);
            Assert.IsInstanceOf(targetType, target);
            AssertEquals(source as IDictionary ?? ObjectToDict(source), target, new ValuePath(targetType));
        }

        public static IEnumerable<object[]> TestDictToObjectSource()
        {
            const string strField     = nameof(SampleObject.StringField);
            const string impDictField = nameof(SampleObject.ImplicitDictField);
            const string impArrField1 = nameof(SampleObject.ImplicitArrayField1);
            const string impArrField2 = nameof(SampleObject.ImplicitArrayField2);
            const string expArrField  = nameof(SampleObject.ExplicitArrayField);
            const string intField     = SampleObject.IntFieldName;
            const string errIntField  = nameof(SampleObject.IntField);

            IEnumerable<string> array = new [] { "one", "two" };

            // valid values
            foreach (var arrayField in new [] { impArrField1, impArrField2, expArrField }) {
                for (var i = 0; i < 2; i++) {
                    var list = i == 0 ? array : new List<string>(array);

                    yield return new object[] {
                        new Dictionary<string, object> {
                            [strField]   = "test value",
                            [intField]   = 13,
                            [arrayField] = list,
                        },
                        null,
                        arrayField,
                    };
                    yield return new object[] {
                        new Dictionary<object, object> {
                            [strField]   = "test value",
                            [intField]   = 13,
                            [arrayField] = list,
                        },
                        null,
                        arrayField,
                    };
                }
            }

            foreach (var dict in new IDictionary[] {
                new Hashtable { ["one"] = "two", ["tree"] = "four", },
                new Dictionary<string, string> { ["one"] = "two", ["tree"] = "four", },
                new Dictionary<string, object> { ["one"] = "two", ["tree"] = "four", },
                new Dictionary<object, string> { ["one"] = "two", ["tree"] = "four", },
                new Dictionary<object, object> { ["one"] = "two", ["tree"] = "four", },
            }) {
                yield return new object[] {
                    new Dictionary<string, object> {
                        [strField]     = "test value",
                        [intField]     = 13,
                        [impDictField] = dict,
                    },
                    null,
                    null,
                };
            }

            // invalid field
            yield return new object[] {
                new Dictionary<string, object> { [strField] = "test value", [errIntField] = 13 },
                $"Invalid property name {errIntField} at ",
                null,
            };
            yield return new object[] {
                new Dictionary<object, object> { [strField] = "test value", [errIntField] = 13 },
                $"Invalid property name {errIntField} at ",
                null,
            };

            // invalid values
            yield return new object[] {
                new Dictionary<string, string> { [strField] = "test value", [intField] = "13" },
                $"Failed to set {intField} value: ",
                null,
            };
            yield return new object[] {
                new Dictionary<object, string> { [strField] = "test value", [intField] = "13" },
                $"Failed to set {intField} value: ",
                null,
            };

            // invalid key types
            yield return new object[] {
                new Dictionary<int, object> { [23] = "test value", [18] = 13 },
                $"Deserializing object with 0x{DataType.Int32.Id:X2} key type into",
                null,
            };
            yield return new object[] {
                new Dictionary<int, string> { [23] = "test value", [18] = "13" },
                $"Deserializing object with 0x{DataType.Int32.Id:X2} key type into",
                null,
            };
            yield return new object[] {
                new Dictionary<object, object> { [strField] = "test value", [18] = 13 },
                "Invalid property name 18 at ",
                null,
            };
        }

        [Test]
        [TestCaseSource(nameof(TestDictToObjectSource))]
        public void TestDictToObject(IDictionary dict, string err, string arrayField)
        {
            var bytes = _serializer.Serialize(dict);
            var targetType = typeof(SampleObject);

            if (err != null) {
                var ex = Assert.Throws<JesterReadException>(() => _deserializer.Deserialize(bytes, targetType));
                StringAssert.StartsWith(err, ex.Message);
                return;
            }

            var target = _deserializer.Deserialize(bytes, targetType);
            var source = new SampleObject {
                StringField = "test value",
                IntField    = 13,
            };

            switch (arrayField) {
                case nameof(SampleObject.ImplicitArrayField1):
                    source.ImplicitArrayField1 = (IEnumerable) dict[arrayField];
                    break;
                case nameof(SampleObject.ImplicitArrayField2):
                    source.ImplicitArrayField2 = (IEnumerable<string>) dict[arrayField];
                    break;
                case nameof(SampleObject.ExplicitArrayField):
                    source.ExplicitArrayField = ((IEnumerable<string>) dict[arrayField]).ToArray();
                    break;
            }
            if (dict.Contains(nameof(SampleObject.ImplicitDictField))) {
                source.ImplicitDictField = (IDictionary) dict[nameof(SampleObject.ImplicitDictField)];
            }

            Assert.IsInstanceOf(targetType, target);
            AssertEquals(source, target, new ValuePath(targetType));
        }


        private Dictionary<string, object> ObjectToDict(object source)
        {
            var dict = new Dictionary<string, object>();
            foreach (var member in _inspector.GetTypeFields(source.GetType())) {
                var value = member.Get(source);
                if (!member.WriteDefaultValue && value == member.DefaultValue) {
                    continue;
                }
                dict.Add(member.Name, value);
            }
            return dict;
        }

        private void AssertEquals<T>(T source, T target, ValuePath path)
        {
            if (source == null) {
                Assert.Null(target);
            }
            else if (source is IComparable || source is IEnumerable) {
                AssertValue(source, target, path);
            }
            else {
                var members = source.GetType().GetMembers(BindingFlags.Instance | BindingFlags.Public);
                foreach (var member in members) {
                    switch (member) {
                        case PropertyInfo pi:
                            AssertValue(source, target, pi.GetValue, path, pi.Name, pi.PropertyType);
                            break;
                        case FieldInfo fi:
                            AssertValue(source, target, fi.GetValue, path, fi.Name, fi.FieldType);
                            break;
                    }
                }
            }
        }

        private void AssertValue(object source, object target, Func<object, object> getter, ValuePath path, string name, Type type)
        {
            path.Push(name, type);
            var expected = getter(source);
            var actual   = getter(target);
            AssertValue(expected, actual, path);
            path.Pop();
        }

        private void AssertValue(object expected, object actual, ValuePath path)
        {
            var propName = path.ToString();
            Assert.DoesNotThrow(() => {
                if (expected == null) {
                    Assert.Null(actual, $"Different value at {propName}");
                }
                else if (expected is IComparable cmp) {
                    Assert.True(cmp.CompareTo(actual) == 0, $"Different value at {propName}");
                }
                else if (expected is IDictionary dict1) {
                    var dict2 = (IDictionary) actual;

                    Assert.AreEqual(dict1.Count, dict2.Count, $"Dictionary length mismatch: {dict2.Count}, {dict1.Count} expected");

                    var actualArgsResolved   = GetDictArgs(actual.GetType(),   out var actualKeyType,   out var actualValueType);
                    var expectedArgsResolved = GetDictArgs(expected.GetType(), out var expectedKeyType, out var expectedValueType);
                    var parentArgsResolved   = GetDictArgs(path.PeekType(),    out var parentKeyType,   out _);

                    bool compatibleKeys;
                    // source object had kv types info (e.g. IDictionary<TKet, TValue>)
                    // it should be deserialized into dictionary with the same generic args unless other type was specified explicitly
                    if (expectedArgsResolved && expectedKeyType != typeof(object)) {
                        compatibleKeys = !parentArgsResolved || parentKeyType.IsAssignableFrom(expectedKeyType);
                    } else {
                        compatibleKeys = false;
                    }

                    if (path.NestingLevel > 1) {
                        Assert.AreEqual(expectedKeyType,   actualKeyType,   $"Dictionary key type mismatch: {actualKeyType.Name}, {expectedKeyType.Name} expected");
                        Assert.AreEqual(expectedValueType, actualValueType, $"Dictionary value type mismatch: {actualValueType.Name}, {expectedValueType.Name} expected");
                    }

                    foreach (DictionaryEntry entry in dict1) {
                        var key = compatibleKeys ? entry.Key : entry.Key.ToString();

                        path.Push(key.ToString(), expectedValueType);
                        AssertEquals(entry.Value, dict2[key], path);
                        path.Pop();
                    }
                }
                else if (expected is IEnumerable expectedList) {
                    var actualList = (IEnumerable) actual;

                    var expectedIt = expectedList.GetEnumerator();
                    var actualIt   = actualList.GetEnumerator();
                    var iface      = GetGenericType(expected.GetType(), typeof(IEnumerable<>));
                    var valType    = iface.GenericTypeArguments[0];
                    var hasItem    = true;
                    var index      = 0;

                    while (hasItem) {
                        Assert.AreEqual(hasItem = expectedIt.MoveNext(), actualIt.MoveNext(), $"Collection length mismatch at {index}");
                        if (hasItem) {
                            path.Push($"{index++}", valType);
                            AssertEquals(expectedIt.Current, actualIt.Current, path);
                            path.Pop();
                        }
                    }
                }
                else {
                    Assert.AreEqual(expected, actual, $"Different value at {propName}");
                    var type = expected.GetType();
                    if (type != typeof(string) && !type.IsPrimitive) {
                        AssertEquals(expected, actual, path);
                    }
                }
            }, $"Failed to assert value at {propName}");
        }


        private bool GetDictArgs(Type type, out Type keyType, out Type valueType)
        {
            var expectedIface = GetGenericType(type, typeof(IDictionary<,>), typeof(IReadOnlyDictionary<,>));
            if (expectedIface == null) {
                keyType   = typeof(object);
                valueType = typeof(object);
                return false;
            }
            keyType   = expectedIface?.GenericTypeArguments[0];
            valueType = expectedIface?.GenericTypeArguments[1];
            return true;
        }

        private Type GetGenericType(Type type, params Type[] typeDefs)
        {
            if (MatchTypeDef(type, typeDefs)) {
                return type;
            }
            foreach (var iface in type.GetInterfaces()) {
                if (MatchTypeDef(iface, typeDefs)) {
                    return iface;
                }
            }
            return null;
        }

        private bool MatchTypeDef(Type type, Type[] typeDefs)
        {
            if (type.IsGenericType) {
                var typeDef = type.GetGenericTypeDefinition();
                return typeDefs.Any(d => d == typeDef);
            }
            return false;
        }


        private static SampleDataObject CreateSampleData()
        {
            return new SampleDataObject
            {
                StringField   = "String data",
                SbyteField    = 1,
                ByteField     = 2,
                ShortField    = 3,
                UshortField   = 4,
                IntField      = 5,
                UintField     = 6,
                LongField     = 7,
                UlongField    = 8,
                FloatField    = 9,
                DoubleField   = 10,
                BoolField     = true,
                DateTimeField = Today,

                IntArray  = new [] { 1, 2, 3 },
                LongArray = new [] { 1L, 2L, 3L },

                IntList      = new List<int> { 1, 2, 3 },
                LongList     = new List<long> { 1L, 2L, 3L },
                StringList   = new List<string> { "one", "two", "three" },

                IntIList     = new List<int> { 1, 2, 3 },
                LongIList    = new List<long> { 1L, 2L, 3L },
                StringIList  = new List<string> { "four", "five", "six" },

                IntRoList    = new List<int> { 1, 2, 3 },
                LongRoList   = new List<long> { 1L, 2L, 3L },
                StringRoList = new List<string> { "seven", "eight", "nine" },

                CustomList   = new CustomList<int> { 1, 2, 3 },
                CustomIList  = new CustomList<int> { 1, 2, 3 },
                CustomRoList = new CustomList<int> { 1, 2, 3 },

                CustomStringList   = new CustomList<string> { "one", "two", "three" },
                CustomStringIList  = new CustomList<string> { "four", "five", "six" },
                CustomStringRoList = new CustomList<string> { "seven", "eight", "nine" },

                CustomObjectList   = new CustomList<object>(CreateMixedObjects()),
                CustomObjectIList  = new CustomList<object>(CreateMixedObjects()),
                CustomObjectRoList = new CustomList<object>(CreateMixedObjects()),

                CustomFixedConvObject = new Color {
                    R = 13,
                    G = 69,
                    B = 222,
                },
                CustomDynamicConvObject = new Path(new [] {
                    "M 50 0",
                    "L 100 64",
                    "L 0 64",
                    "L 50 0",
                }),

                IntEnumField   = IntEnum.Two,
                IntFlagsField  = IntFlags.Item1 | IntFlags.Item3,

                ByteEnumField  = ByteEnum.First,
                ByteFlagsField = ByteFlags.Item2 | ByteFlags.Item4,
            };
        }

        private static IEnumerable<object> CreateMixedObjects()
        {
            var list = new List<object> {
                true, false,                                                                                   // Bool
                (sbyte)  0,   (sbyte)  1,   (sbyte)   (sbyte.MinValue * .66), (sbyte)   (sbyte.MaxValue * .7), // Int8
                (byte)   0,   (byte)   1,   (byte)     (byte.MaxValue * .66), (byte)     (byte.MaxValue * .7), // UInt8
                (short)  0,   (short)  1,   (short)   (short.MinValue * .66), (short)   (short.MaxValue * .7), // Int16
                (ushort) 0,   (ushort) 1,   (ushort) (ushort.MaxValue * .66), (ushort) (ushort.MaxValue * .7), // UInt16
                (int)    0,   (int)    1,   (int)       (int.MinValue * .66), (int)       (int.MaxValue * .7), // Int32
                (uint)   0U,  (uint)   1U,  (uint)     (uint.MaxValue * .66), (uint)     (uint.MaxValue * .7), // UInt32
                (long)   0L,  (long)   1L,  (long)     (long.MinValue * .66), (long)     (long.MaxValue * .7), // Int64
                (ulong)  0UL, (ulong)  1UL, (ulong)   (ulong.MaxValue * .66), (ulong)   (ulong.MaxValue * .7), // UInt64
                (float)  0F,  (float)  1F,  (float)   (float.MinValue * .66), (float)   (float.MaxValue * .7), // Float32
                (double) 0D,  (double) 1D,  (double) (double.MinValue * .66), (double) (double.MaxValue * .7), // Float64
                Today, Today.Subtract(TimeSpan.FromDays(0x13)),                                                // DateTime
                "", "one", "two", "three",                                                                     // String

                // TODO: Objects
            };

            // Array-like
            list.AddRange(CollectionsValueSource());

            return list.ToArray();
        }

        private static IEnumerable<object> CollectionsValueSource()
        {
            yield return new int[]    { 1, 2, 3 };
            yield return new string[] { "one", "two", "three" };
            yield return new object[] { 1, "two", new Dictionary<object, object> { [3] = "three", ["four"] = 4 } };

            yield return new List<int>    { 1, 2, 3 };
            yield return new List<string> { "one", "two", "three" };
            yield return new List<object> { 1, "two", new Dictionary<object, object> { [3] = "three", ["four"] = 4 } };
        }

        private static IEnumerable<object> DictionariesValueSource()
        {
            yield return new Dictionary<object, object> { [3]   = "three", ["four"] = 4 };
            yield return new CustomDict<object, object> { [3]   = "three", ["four"] = 4 };
            yield return new Dictionary<string, object> { ["3"] = "three", ["four"] = 4 };
            yield return new CustomDict<string, object> { ["3"] = "three", ["four"] = 4 };
            yield return new Dictionary<int, object>    { [3]   = "three", [4] = 4 };
            yield return new CustomDict<int, object>    { [3]   = "three", [4] = 4 };

            yield return new Dictionary<object, string> { [3]   = "three", ["four"] = "4" };
            yield return new CustomDict<object, string> { [3]   = "three", ["four"] = "4" };
            yield return new Dictionary<string, string> { ["3"] = "three", ["four"] = "4" };
            yield return new CustomDict<string, string> { ["3"] = "three", ["four"] = "4" };
            yield return new Dictionary<int, string>    { [3]   = "three", [4] = "4" };
            yield return new CustomDict<int, string>    { [3]   = "three", [4] = "4" };

            yield return new Dictionary<object, int>    { [3]   = 3, ["four"] = 4 };
            yield return new CustomDict<object, int>    { [3]   = 3, ["four"] = 4 };
            yield return new Dictionary<string, int>    { ["3"] = 3, ["four"] = 4 };
            yield return new CustomDict<string, int>    { ["3"] = 3, ["four"] = 4 };
            yield return new Dictionary<int, int>       { [3]   = 3, [4] = 4 };
            yield return new CustomDict<int, int>       { [3]   = 3, [4] = 4 };
        }

        private static IEnumerable<object> DictionaryLikeValueSource()
        {
            foreach (var dict in DictionariesValueSource()) {
                yield return dict;
            }

            yield return new SampleObject {
                StringField         = "test value",
                IntField            = 13,
                ImplicitArrayField1 = new object[] { 1, "string item", 3L },
            };

            yield return new SampleObject {
                StringField         = "test value",
                IntField            = 13,
                ImplicitArrayField1 = new [] { "item 1", "item 2" },
            };

            yield return new SampleObject {
                StringField         = "test value",
                IntField            = 13,
                ImplicitArrayField2 = new [] { "item 3", "item 4" },
            };

            yield return new SampleObject {
                StringField         = "test value",
                IntField            = 13,
                ExplicitArrayField  = new [] { "item 5", "item 6" },
            };
        }

        private class ValuePath
        {
            public int NestingLevel => _types.Count;

            private readonly Stack<string> _path = new Stack<string>(new[] { "$" });
            private readonly Stack<Type> _types = new Stack<Type>();

            public ValuePath(Type rootType) => _types.Push(rootType);

            public void Push(string item, Type type)
            {
                _path.Push(item);
                _types.Push(type);
            }

            public void Pop()
            {
                _path.Pop();
                _types.Pop();
            }

            public Type PeekType() => _types.Peek();

            public override string ToString() => string.Join(".", _path.Reverse());
        }


        private class ColorConverter : JesterConverter<Color>
        {
            public override bool IsFixedSize => true;

            public override void Write(BinaryWriter writer, Color source, Type type, SerializationContext ctx)
            {
                writer.Write(source.R);
                writer.Write(source.G);
                writer.Write(source.B);
            }

            public override void Read(BinaryReader reader, ref Color target, Type type, DeserializationContext ctx)
            {
                target = new Color {
                    R = reader.ReadByte(),
                    G = reader.ReadByte(),
                    B = reader.ReadByte(),
                };
            }
        }

        private class PathConverter : JesterConverter<Path>
        {
            public override bool IsFixedSize => false;

            public override void Write(BinaryWriter writer, Path source, Type type, SerializationContext ctx)
                => ctx.Write(source.Items);

            public override void Read(BinaryReader reader, ref Path target, Type type, DeserializationContext ctx)
            {
                IEnumerable<string> items = default;
                ctx.Read(ref items);
                target = new Path(items);
            }
        }

        private class EntityConverter1 : JesterConverter<BaseEntity<string>>, IMembersAware
        {
            public override bool IsFixedSize => false;

            public override void Write(BinaryWriter writer, BaseEntity<string> source, Type type, SerializationContext ctx)
            {
                ctx.WriteCString(source.ItemType);
                ctx.WriteFields(source);
            }

            public override void Read(BinaryReader reader, ref BaseEntity<string> target, Type type, DeserializationContext ctx)
            {
                var itemType = ctx.ReadCString();
                target = itemType switch {
                    "A" => new EntityA(itemType),
                    "B" => new EntityB(itemType),
                    _ => throw new ArgumentOutOfRangeException(),
                };

                var targetObj = (object) target;
                ctx.ReadFields(ref targetObj);
            }
        }

        private class EntityConverter2 : JesterConverter<BaseEntity<byte>>, IMembersAware
        {
            public override bool IsFixedSize => false;

            public override void Write(BinaryWriter writer, BaseEntity<byte> source, Type type, SerializationContext ctx)
            {
                writer.Write(source.ItemType);
                ctx.WriteFields(source);
            }

            public override void Read(BinaryReader reader, ref BaseEntity<byte> target, Type type, DeserializationContext ctx)
            {
                var itemType = reader.ReadByte();
                target = itemType switch {
                    8 => new EntityC(itemType),
                    3 => new EntityD(itemType),
                    _ => throw new ArgumentOutOfRangeException(),
                };

                var targetObj = (object) target;
                ctx.ReadFields(ref targetObj);
            }
        }
    }
}
