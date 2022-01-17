using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace x0.Jester
{
    public class Deserializer
    {
        public const byte Version = 1;

        private static readonly MethodInfo ReadDynamicCollectionMethod = typeof(Deserializer)
            .GetMethod(nameof(ReadDynamicCollection), BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly Dictionary<Type, MethodInfo> _readDynamicCollectionMethods = new Dictionary<Type, MethodInfo>();
        private readonly TypeInspector _inspector;

        public Deserializer() : this(new SerializerSettings())
        {
        }

        public Deserializer(SerializerSettings settings) => _inspector = new TypeInspector(settings);

        public T Deserialize<T>(byte[] encodedData) => (T) Deserialize(encodedData, typeof(T));

        public T Deserialize<T>(Stream stream, bool leaveOpen = false) => (T) Deserialize(stream, typeof(T), leaveOpen);

        public object Deserialize(byte[] encodedData, Type targetType)
        {
            using (var stream = new MemoryStream(encodedData, false)) {
                return Deserialize(stream, targetType);
            }
        }

        public object Deserialize(Stream stream, Type targetType, bool leaveOpen = false)
        {
            using (var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen)) {
                if (Version != reader.ReadByte()) {
                    throw new ArgumentException("Version mismatch");
                }

                var ctx = new DeserializationContext(reader, this);
                return ReadValue(reader, reader.ReadByte(), targetType, ctx);
            }
        }

        private void ReadViaConverter(JesterConverter conv, BinaryReader reader, ref object target, Type targetType, DeserializationContext ctx)
        {
            if (conv.IsFixedSize) {
                conv.Read(reader, ref target, targetType, ctx);
            } else {
                var objectEnd = ReadDataLength(reader, ref ctx.ObjectHeader);
                conv.Read(reader, ref target, targetType, ctx);
                EnsureObjectEnd(reader.BaseStream, objectEnd);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long ReadDataLength(BinaryReader reader, ref ObjectHeader header)
        {
            header.DataLength = reader.ReadInt32();
            header.ObjectEnd  = header.DataLength + reader.BaseStream.Position;
            return header.ObjectEnd;
        }

        private void ReadObjectHeader(BinaryReader reader, ref ObjectHeader header)
        {
            ReadDataLength(reader, ref header);
            header.KeyType   = reader.ReadByte();
            header.ValueType = reader.ReadByte();
        }

        internal void ReadObject(BinaryReader reader, ref object target, DeserializationContext ctx)
        {
            ReadObjectHeader(reader, ref ctx.ObjectHeader);
            ReadObject(reader, ref target, target.GetType(), ctx);
        }

        private void ReadObject(BinaryReader reader, ref object target, Type targetType, DeserializationContext ctx)
        {
            var stream    = reader.BaseStream;
            var objectEnd = ctx.ObjectHeader.ObjectEnd;
            var szKeyType = ctx.ObjectHeader.KeyType;
            var members   = target is TypeProxy proxy
                ? _inspector.GetTypeFields(proxy.UnderlyingType ?? targetType, true)
                : _inspector.GetTypeFields(target?.GetType() ?? targetType);

            if (szKeyType != 0 && szKeyType != DataType.String.Id) {
                throw new JesterReadException(
                    $"Deserializing object with 0x{szKeyType:X2} key type into {targetType} is not supported. " +
                    "Use dictionary-like class or custom converted instead"
                );
            }

            while (stream.Position < objectEnd) {
                var dataType = reader.ReadByte();
                var propName = ctx.ReadCString();
                if (members.TryGetValue(propName, out var mdesc)) {
                    ctx.PushProp(propName);
                    object value = ReadValue(reader, dataType, mdesc.MemberType, ctx);
                    try {
                        mdesc.Set(target, value);
                    } catch (Exception e) {
                        throw new JesterReadException($"Failed to set {propName} value: {e.Message}", e);
                    }

                    var popName = ctx.PopProp();
                    if (!Equals(popName, propName)) {
                        throw new JesterReadException($"Unexpected property popped out from the stack: {popName}, expected {propName}");
                    }
                } else {
                    throw new JesterReadException($"Invalid property name {propName} at {stream.Position - ctx.BufferStream.Length}");
                }
            }

            EnsureObjectEnd(stream, objectEnd);
        }

        private void ReadDictionary(BinaryReader reader, ref IDictionary target, Type targetType, IDictionaryConverter conv, DeserializationContext ctx)
        {
            var stream    = reader.BaseStream;
            var objectEnd = ctx.ObjectHeader.ObjectEnd;
            var szKeyType = ctx.ObjectHeader.KeyType;
            var valueType = conv.ValueType;
            var keyType   = conv.KeyType;

            if (szKeyType != 0 && BuiltinTypes.TryGet(szKeyType, out var sourceType)) {
                var sourceKeyType = sourceType.Converter.Type;
                if (keyType.IsAssignableFrom(sourceKeyType)) {
                    keyType = sourceKeyType;
                }
            }

            var keyConvert = keyType != typeof(object) && keyType != typeof(string);

            while (stream.Position < objectEnd) {
                var dataType = reader.ReadByte();
                object propName = ctx.ReadCString();
                if (keyConvert) {
                    try {
                        propName = ((IConvertible) propName).ToType(keyType, CultureInfo.InvariantCulture);
                    } catch (Exception e) {
                        throw new JesterReadException($"Unable to convert key from string to {keyType}: \"{propName}\"", e);
                    }
                }

                ctx.PushProp(propName);
                object value = ReadValue(reader, dataType, valueType, ctx);
                try {
                    target.Add(propName, value);
                } catch (ArgumentException e) when (!valueType.IsInstanceOfType(value)) {
                    throw new JesterReadException($"Incompatible value type: {value.GetType()}, expected {valueType}", e);
                }

                var popName = ctx.PopProp();
                if (!Equals(popName, propName)) {
                    throw new JesterReadException($"Unexpected property popped out from the stack: {popName}, expected {propName}");
                }
            }

            EnsureObjectEnd(stream, objectEnd);
        }

        internal void ReadCollection(BinaryReader reader, ref object target, Type collectionType, DeserializationContext ctx)
        {
            var arrayEnd = ReadDataLength(reader, ref ctx.ObjectHeader);
            var stream   = reader.BaseStream;
            var itemType = reader.ReadByte();

            if (itemType != 0) {
                if (!_inspector.TryGetConverter(itemType, out var conv)) {
                    throw new JesterReadException($"Unexpected collection item type 0x{itemType:X2} at {stream.Position}");
                }

                var targetType = collectionType == typeof(object) ? typeof(List<>).MakeGenericType(conv.Type) : collectionType;
                var factory = _inspector.GetInstanceFactory(targetType) as ICollectionInstanceFactory
                    ?? throw new JesterReadException("Unexpected instance factory type");

                IEnumerable list;
                if (conv is IPrimitiveConverter) {
                    list = ReadTypedCollection<object>(conv, stream, reader, arrayEnd, ctx);
                } else {
                    if (!_readDynamicCollectionMethods.TryGetValue(conv.Type, out var method)) {
                        method = ReadDynamicCollectionMethod.MakeGenericMethod(conv.Type);
                        _readDynamicCollectionMethods.Add(conv.Type, method);
                    }

                    list = (IEnumerable) method.Invoke(this, new object[] { stream, reader, arrayEnd, ctx });
                }

                target = factory.Create(list, reader, ctx);
            } else {
                var targetType = collectionType == typeof(object) ? typeof(List<object>) : collectionType;
                var factory = _inspector.GetInstanceFactory(targetType) as ICollectionInstanceFactory
                    ?? throw new JesterReadException("Unexpected instance factory type");

                var list = ReadDynamicCollection<object>(stream, reader, arrayEnd, ctx);
                target = factory.Create(list, reader, ctx);
            }

            EnsureObjectEnd(stream, arrayEnd);
        }

        internal void ReadCollection<T>(BinaryReader reader, ref object target, Type collectionType, DeserializationContext ctx)
        {
            var arrayEnd = ReadDataLength(reader, ref ctx.ObjectHeader);
            var stream   = reader.BaseStream;
            var itemType = reader.ReadByte();

            if (itemType != 0) {
                if (!_inspector.TryGetConverter(itemType, out var conv)) {
                    throw new JesterReadException($"Unexpected collection item type 0x{itemType:X2} at {stream.Position}");
                }

                if (collectionType.IsArray) {
                    target = conv is IPrimitiveConverter
                        ? ReadTypedCollection<T>(conv, stream, reader, arrayEnd, ctx).ToArray()
                        : ReadDynamicCollection<T>(stream, reader, arrayEnd, ctx).ToArray();
                }
                else {
                    var factory = _inspector.GetInstanceFactory(collectionType) as ICollectionInstanceFactory
                        ?? throw new JesterReadException("Unexpected instance factory type");

                    var list = conv is IPrimitiveConverter
                        ? ReadTypedCollection<T>(conv, stream, reader, arrayEnd, ctx)
                        : ReadDynamicCollection<T>(stream, reader, arrayEnd, ctx);
                    target = factory.Create(list, reader, ctx);
                }
            } else {
                if (collectionType.IsArray) {
                    target = ReadDynamicCollection<T>(stream, reader, arrayEnd, ctx).ToArray();
                }
                else {
                    var factory = _inspector.GetInstanceFactory(collectionType) as ICollectionInstanceFactory
                        ?? throw new JesterReadException("Unexpected instance factory type");

                    var list = ReadDynamicCollection<T>(stream, reader, arrayEnd, ctx);
                    target = factory.Create(list, reader, ctx);
                }
            }

            EnsureObjectEnd(stream, arrayEnd);
        }

        private ICollection<T> ReadTypedCollection<T>(JesterConverter conv, Stream stream, BinaryReader reader, long arrayEnd, DeserializationContext ctx)
        {
            object value = null;
            var list = new List<T>();
            var type = conv.Type;
            var index = 0;
            while (stream.Position < arrayEnd) {
                ctx.PushProp(index);
                conv.Read(reader, ref value, type, ctx);
                list.Add((T) value);

                var poppedIndex = ctx.PopProp();
                if (!Equals(poppedIndex, index)) {
                    throw new JesterReadException($"Unexpected index popped out from the stack: {poppedIndex}, expected {index}");
                }
                index++;
            }

            return list;
        }

        private List<T> ReadDynamicCollection<T>(Stream stream, BinaryReader reader, long arrayEnd, DeserializationContext ctx)
        {
            T value;
            var list = new List<T>();
            var index = 0;
            while (stream.Position < arrayEnd) {
                ctx.PushProp(index);
                value = (T) ReadValue(reader, reader.ReadByte(), typeof(T), ctx);
                list.Add(value);

                var poppedIndex = ctx.PopProp();
                if (!Equals(poppedIndex, index)) {
                    throw new JesterReadException($"Unexpected index popped out from the stack: {poppedIndex}, expected {index}");
                }
                index++;
            }

            return list;
        }


        internal object ReadValue(BinaryReader reader, Type targetType, DeserializationContext ctx)
        {
            var dataType = _inspector.InspectType(targetType);
            return ReadValue(reader, dataType.TypeId, targetType, ctx);
        }

        private object ReadValue(BinaryReader reader, byte dataType, Type targetType, DeserializationContext ctx)
        {
            if (reader.ReadByte() == DataType.NoValue) {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }

            object value = null;

            if (_inspector.TryGetConverter(dataType, out var converter)) {
                ReadViaConverter(converter, reader, ref value, targetType, ctx);
            }
            else if (dataType == DataType.Array.Id) {
                var desc = _inspector.InspectType(targetType);

                // it is use case like this: object NonTypedField = new object[] { 1, 2L, 3F }
                // the field itself is not typed, but it had array-like value during serialization
                if (desc.TypeId == 0) {
                    desc = _inspector.InspectType(typeof(IDynamicList));
                }

                if (desc.TypeId == DataType.Array.Id && desc.Converter != null) {
                    desc.Converter.Read(reader, ref value, targetType, ctx);
                } else {
                    throw new JesterReadException($"No valid converter registered for 0x{dataType:X2} / {targetType}");
                }
            }
            else if (dataType == DataType.Object.Id) {
                ReadObjectHeader(reader, ref ctx.ObjectHeader);

                if (ctx.ObjectHeader.HasGenericInfo) {
                    if (targetType == typeof(object) || targetType == typeof(IDictionary)) {
                        var (keyType, valueType) = ctx.ObjectHeader;
                        var tkey = keyType   != 0 && _inspector.TryGetConverter(keyType,   out var kc) ? kc.Type : typeof(object);
                        var tval = valueType != 0 && _inspector.TryGetConverter(valueType, out var vc) ? vc.Type : typeof(object);
                        targetType = typeof(IDictionary<,>).MakeGenericType(tkey, tval);
                    }
                } else if (targetType == typeof(object)) {
                    targetType = typeof(IDictionary);
                }

                var factory = _inspector.GetInstanceFactory(targetType) as IObjectInstanceFactory
                    ?? throw new JesterReadException("Unexpected instance factory type");

                if (factory.RequiredParams.Count != 0) {
                    var proxy = new TypeProxy(factory.ResultType ?? targetType);
                    value = proxy;

                    ReadObject(reader, ref value, targetType, ctx);

                    var index = 0;
                    var injects = new object[factory.RequiredParams.Count];
                    try {
                        foreach (var param in factory.RequiredParams) {
                            if (param.Type == null) {
                                injects[index++] = proxy.Take(param.Name, param.IsExplicit);
                            }
                            else if (param.Type == typeof(DeserializationContext)) {
                                injects[index++] = ctx;
                            }
                            else {
                                throw new JesterReadException($"Unsupported injection type {param.Type} in {proxy.UnderlyingType}");
                            }
                        }
                    } catch (ArgumentException e) {
                        throw new JesterReadException(e.Message, e);
                    }

                    value = factory.Create(reader, ctx, injects);

                    if (proxy.Values.Count > 0) {
                        var props = _inspector.GetTypeFields(value.GetType());
                        foreach (var item in proxy.Values) {
                            props[item.Key].Set(value, item.Value);
                        }
                    }
                }
                else {
                    value = factory.Create(reader, ctx);

                    if (_inspector.InspectType(value?.GetType() ?? targetType).Converter is IDictionaryConverter conv) {
                        var dict = (IDictionary) value;
                        ReadDictionary(reader, ref dict, targetType, conv, ctx);
                    } else {
                        ReadObject(reader, ref value, targetType, ctx);
                    }
                }
            }
            else {
                throw new JesterReadException($"Unexpected data type 0x{dataType:X2} at {reader.BaseStream.Position}");
            }

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureObjectEnd(Stream stream, long objectEnd)
        {
            var readLen = stream.Position - objectEnd;
            if (readLen != 0) {
                if (readLen > 0) {
                    throw new JesterReadException($"The input stream has been read beyond object boundaries: {stream.Position} / {objectEnd}");
                } else {
                    throw new JesterReadException($"The input stream was not read until object end boundaries: {stream.Position} / {objectEnd}");
                }
            }
        }


        private interface IDynamicList : IEnumerable
        {
        }
    }

    public class DeserializationContext : IDisposable
    {
        public int  DataLength => ObjectHeader.DataLength;
        public long DataEnd    => ObjectHeader.ObjectEnd;

        internal BinaryReader Reader { get; }
        internal Deserializer Deserializer { get; }
        internal MemoryStream BufferStream { get; } = new MemoryStream(256);
        internal BinaryWriter BufferWriter { get; }
        internal ObjectHeader ObjectHeader;

        private readonly Stack<object> _path = new Stack<object>();

        internal DeserializationContext(BinaryReader reader, Deserializer deserializer)
        {
            Reader       = reader;
            Deserializer = deserializer;
            BufferWriter = new BinaryWriter(BufferStream, Encoding.UTF8, false);
        }

        public void Dispose() => BufferWriter?.Dispose();

        public string ReadCString()
        {
            byte readByte;

            BufferStream.SetLength(0);
            while (0 != (readByte = Reader.ReadByte())) {
                BufferWriter.Write(readByte);
            }

#if UNITY_2020_1_OR_NEWER
            return Encoding.UTF8.GetString(BufferStream.GetBuffer(), 0, (int) BufferStream.Length);
#else
            return Encoding.UTF8.GetString(BufferStream.GetBuffer().AsSpan(0, (int) BufferStream.Length));
#endif
        }

        public void Read<T>(ref T target)
            => target = (T) Deserializer.ReadValue(Reader, typeof(T), this);

        public void Read(ref object target, Type targetType)
            => target = Deserializer.ReadValue(Reader, targetType, this);

        public void ReadFields(ref object target)
            => Deserializer.ReadObject(Reader, ref target, this);

        internal void PushProp(object name) => _path.Push(name);

        internal object PopProp() => _path.Pop();
    }

    internal struct ObjectHeader
    {
        public int DataLength;
        public long ObjectEnd;
        public byte KeyType;
        public byte ValueType;

        public bool HasGenericInfo => (KeyType | ValueType) != 0;

        public void Deconstruct(out byte keyType, out byte valueType)
        {
            keyType   = KeyType;
            valueType = ValueType;
        }
    }
}
