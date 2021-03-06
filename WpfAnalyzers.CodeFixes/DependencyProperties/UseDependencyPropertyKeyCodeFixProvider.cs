﻿namespace WpfAnalyzers.DependencyProperties
{
    using System.Collections.Immutable;
    using System.Composition;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CodeActions;
    using Microsoft.CodeAnalysis.CodeFixes;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseDependencyPropertyKeyCodeFixProvider))]
    [Shared]
    internal class UseDependencyPropertyKeyCodeFixProvider : CodeFixProvider
    {
        /// <inheritdoc/>
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(WPF0040SetUsingDependencyPropertyKey.DiagnosticId);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var document = context.Document;
            var syntaxRoot = await document.GetSyntaxRootAsync(context.CancellationToken)
                .ConfigureAwait(false);

            var semanticModel = await document.GetSemanticModelAsync(context.CancellationToken)
                                              .ConfigureAwait(false);
            foreach (var diagnostic in context.Diagnostics)
            {
                var token = syntaxRoot.FindToken(diagnostic.Location.SourceSpan.Start);
                if (string.IsNullOrEmpty(token.ValueText))
                {
                    continue;
                }

                var invocation = syntaxRoot.FindNode(diagnostic.Location.SourceSpan)
                                           .FirstAncestorOrSelf<InvocationExpressionSyntax>();

                if (invocation == null || invocation.IsMissing)
                {
                    continue;
                }

                ArgumentSyntax property;
                IFieldSymbol setField;
                ArgumentSyntax value;
                if (DependencyObject.TryGetSetValueArguments(
                      invocation,
                      semanticModel,
                      context.CancellationToken,
                      out property,
                      out setField,
                      out value))
                {
                    IFieldSymbol keyField;
                    if (DependencyProperty.TryGetDependencyPropertyKeyField(
                        setField,
                        semanticModel,
                        context.CancellationToken,
                        out keyField))
                    {
                        var keyArg = DependencyProperty.CreateArgument(keyField, semanticModel, token.SpanStart);
                        var setValue = invocation.ReplaceNode(property, keyArg);
                        context.RegisterCodeFix(
                            CodeAction.Create(
                                invocation.ToString(),
                                cancellationToken => ApplyFixAsync(
                                    context,
                                    cancellationToken,
                                    invocation,
                                    setValue),
                                nameof(MakeFieldStaticReadonlyCodeFixProvider)),
                            diagnostic);
                    }

                    continue;
                }

                if (DependencyObject.TryGetSetCurrentValueArguments(
                      invocation,
                      semanticModel,
                      context.CancellationToken,
                      out property,
                      out setField,
                      out value))
                {
                    IFieldSymbol keyField;
                    if (DependencyProperty.TryGetDependencyPropertyKeyField(
                        setField,
                        semanticModel,
                        context.CancellationToken,
                        out keyField))
                    {
                        var keyArg = DependencyProperty.CreateArgument(keyField, semanticModel, token.SpanStart);
                        var setValue = invocation.WithExpression(SetValueExpression(invocation.Expression))
                                                 .ReplaceNode(property, keyArg);
                        context.RegisterCodeFix(
                            CodeAction.Create(
                                invocation.ToString(),
                                cancellationToken => ApplyFixAsync(
                                    context,
                                    cancellationToken,
                                    invocation,
                                    setValue),
                                nameof(MakeFieldStaticReadonlyCodeFixProvider)),
                            diagnostic);
                    }
                }
            }
        }

        private static async Task<Document> ApplyFixAsync(
            CodeFixContext context,
            CancellationToken cancellationToken,
            SyntaxNode oldNode,
            SyntaxNode newNode)
        {
            var syntaxRoot = await context.Document.GetSyntaxRootAsync(cancellationToken)
                                          .ConfigureAwait(false);
            return context.Document.WithSyntaxRoot(syntaxRoot.ReplaceNode(oldNode, newNode));
        }

        private static ExpressionSyntax SetValueExpression(ExpressionSyntax old)
        {
            var identifierNameSyntax = old as IdentifierNameSyntax;
            if (identifierNameSyntax != null)
            {
                return identifierNameSyntax.Identifier.ValueText == "SetCurrentValue"
                           ? identifierNameSyntax.WithIdentifier(SyntaxFactory.Identifier("SetValue"))
                           : identifierNameSyntax;
            }

            var memberAccessExpressionSyntax = old as MemberAccessExpressionSyntax;
            if (memberAccessExpressionSyntax != null)
            {
                var newName = SetValueExpression(memberAccessExpressionSyntax.Name) as SimpleNameSyntax;
                return memberAccessExpressionSyntax.WithName(newName);
            }

            return old;
        }
    }
}