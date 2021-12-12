using System;
using System.Collections.Generic;

namespace x0.Jester
{
    public delegate bool ResolveConverter(JesterConverter conv1, JesterConverter conv2);

    public class SerializerSettings
    {
        private readonly Dictionary<byte, JesterConverter> _converters = new Dictionary<byte, JesterConverter>();
        public IReadOnlyDictionary<byte, JesterConverter> Converters => _converters;

        public JesterSerializeMembers SerializeFilter { get; set; } = JesterSerializeMembers.All;

        public ResolveConverter ConverterResolver { get; set; }

        private byte _converterIndex = (byte) (DataType.LastBuiltInType.Id + 1);

        public void AddConverter<T>(JesterConverter<T> converter)
        {
            while (_converterIndex != 0) {
                if (_converters.TryAdd(_converterIndex++, converter)) {
                    return;
                }
            }
            throw new OverflowException("Maximum number of supported types has been reached");
        }

        public void AddConverter<T>(JesterConverter<T> converter, byte typeId)
        {
            if (typeId <= DataType.LastBuiltInType.Id) {
                throw new ArgumentOutOfRangeException($"Types 1-{DataType.LastBuiltInType.Id} are reserved");
            }

            if (_converters.TryAdd(typeId, converter)) {
                return;
            }

            throw new OverflowException($"Type {typeId} is already defined");
        }
    }
}
