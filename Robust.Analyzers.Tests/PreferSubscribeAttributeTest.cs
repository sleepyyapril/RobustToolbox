using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using VerifyCS =
    Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<Robust.Analyzers.PreferSubscribeAttributeAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Robust.Analyzers.Tests;

[Parallelizable(ParallelScope.All | ParallelScope.Fixtures)]
[TestFixture]
[TestOf(typeof(PreferSubscribeAttributeAnalyzer))]
public class PreferSubscribeAttributeTest
{
    private const string SubscribeEventDef = """
        using System;
        namespace Robust.Shared.GameObjects;

        public readonly struct EntityUid;

        public abstract class EntitySystem
        {
            public void SubscribeLocalEvent<T, TEvent>(EntityEventHandler<T> handler,
                   Type[]? before = null, Type[]? after = null)
                   where T : notnull { }
            public void SubscribeNetworkEvent<T, TEvent>(EntityEventHandler<T> handler,
                   Type[]? before = null, Type[]? after = null)
                   where T : notnull { }
        }

        public interface IComponent;
        public interface IComponentState;
        """;

    private const string TestEventDef = """
                                        using System;

                                        namespace Robust.Shared.GameObjects;

                                        public sealed class TestEvent : EntityEventArgs
                                        {
                                            public readonly EntityUid Entity;

                                            public TestEvent(EntityUid entity)
                                            {
                                                Entity = entity;
                                            }
                                        }
                                        """;

    private const string OtherTypeDefs = """
                                         using System;

                                         namespace JetBrains.Annotations
                                         {
                                             public sealed class BaseTypeRequiredAttribute(Type baseType) : Attribute;
                                         }
                                         """;

    private static Task Verifier(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<PreferSubscribeAttributeAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { code } }
        };

        TestHelper.AddEmbeddedSources(test.TestState,
            "Robust.Shared.GameObjects.EntityEvents.cs",
            "Robust.Shared.GameObjects.EventBusAttributes.cs");

        test.TestState.Sources.Add(("EntitySystem.Subscriptions.cs", SubscribeEventDef));
        test.TestState.Sources.Add(("Events.cs", TestEventDef));
        test.TestState.Sources.Add(("Types.cs", OtherTypeDefs));

        test.TestState.ExpectedDiagnostics.AddRange(expected);

        return test.RunAsync();
    }

    [Test]
    public async Task Test()
    {
        const string code = """
            using Robust.Shared.Analyzers;
            using Robust.Shared.GameObjects;

            public sealed class TestComponent: IComponent;

            public sealed class Foo : EntitySystem
            {
                public void Bad()
                {
                    // Subscribing to events warns.
                    SubscribeLocalEvent<TestComponent, TestEvent>(Subscription);
                }

                private void Subscription<TestComponent>(TestComponent component, TestEvent args)
                { }
            }
            """;

        await Verifier(code,
            VerifyCS.Diagnostic(PreferSubscribeAttributeAnalyzer.PreferSubscribeAttributeRule)
                .WithSpan(62, 21, 62, 63)
        );
    }
}
