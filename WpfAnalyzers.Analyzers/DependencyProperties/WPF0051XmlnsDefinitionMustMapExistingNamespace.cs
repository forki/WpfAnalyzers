﻿namespace WpfAnalyzers.DependencyProperties
{
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    internal class WPF0051XmlnsDefinitionMustMapExistingNamespace : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "WPF0051";
        private const string Title = "XmlnsDefinition must map to existing namespace.";
        private const string MessageFormat = "[XmlnsDefinition] maps to '{0}' that does not exist.";
        private const string Description = "XmlnsDefinition must map to existing namespace.";
        private static readonly string HelpLink = WpfAnalyzers.HelpLink.ForId(DiagnosticId);

        private static readonly DiagnosticDescriptor Descriptor = new DiagnosticDescriptor(
                                                                      DiagnosticId,
                                                                      Title,
                                                                      MessageFormat,
                                                                      AnalyzerCategory.DependencyProperties,
                                                                      DiagnosticSeverity.Error,
                                                                      AnalyzerConstants.EnabledByDefault,
                                                                      Description,
                                                                      HelpLink);

        /// <inheritdoc/>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Descriptor);

        /// <inheritdoc/>
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(HandleDeclaration, SyntaxKind.Attribute);
        }

        private static void HandleDeclaration(SyntaxNodeAnalysisContext context)
        {
            var attributeSyntax = context.Node as AttributeSyntax;
            if (attributeSyntax == null ||
                attributeSyntax.IsMissing)
            {
                return;
            }

            AttributeSyntax attribute;
            string @namespace;
            AttributeArgumentSyntax arg;
            if (Attribute.TryGetAttribute(attributeSyntax, KnownSymbol.XmlnsDefinitionAttribute, context.SemanticModel, context.CancellationToken, out attribute) &&
                Attribute.TryGetArgumentStringValue(attributeSyntax, 1, context.SemanticModel, context.CancellationToken, out arg, out @namespace))
            {
                if (context.Compilation.GetSymbolsWithName(x => !string.IsNullOrEmpty(x) && @namespace.EndsWith(x), SymbolFilter.Namespace)
                            .All(x => x.ToMinimalDisplayString(context.SemanticModel, 0) != @namespace))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Descriptor, arg.GetLocation(), arg));
                }
            }
        }
    }
}
