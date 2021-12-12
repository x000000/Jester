using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace x0.Jester
{
    internal struct FactoryParam
    {
        public string Name;
        public bool IsExplicit;
        public Type Type;
    }

    internal interface IInstanceFactory
    {
    }

    internal interface IObjectInstanceFactory : IInstanceFactory
    {
        IReadOnlyCollection<FactoryParam> RequiredParams { get; }
        Type ResultType { get; }

        object Create(BinaryReader reader, DeserializationContext ctx);
        object Create(BinaryReader reader, DeserializationContext ctx, object[] @params);
    }

    internal interface ICollectionInstanceFactory : IInstanceFactory
    {
        object Create(IEnumerable items, BinaryReader reader, DeserializationContext ctx);
    }

    internal class DefaultInstanceFactory : IObjectInstanceFactory
    {
        public IReadOnlyCollection<FactoryParam> RequiredParams { get; } = Array.Empty<FactoryParam>();
        public Type ResultType { get; }

        private readonly ConstructorInfo _ctor;

        public DefaultInstanceFactory(ConstructorInfo ctor)
        {
            _ctor = ctor;
            ResultType = ctor.DeclaringType;
        }

        public object Create(BinaryReader reader, DeserializationContext ctx) => _ctor.Invoke(null);
        public object Create(BinaryReader reader, DeserializationContext ctx, object[] @params) => throw new NotSupportedException();
    }

    internal class InstanceFactory : IObjectInstanceFactory
    {
        public IReadOnlyCollection<FactoryParam> RequiredParams { get; }
        public Type ResultType { get; }

        private readonly ConstructorInfo _ctor;

        public InstanceFactory(ConstructorInfo ctor, FactoryParam[] @params)
        {
            _ctor = ctor;
            ResultType = ctor.DeclaringType;
            RequiredParams = @params;
        }

        public object Create(BinaryReader reader, DeserializationContext ctx) => throw new NotSupportedException();
        public object Create(BinaryReader reader, DeserializationContext ctx, object[] @params) => _ctor.Invoke(@params);
    }

    internal class CollectionInstanceFactory : ICollectionInstanceFactory
    {
        private readonly ConstructorInfo _ctor;

        public CollectionInstanceFactory(ConstructorInfo ctor) => _ctor = ctor;

        public object Create(IEnumerable items, BinaryReader reader, DeserializationContext ctx)
            => _ctor.Invoke(null, new object[] { items });
    }

    internal class CollectionInstanceFactory<T> : ICollectionInstanceFactory
    {
        private readonly ConstructorInfo _ctor;

        public CollectionInstanceFactory(ConstructorInfo ctor) => _ctor = ctor;

        public object Create(IEnumerable items, BinaryReader reader, DeserializationContext ctx)
        {
            if (_ctor.DeclaringType?.IsAssignableFrom(items.GetType()) == true) {
                return items;
            }

            var rawCollection = _ctor.Invoke(null);
            var collection = (ICollection<T>) rawCollection;
            foreach (T item in items) {
                collection.Add(item);
            }
            return collection;
        }
    }

    internal class StaticInstanceFactory : IObjectInstanceFactory
    {
        public IReadOnlyCollection<FactoryParam> RequiredParams { get; }
        public Type ResultType { get; }

        private readonly MethodInfo _ctor;

        public StaticInstanceFactory(MethodInfo ctor, FactoryParam[] @params, Type resultType)
        {
            _ctor = ctor;
            ResultType = resultType;
            RequiredParams = @params;
        }

        public object Create(BinaryReader reader, DeserializationContext ctx) => throw new NotSupportedException();

        public object Create(BinaryReader reader, DeserializationContext ctx, object[] @params) => _ctor.Invoke(null, @params);
    }

    internal class StaticCollectionInstanceFactory : ICollectionInstanceFactory
    {
        private readonly MethodInfo _ctor;
        private readonly Type _resultType;
        private readonly bool _requireItems;

        public StaticCollectionInstanceFactory(MethodInfo ctor, Type resultType, bool requireItems)
        {
            _ctor = ctor;
            _resultType = resultType;
            _requireItems = requireItems;
        }

        public object Create(IEnumerable items, BinaryReader reader, DeserializationContext ctx)
        {
            if (_requireItems) {
                return _ctor.Invoke(null, new object[] { items, reader, ctx });
            } else {
                return _ctor.Invoke(null, new object[] { reader, ctx });
            }
        }
    }
}
