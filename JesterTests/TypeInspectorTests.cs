using System;
using System.Collections.Generic;
using NUnit.Framework;
using x0.Jester;
using static x0.JesterTests.TypeInspectorTestsSamples;

namespace x0.JesterTests
{
    public class TypeInspectorTests
    {
        private TypeInspector _inspector;

        [SetUp]
        public void Setup()
        {
            _inspector = new TypeInspector(new SerializerSettings {
                SerializeFilter = JesterSerializeMembers.All,
            });
        }

        public static IEnumerable<object[]> CreateInstanceSource()
        {
            yield return new object[] { typeof(InstanceClass1), false, true };
            yield return new object[] { typeof(InstanceClass2), true,  false };
            yield return new object[] { typeof(InstanceClass3), true,  0 };
            yield return new object[] { typeof(InstanceClass4), true,  1 };
            yield return new object[] { typeof(InstanceClass5), true,  2 };
            yield return new object[] { typeof(InstanceClass6), true,  4 };
        }

        [Test]
        [TestCaseSource(nameof(CreateInstanceSource))]
        public void CreateTest(Type type, bool injectable, object ctorVal)
        {
            var factory  = _inspector.GetInstanceFactory<IObjectInstanceFactory>(type);
            var instance = injectable
                ? factory.Create(null, null, new [] { ctorVal })
                : factory.Create(null, null);

            Assert.IsInstanceOf(type, instance);
            Assert.AreEqual(ctorVal, ((InstanceClass) instance).Value);
        }

        [Test]
        [TestCaseSource(nameof(CreateInstanceSource))]
        public void InjectableTest(Type type, bool injectable, object ctorVal)
        {
            var factory = _inspector.GetInstanceFactory<IObjectInstanceFactory>(type);
            Assert.Throws<NotSupportedException>(() => {
                var _ = injectable
                    ? factory.Create(null, null)
                    : factory.Create(null, null, new [] { ctorVal });
            });
        }

        [Test]
        public void NoDistinctFactoryTest()
        {
            var ex = Assert.Throws<JesterAttributeException>(() => {
                _inspector.GetInstanceFactory<IObjectInstanceFactory>(typeof(InstanceClass7)).Create(null, null);
            });
            Assert.True(ex.Message.StartsWith("Multiple factory methods found for type "));
        }
    }
}
