using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace x0.Jester
{
    public class Serializer
    {
        public const byte Version = 1;

        private readonly TypeInspector _inspector;
        private readonly byte _stringTypeId;

        public Serializer() : this(new SerializerSettings())
        {
        }

        public Serializer(SerializerSettings settings)
        {
            _inspector    = new TypeInspector(settings);
            _stringTypeId = _inspector.InspectType(typeof(string)).TypeId;
        }

        public byte[] Serialize<T>(T source)
        {
            var type = source?.GetType() ?? typeof(T);
            var desc = GetTypeDescriptor(type);

            using (var stream = new MemoryStream(1024)) {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, false)) {
                    var ctx = new SerializationContext(writer, this);

                    writer.Write(Version);
                    writer.Write(desc.TypeId == 0 ? DataType.Object.Id : desc.TypeId);
                    WriteObject(writer, source, type, desc, ctx);

                    return stream.ToArray();
                }
            }
        }

        public void Serialize<T>(T source, Stream stream)
        {
            var type = source?.GetType() ?? typeof(T);
            var desc = GetTypeDescriptor(type);

            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) {
                var ctx = new SerializationContext(writer, this);

                writer.Write(Version);
                writer.Write(desc.TypeId == 0 ? DataType.Object.Id : desc.TypeId);
                WriteObject(writer, source, type, desc, ctx);
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TypeDescriptor GetTypeDescriptor(Type type) => _inspector.InspectType(type);

        internal void WriteObject(BinaryWriter writer, object source, Type type, SerializationContext ctx)
            => WriteObject(writer, source, type, GetTypeDescriptor(type), ctx);

        private void WriteObject(BinaryWriter writer, object source, Type type, TypeDescriptor descriptor, SerializationContext ctx)
        {
            if (descriptor.Converter != null) {
                WriteViaConverter(descriptor.Converter, writer, source, type, ctx);
            }
            else if (source == null) {
                writer.Write((int) 0);
            }
            else {
                WriteObjectFields(writer, source, descriptor, ctx);
            }
        }

        internal void WriteObjectFields(BinaryWriter writer, object source, SerializationContext ctx)
            => WriteObjectFields(writer, source, GetTypeDescriptor(source.GetType()), ctx);

        private void WriteObjectFields(BinaryWriter writer, object source, TypeDescriptor descriptor, SerializationContext ctx)
        {
            writer.Write((int) 0);
            var pos = writer.BaseStream.Position;

            // to be compatible with dict serialization we write key-value types
            writer.Write(_stringTypeId);
            writer.Write((byte) 0);

            foreach (var member in descriptor.Members) {
                var value = member.Get(source);
                if (value == null) {
                    continue;
                }

                var type = value?.GetType() ?? member.MemberType;
                var desc = GetTypeDescriptor(type);

                writer.Write(desc.TypeId == 0 ? DataType.Object.Id : desc.TypeId);
                ctx.WriteCString(member.Name);
                WriteObject(writer, value, type, desc, ctx);
            }

            WriteLength(writer, pos);
        }

        private void WriteViaConverter(JesterConverter conv, BinaryWriter writer, object source, Type type, SerializationContext ctx)
        {
            if (conv.IsFixedSize) {
                conv.Write(writer, source, type, ctx);
            } else {
                writer.Write((int) 0);
                var pos = writer.BaseStream.Position;

                conv.Write(writer, source, type, ctx);
                WriteLength(writer, pos);
            }
        }

        internal void WriteDictionary(BinaryWriter writer, IDictionary source, Type dictType, SerializationContext ctx)
        {
            writer.Write((byte) 0);
            writer.Write((byte) 0);

            if (source != null) {
                foreach (DictionaryEntry entry in source) {
                    WriteDictionaryEntry(writer, entry.Key, entry.Value, ctx);
                }
            }
        }

        internal void WriteDictionary<TK, TV>(BinaryWriter writer, IEnumerable<KeyValuePair<TK, TV>> source, Type dictType, SerializationContext ctx)
        {
            writer.Write(_inspector.InspectType(typeof(TK)).TypeId);
            writer.Write(_inspector.InspectType(typeof(TV)).TypeId);

            if (source != null) {
                foreach (var entry in source) {
                    WriteDictionaryEntry(writer, entry.Key, entry.Value, ctx);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteDictionaryEntry<T>(BinaryWriter writer, object key, T value, SerializationContext ctx)
        {
            var name = (key as IConvertible)?.ToString(CultureInfo.InvariantCulture) ?? key.ToString();
            var type = value?.GetType() ?? typeof(T);
            var desc = GetTypeDescriptor(type);

            writer.Write(desc.TypeId == 0 ? DataType.Object.Id : desc.TypeId);
            ctx.WriteCString(name);
            WriteObject(writer, value, type, desc, ctx);
        }

        internal void WriteCollection(BinaryWriter writer, IEnumerable source, Type collectionType, SerializationContext ctx)
        {
            // that's a bad solution to distinct between empty and null collection, but enough for current needs
            if (source == null) {
                writer.Write(byte.MaxValue);
                return;
            }

            writer.Write((byte) 0);

            foreach (var item in source) {
                WriteCollectionEntry(writer, item, ctx);
            }
        }

        internal void WriteCollection<T>(BinaryWriter writer, IEnumerable<T> source, Type collectionType, SerializationContext ctx)
        {
            // that's a bad solution to distinct between empty and null collection, but enough for current needs
            if (source == null) {
                writer.Write(byte.MaxValue);
                return;
            }

            var itemDesc = GetTypeDescriptor(typeof(T));
            writer.Write(itemDesc.TypeId);

            // it is a primitive type which has a converter defined
            if (itemDesc.Converter is IPrimitiveConverter c && c.IsFixedSize) {
                var conv = itemDesc.Converter;
                var type = conv.Type;
                foreach (var item in source) {
                    conv.Write(writer, item, type, ctx);
                }
            } else {
                foreach (var item in source) {
                    WriteCollectionEntry(writer, item, ctx);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteCollectionEntry<T>(BinaryWriter writer, T value, SerializationContext ctx)
        {
            var type = value?.GetType() ?? typeof(T);
            var desc = GetTypeDescriptor(type);
            writer.Write(desc.TypeId == 0 ? DataType.Object.Id : desc.TypeId);
            WriteObject(writer, value, type, desc, ctx);
        }

        private void WriteLength(BinaryWriter writer, long atPos)
        {
            var currentPos = writer.BaseStream.Position;
            if (currentPos != atPos) {
                writer.BaseStream.Seek(atPos - sizeof(int), SeekOrigin.Begin);
                writer.Write((int) (currentPos - atPos));
                writer.BaseStream.Seek(currentPos, SeekOrigin.Begin);
            }
        }
    }

    public class SerializationContext
    {
        internal BinaryWriter Writer { get; }

        internal Serializer Serializer { get; }

        internal SerializationContext(BinaryWriter writer, Serializer serializer)
        {
            Writer = writer;
            Serializer = serializer;
        }

        public void Write<T>(T value)
            => Serializer.WriteObject(Writer, value, value?.GetType() ?? typeof(T), this);

        public void Write(object value, Type type)
            => Serializer.WriteObject(Writer, value, type, this);

        public void WriteFields(object value)
            => Serializer.WriteObjectFields(Writer, value, this);

        public void WriteCString(string value)
        {
            Writer.Write(Encoding.UTF8.GetBytes(value));
            Writer.Write((byte) 0);
        }
    }
}
