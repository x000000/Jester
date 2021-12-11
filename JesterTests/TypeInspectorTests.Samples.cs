using System.Diagnostics.CodeAnalysis;
using x0.Jester;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedType.Local

namespace x0.JesterTests
{
    [ExcludeFromCodeCoverage]
    public static class TypeInspectorTestsSamples
    {
        public class InstanceClass
        {
            public object Value { get; protected set; }
        }

        public class InstanceClass1 : InstanceClass
        {
            public InstanceClass1() => Value = true;
            public InstanceClass1(bool x) => Value = false;
        }

        public class InstanceClass2 : InstanceClass
        {
            public InstanceClass2() {}

            [JesterCreator]
            public InstanceClass2(bool x) => Value = x;
        }

        [JesterFactory(typeof(InstanceFactory))]
        public class InstanceClass3 : InstanceClass
        {
            public InstanceClass3() {}
            public InstanceClass3(object x) => Value = x;
        }

        [JesterFactory(typeof(InstanceFactory))]
        public class InstanceClass4 : InstanceClass
        {
            public InstanceClass4() {}
            public InstanceClass4(object x) => Value = x;
        }

        [JesterFactory(typeof(InstanceFactory))]
        public class InstanceClass5 : InstanceClass
        {
            public InstanceClass5() {}
            public InstanceClass5(object x) => Value = x;
        }

        public class InstanceClass51 : InstanceClass5
        {
            public InstanceClass51() {}
            public InstanceClass51(object x) => Value = x;
        }

        [JesterFactory(typeof(InstanceFactory))]
        public class InstanceClass6 : InstanceClass
        {
            public InstanceClass6() {}
            public InstanceClass6(object x) => Value = x;
        }

        public class InstanceClass61 : InstanceClass6
        {
            public InstanceClass61() {}
            public InstanceClass61(object x) => Value = x;
        }

        public class InstanceClass62 : InstanceClass6
        {
            public InstanceClass62() {}
            public InstanceClass62(object x) => Value = x;
        }

        [JesterFactory(typeof(InstanceFactory))]
        public class InstanceClass7 : InstanceClass
        {
            public InstanceClass7() {}
            public InstanceClass7(object x) => Value = x;
        }

        public class InstanceClass71 : InstanceClass7
        {
            public InstanceClass71() { }
            public InstanceClass71(object x) : base(x) { }
        }

        public class InstanceClass72 : InstanceClass7
        {
            public InstanceClass72() { }
            public InstanceClass72(object x) : base(x) { }
        }

        private static class InstanceFactory
        {
            [JesterCreator]
            public static InstanceClass3 CreateInstanceClass3(object value) => new InstanceClass3(value);

            [JesterCreator(Type = typeof(InstanceClass4))]
            public static InstanceClass CreateInstanceClass4(object value) => new InstanceClass4(value);

            [JesterCreator]
            public static InstanceClass51 CreateInstanceClass5(object value) => new InstanceClass51(value);

            [JesterCreator(Type = typeof(InstanceClass61))]
            public static InstanceClass61 CreateInstanceClass61(object value) => new InstanceClass61(value);

            [JesterCreator]
            public static InstanceClass62 CreateInstanceClass62(object value) => new InstanceClass62(value);

            [JesterCreator]
            public static InstanceClass71 CreateInstanceClass5_1(object value) => new InstanceClass71(value);

            [JesterCreator]
            public static InstanceClass72 CreateInstanceClass5_2(object value) => new InstanceClass72(value);
        }
    }
}
