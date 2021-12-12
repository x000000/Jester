using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("JesterTests")]
namespace x0.Jester
{
    using TMemberKV = KeyValuePair<string, IMemberDescriptor>;
    using TFactoryCache = Dictionary<Type, (MethodInfo Method, bool Explicit, bool RequireItems)>;

    internal class TypeInspector
    {
        private readonly Dictionary<Type, MemberCollection> _memberProxyCache = new Dictionary<Type, MemberCollection>();
        private readonly Dictionary<Type, MemberCollection> _memberCache = new Dictionary<Type, MemberCollection>();
        private readonly Dictionary<Type, IInstanceFactory> _factoryCache = new Dictionary<Type, IInstanceFactory>();
        private readonly Dictionary<Type, TFactoryCache> _factoryInspectCache = new Dictionary<Type, TFactoryCache>();
        private readonly JesterSerializeMembers _serializeFilter;
        private readonly ResolveConverter _resolveConverter;

        private readonly Dictionary<Type, TypeDescriptor> _inspectCache = BuiltinTypes.List.ToDictionary(d => d.Converter.Type);
        private readonly JesterConverter _arrayConverter      = new ArrayConverter();
        private readonly JesterConverter _dictionaryConverter = new DictionaryConverter();

        private readonly Dictionary<byte, JesterConverter> _customConverters;

        private readonly Dictionary<Type, (int priority, Type type)> _defaultImpls = new Dictionary<Type, (int, Type)> {
            [typeof(IDictionary<,>)]         = (30, typeof(Dictionary<,>)),
            [typeof(IReadOnlyDictionary<,>)] = (30, typeof(Dictionary<,>)),
            [typeof(ISet<>)]                 = (20, typeof(HashSet<>)),
            [typeof(IList<>)]                = (10, typeof(List<>)),
            [typeof(IReadOnlyList<>)]        = (10, typeof(List<>)),
            [typeof(ICollection<>)]          = (10, typeof(List<>)),
            [typeof(IReadOnlyCollection<>)]  = (10, typeof(List<>)),
            [typeof(IEnumerable<>)]          = (10, typeof(List<>)),
            [typeof(IDictionary)]            = (3,  typeof(Dictionary<object, object>)),
            [typeof(IList)]                  = (1,  typeof(List<object>)),
            [typeof(ICollection)]            = (1,  typeof(List<object>)),
            [typeof(IEnumerable)]            = (1,  typeof(List<object>)),
        };

        public TypeInspector(SerializerSettings settings)
        {
            _serializeFilter  = settings.SerializeFilter;
            _resolveConverter = settings.ConverterResolver ?? ThrowingConverterResolver;
            _customConverters = new Dictionary<byte, JesterConverter>(settings.Converters);
        }

        private bool ThrowingConverterResolver(JesterConverter a, JesterConverter b)
            => throw new InvalidOperationException("Merger is not configured");

        public bool TryGetConverter(byte typeId, out JesterConverter converter)
        {
            if (typeId <= DataType.LastBuiltInTypeId) {
                if (BuiltinTypes.TryGet(typeId, out var descriptor)) {
                    converter = descriptor.Converter;
                    return true;
                }
            } else {
                return _customConverters.TryGetValue(typeId, out converter);
            }
            converter = null;
            return false;
        }

        public MemberCollection GetTypeFields(Type type) => GetTypeFields(type, false);

        internal MemberCollection GetTypeFields(Type type, bool proxy)
        {
            MemberCollection result;

            if (proxy) {
                if (!_memberProxyCache.TryGetValue(type, out result)) {
                    var typeMembers = GetTypeFields(type, false);
                    var membersByName = typeMembers.Values.ToDictionary(
                        d => d.Name,
                        d => (IMemberDescriptor) new MemberWrapperDescriptor(d)
                    );
                    _memberProxyCache[type] = result = new MemberCollection(membersByName.Values.ToList(), membersByName);
                }
            }
            else if (!_memberCache.TryGetValue(type, out result)) {
                var attr = type.GetCustomAttribute<JesterSerializeAttribute>(true);
                var serializeFields = (attr?.Serialize ?? _serializeFilter).HasFlag(JesterSerializeMembers.Fields);
                var serializeProps  = (attr?.Serialize ?? _serializeFilter).HasFlag(JesterSerializeMembers.Properties);

                var orderDefined = false;
                var members = new Dictionary<IMemberDescriptor, int>();
                var membersByName = new Dictionary<string, IMemberDescriptor>();

                foreach (var mi in type.GetMembers(BindingFlags.Instance | BindingFlags.Public)) {
                    JesterPropertyAttribute prop;
                    MemberDescriptor descriptor;

                    if (mi is FieldInfo fi) {
                        prop = fi.GetCustomAttribute<JesterPropertyAttribute>();
                        if (prop == null && !serializeFields) {
                            continue;
                        }
                        descriptor = new FieldDescriptor(fi, prop);
                    }
                    else if (mi is PropertyInfo pi) {
                        prop = pi.GetCustomAttribute<JesterPropertyAttribute>();
                        if (prop == null && !serializeProps) {
                            continue;
                        }
                        descriptor = new PropertyDescriptor(pi, prop);
                    }
                    else {
                        continue;
                    }

                    if (prop?.Ignore == true) {
                        continue;
                    }

                    var order = prop?.Order ?? 0;
                    members.Add(descriptor, order);
                    membersByName.Add(descriptor.Name, descriptor);
                    if (!orderDefined && order != 0) {
                        orderDefined = true;
                    }
                }

                _memberCache[type] = result = orderDefined
                    ? new MemberCollection(members.OrderBy(o => o.Value).Select(o => o.Key).ToList(), membersByName)
                    : new MemberCollection(members.Keys.ToList(), membersByName);
            }

            return result;
        }

        public TypeDescriptor InspectType(Type type)
        {
            if (!_inspectCache.TryGetValue(type, out var props)) {
                _inspectCache[type] = props = InspectTypeInternal(type);
            }
            return props;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TypeDescriptor InspectTypeInternal(Type type)
        {
            if (type.IsPrimitive) {
                throw new ArgumentOutOfRangeException($"Unexpected value type: {type}");
            }

            Type dictType1 = null;
            Type dictType2 = null;
            Type arrayType = null;
            byte typeId = 0;

            JesterConverter converter = null;
            foreach (var iface in FlattenType(type)) {
                foreach (var entry in _customConverters) {
                    if (entry.Value.Type.IsAssignableFrom(iface)) {
                        if (converter == null || (
                            converter != entry.Value && typeId != entry.Key && _resolveConverter(converter, entry.Value)
                        )) {
                            typeId    = entry.Key;
                            converter = entry.Value;
                        }
                    }
                }

                if (converter == null && dictType1 == null && iface.IsGenericType) {
                    var genType = iface.GetGenericTypeDefinition();
                    if (genType == typeof(IDictionary<,>)) {
                        dictType1 = iface;
                    } else if (genType == typeof(IReadOnlyDictionary<,>)) {
                        dictType2 = iface;
                    } else if (genType == typeof(IEnumerable<>)) {
                        arrayType = iface;
                    }
                }
            }

            if (converter != null) {
                return new TypeDescriptor(typeId, converter, converter is IMembersAware ? GetTypeFields(type) : null);
            }

            if (dictType1 != null || dictType2 != null || typeof(IDictionary).IsAssignableFrom(type)) {
                if (dictType1 != null || dictType2 != null) {
                    var genArgs  = (dictType1 ?? dictType2).GenericTypeArguments;
                    var convType = typeof(DictionaryConverter<,>).MakeGenericType(genArgs);
                    converter = (JesterConverter) Activator.CreateInstance(convType);

                    return new TypeDescriptor(DataType.Object.Id, converter);
                } else {
                    return new TypeDescriptor(DataType.Object.Id, _dictionaryConverter);
                }
            }
            if (arrayType != null) {
                var genArgs  = arrayType.GenericTypeArguments;
                var convType = typeof(ArrayConverter<>).MakeGenericType(genArgs);
                converter = (JesterConverter) Activator.CreateInstance(convType);

                return new TypeDescriptor(DataType.Array.Id, converter);
            }
            if (typeof(IEnumerable).IsAssignableFrom(type)) {
                return new TypeDescriptor(DataType.Array.Id, _arrayConverter);
            }

            return new TypeDescriptor(GetTypeFields(type));
        }

        public IEnumerable<Type> FlattenType(Type type)
        {
            var result  = new List<Type>();
            var visited = new HashSet<Type>();

            do {
                result.Add(type);

                foreach (var iface in type.GetInterfaces()) {
                    if (visited.Add(iface)) {
                        result.Add(iface);
                    }
                }
            } while (null != (type = type.BaseType));

            return result;
        }

        public T GetInstanceFactory<T>(Type type) where T : IInstanceFactory
            => (T) GetInstanceFactory(type);

        public IInstanceFactory GetInstanceFactory(Type type)
        {
            if (!_factoryCache.TryGetValue(type, out var factory)) {
                _factoryCache[type] = factory = GetInstanceFactoryInternal(type);
            }
            return factory;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IInstanceFactory GetInstanceFactoryInternal(Type type)
        {
            var descriptor = InspectType(type);

            var requireItems = false;
            var factoryAttr = type.GetCustomAttribute<JesterFactoryAttribute>(false);
            if (factoryAttr != null) {
                var factories = InspectFactory(factoryAttr.FactoryClass);
                if (factories.TryGetValue(type, out var tuple)) {
                    return descriptor.TypeId == DataType.Array.Id
                        ? new StaticCollectionInstanceFactory(tuple.Method, type, tuple.RequireItems)
                        : (IInstanceFactory) new StaticInstanceFactory(tuple.Method, GetParams(tuple.Method, true), type);
                }

                MethodInfo factory = null;
                Type factoryType = null;
                foreach (var (returnType, (method, isExplicit, reqItems)) in factories) {
                    if (!isExplicit && type.IsAssignableFrom(method.ReturnType)) {
                        factory = factory == null
                            ? method
                            : throw new JesterAttributeException($"Multiple factory methods found for type {type}");
                        factoryType  = returnType;
                        requireItems = reqItems;
                    }
                }

                if (factory == null) {
                    throw new JesterAttributeException($"No factory method found for type {type}");
                }

                return descriptor.TypeId == DataType.Array.Id
                    ? new StaticCollectionInstanceFactory(factory, factoryType, requireItems)
                    : (IInstanceFactory) new StaticInstanceFactory(factory, GetParams(factory, true), factoryType);
            }

            ConstructorInfo ctor = null;
            ConstructorInfo defaultCtor = null;

            var ctors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public/* | BindingFlags.NonPublic*/);
            foreach (var ctorInfo in ctors) {
                if (ctorInfo.GetParameters().Length == 0) {
                    defaultCtor = ctorInfo;
                }

                var attr = ctorInfo.GetCustomAttribute<JesterCreatorAttribute>();
                if (attr != null) {
                    ctor = ctor == null
                        ? ctorInfo
                        : throw new JesterAttributeException($"Type misconfiguration: multiple constructors found in {type}");
                }
            }

            if (ctor != null && ctor != defaultCtor) {
                if (descriptor.TypeId == DataType.Array.Id) {
                    var list = ctor.GetParameters();
                    if (list.Length == 1 && list[0].GetCustomAttribute<JesterItemsAttribute>() != null) {
                        return new CollectionInstanceFactory(ctor);
                    }
                }

                return new InstanceFactory(ctor, GetParams(ctor));
            }

            if (ctor == null) {
                if (defaultCtor != null) {
                    ctor = defaultCtor;
                } else {
                    if (type.IsInterface) {
                        (int priority, bool generic, Type iface, Type type) impl = (-1, false, null, null);
                        foreach (var iface in FlattenType(type)) {
                            if (iface.IsGenericType) {
                                var getType = iface.GetGenericTypeDefinition();
                                if (_defaultImpls.TryGetValue(getType, out var val) && val.priority > impl.priority) {
                                    impl = (val.priority, true, iface, val.type);
                                }
                            } else {
                                if (_defaultImpls.TryGetValue(iface, out var val) && val.priority > impl.priority) {
                                    impl = (val.priority, false, iface, val.type);
                                }
                            }
                        }

                        if (impl.priority != -1) {
                            return impl.generic
                                ? GetInstanceFactory(impl.type.MakeGenericType(impl.iface.GenericTypeArguments))
                                : GetInstanceFactory(impl.type);
                        }
                    } else if (type.IsAbstract) {
                        throw new JesterAttributeException($"Type misconfiguration: abstract types are not supported");
                    }
                    throw new JesterAttributeException($"Type misconfiguration: no valid constructor found in {type}");
                }
            }

            return descriptor.TypeId == DataType.Array.Id
                ? CreateCollectionFactory(ctor)
                : new DefaultInstanceFactory(ctor);
        }

        private FactoryParam[] GetParams(MethodBase method, bool allowContext = false)
        {
            var list   = method.GetParameters();
            var result = new FactoryParam[list.Length];

            for (var i = 0; i < list.Length; i++) {
                var param = list[i];

                var attr = param.GetCustomAttribute<JesterPropertyBindingAttribute>();
                var type = param.ParameterType == typeof(DeserializationContext)
                    ? (allowContext
                        ? typeof(DeserializationContext)
                        : throw new JesterAttributeException($"Type misconfiguration: context parameter is not allowed in {method}")
                    )
                    : null;

                result[i] = new FactoryParam {
                    Name = attr?.Name ?? param.Name,
                    IsExplicit = attr?.Name != null,
                    Type = type,
                };
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IInstanceFactory CreateCollectionFactory(ConstructorInfo ctor)
        {
            var typeArg = FlattenType(ctor.DeclaringType)
                .First(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ICollection<>));
            var genType = typeof(CollectionInstanceFactory<>).MakeGenericType(typeArg.GenericTypeArguments[0]);
            var genCtor = genType.GetConstructor(new [] { typeof(ConstructorInfo) });
            return (IInstanceFactory) genCtor?.Invoke(new object[] { ctor });
        }

        private TFactoryCache InspectFactory(Type type)
        {
            if (!_factoryInspectCache.TryGetValue(type, out var result)) {
                result = new TFactoryCache();
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
                foreach (var method in methods) {
                    var attr = method.GetCustomAttribute<JesterCreatorAttribute>();
                    if (attr != null) {
                        if (method.IsGenericMethod) {
                            throw new JesterAttributeException($"Generic factory methods are not supported: {method.Name}");
                        }

                        var targetType = attr.Type ?? method.ReturnType;
                        if (!result.TryAdd(targetType, (method, attr.Type != null, attr.RequireItems))) {
                            throw new JesterAttributeException($"Multiple factory methods found for type {targetType}");
                        }
                    }
                }
                _factoryInspectCache[type] = result;
            }
            return result;
        }
    }

    internal class TypeProxy
    {
        public Type UnderlyingType { get; }

        public IReadOnlyDictionary<string, object> Values => _values;

        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();
        private readonly Dictionary<string, string> _keys   = new Dictionary<string, string>();

        public TypeProxy(Type underlyingType) => UnderlyingType = underlyingType;

        public void Add(string key, object value)
        {
            _values.Add(key, value);
            _keys.Add(key.ToLowerInvariant(), key);
        }

        public object Take(string key, bool isExplicit)
        {
            if (isExplicit) {
                var result = _values[key];
                _values.Remove(key);
                _keys.Remove(key.ToLowerInvariant());
                return result;
            }

            var keyLc = key.ToLowerInvariant();
            if (_keys.TryGetValue(keyLc, out var k)) {
                var result = _values[k];
                _values.Remove(k);
                _keys.Remove(keyLc);
                return result;
            }

            throw new ArgumentException($"Property '{key}' not found");
        }
    }

    internal class MemberCollection : IReadOnlyList<IMemberDescriptor>, IReadOnlyDictionary<string, IMemberDescriptor>
    {
        private readonly IReadOnlyList<IMemberDescriptor> _members;
        private readonly IReadOnlyDictionary<string, IMemberDescriptor> _membersDict;

        public MemberCollection(
            IReadOnlyList<IMemberDescriptor> members,
            IReadOnlyDictionary<string, IMemberDescriptor> membersDict
        )
        {
            _members     = members;
            _membersDict = membersDict;
        }

        #region IReadOnlyDictionary

        public IEnumerable<string> Keys => _membersDict.Keys;

        public IEnumerable<IMemberDescriptor> Values => _membersDict.Values;

        public IMemberDescriptor this[string index] => _membersDict[index];

        public bool ContainsKey(string key) => _membersDict.ContainsKey(key);

        public bool TryGetValue(string key, out IMemberDescriptor value) => _membersDict.TryGetValue(key, out value);

        IEnumerator<TMemberKV> IEnumerable<TMemberKV>.GetEnumerator() => _membersDict.GetEnumerator();

        #endregion

        #region IReadOnlyList

        public int Count => _members.Count;

        public IMemberDescriptor this[int index] => _members[index];

        public IEnumerator<IMemberDescriptor> GetEnumerator() => _members.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_members).GetEnumerator();

        #endregion
    }

    internal interface IMemberDescriptor
    {
        string MemberName { get; }
        string Name { get; }

        Type MemberType { get; }
        Type Type { get; }

        object Get(object source);
        void Set(object target, object val);
    }

    internal abstract class MemberDescriptor : IMemberDescriptor
    {
        public string MemberName { get; }
        public string Name { get; }

        public Type MemberType { get; }
        public Type Type { get; }

        protected MemberDescriptor(string name, Type type, JesterPropertyAttribute attr)
        {
            MemberType = type;
            MemberName = name;

            Name = attr?.Name ?? name;
            Type = attr?.WriteAs;
        }

        public abstract object Get(object source);
        public abstract void Set(object target, object val);

        public override string ToString() => $"{Name} ({MemberName})";
    }

    internal class FieldDescriptor : MemberDescriptor
    {
        private readonly FieldInfo _fieldInfo;

        public FieldDescriptor(FieldInfo fi, JesterPropertyAttribute attr) : base(fi.Name, fi.FieldType, attr)
            => _fieldInfo = fi;

        public override object Get(object source) => _fieldInfo.GetValue(source);

        public override void Set(object target, object val) => _fieldInfo.SetValue(target, val);
    }

    internal class PropertyDescriptor : MemberDescriptor
    {
        private readonly PropertyInfo _propInfo;

        public PropertyDescriptor(PropertyInfo pi, JesterPropertyAttribute attr) : base(pi.Name, pi.PropertyType, attr)
            => _propInfo = pi;

        public override object Get(object source) => _propInfo.GetValue(source);

        public override void Set(object target, object val) => _propInfo.SetValue(target, val);
    }

    internal class MemberWrapperDescriptor : IMemberDescriptor
    {
        public string MemberName => _underlyingMember.MemberName;
        public string Name       => _underlyingMember.Name;

        public Type MemberType => _underlyingMember.MemberType;
        public Type Type       => _underlyingMember.Type;

        private readonly IMemberDescriptor _underlyingMember;

        public MemberWrapperDescriptor(IMemberDescriptor underlyingMember)
        {
            _underlyingMember = underlyingMember;
        }

        public object Get(object source) => throw new NotSupportedException();

        public void Set(object target, object val) => ((TypeProxy) target).Add(Name, val);
    }

    internal class TypeDescriptor
    {
        public byte TypeId { get; }

        public JesterConverter Converter { get; }

        public MemberCollection Members { get; }

        public TypeDescriptor(MemberCollection members) => Members = members;

        public TypeDescriptor(byte typeId, JesterConverter converter, MemberCollection members = null)
            : this(members)
        {
            TypeId    = typeId;
            Converter = converter;
        }
    }
}
