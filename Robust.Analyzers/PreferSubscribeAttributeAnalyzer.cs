using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Robust.Roslyn.Shared;

namespace Robust.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class PreferSubscribeAttributeAnalyzer : DiagnosticAnalyzer
{
    private const string SubscribeLocalEventName = "SubscribeLocalEvent";
    private const string SubscribeNetworkEventName = "SubscribeNetworkEvent";
    private const string SubscribeAllEventName = "SubscribeAllEvent";

    private readonly List<string> _requiredAttributes = [
        "Robust.Shared.Analyzers.SubscribeLocalEventAttribute",
        "Robust.Shared.Analyzers.SubscribeNetworkEventAttribute",
        "Robust.Shared.Analyzers.EventSubscriptionAttribute"
    ];

    public static readonly DiagnosticDescriptor PreferSubscribeAttributeRule = new(
        Diagnostics.IdPreferSubscribeAttributeAnalyzer,
        "Use the subscribe event attributes",
        "{0} should use the attribute available as you are not using event ordering",
        "Usage",
        DiagnosticSeverity.Warning,
        true,
        "Remove the method-based subscription and instead put the relevant attribute above your method."
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [PreferSubscribeAttributeRule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(ctx =>
        {
            var hasAttributes = true;

            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach (var attributeMetadataName in _requiredAttributes)
            {
                var attributeType = ctx.Compilation.GetTypeByMetadataName(attributeMetadataName);

                if (attributeType is null)
                    hasAttributes = false;
            }

            // At least one attribute is missing
            if (!hasAttributes)
                return;

            ctx.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        });
    }

    private void AnalyzeInvocation(OperationAnalysisContext context)
    {
        if (context.Operation is not IInvocationOperation operation)
            return;

        if (operation.Instance is not IMemberReferenceOperation reference)
            return;

        if (operation.TargetMethod is not
            {
                Name: SubscribeLocalEventName or SubscribeNetworkEventName or SubscribeAllEventName,
                TypeArguments: [var handler]
            })
            return;

        context.ReportDiagnostic(Diagnostic.Create(PreferSubscribeAttributeRule,
            operation.Syntax.GetLocation(),
            operation.TargetMethod.Name));
    }
}
