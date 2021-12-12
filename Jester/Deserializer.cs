using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace x0.Jester
{
    public class Deserializer
    {
    }

    public class DeserializationContext : IDisposable
    {
        public BinaryReader Reader { get; }

        internal Deserializer Deserializer { get; }
        internal MemoryStream BufferStream { get; } = new MemoryStream(256);
        internal BinaryWriter BufferWriter { get; }

        private readonly Stack<object> _path = new Stack<object>();

        internal DeserializationContext(BinaryReader reader, Deserializer deserializer)
        {
            Reader       = reader;
            Deserializer = deserializer;
            BufferWriter = new BinaryWriter(BufferStream, Encoding.UTF8, false);
        }

        public void Dispose() => BufferWriter?.Dispose();

        internal void PushProp(object name) => _path.Push(name);

        internal object PopProp() => _path.Pop();
    }
}
