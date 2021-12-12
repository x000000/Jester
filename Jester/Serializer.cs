using System.IO;

namespace x0.Jester
{
    public class Serializer
    {
    }

    public class SerializationContext
    {
        public BinaryWriter Writer { get; }

        internal Serializer Serializer { get; }

        internal SerializationContext(BinaryWriter writer, Serializer serializer)
        {
            Writer = writer;
            Serializer = serializer;
        }
    }
}
