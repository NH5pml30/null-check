using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Null
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NullCodeFixProvider)), Shared]
    public class NullCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(NullAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var isInverted = bool.Parse(diagnostic.Properties["isInverted"]);

            var ifStatement = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<IfStatementSyntax>().First();

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.CodeFixTitle,
                    createChangedDocument: c => RemoveCheckAsync(context.Document, ifStatement, isInverted, c),
                    equivalenceKey: nameof(CodeFixResources.CodeFixTitle)),
                diagnostic);
        }

        private async Task<Document> RemoveCheckAsync(Document document, IfStatementSyntax ifStatement,
                                                      bool isInverted, CancellationToken cancellationToken)
        {
            StatementSyntax formatStatement(StatementSyntax statement) =>
                statement.WithAdditionalAnnotations(Formatter.Annotation);

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            // leave the 'not null' branch in code and remove the whole if
            StatementSyntax toInsert = isInverted ? ifStatement.Statement : ifStatement.Else?.Statement;
            if (toInsert != null)
            {
                // replace the whole if with one of the branches
                if (ifStatement.Parent != null &&
                    ifStatement.Parent.IsKind(SyntaxKind.Block) &&
                    toInsert.IsKind(SyntaxKind.Block))
                {
                    // the branch is a block and parent is a block => can remove inner block
                    root = root.ReplaceNode(ifStatement, new SyntaxList<StatementSyntax>(
                        ((BlockSyntax)toInsert).Statements.Select(formatStatement)
                    ));
                }
                else
                {
                    // paste the branch as-is
                    root = root.ReplaceNode(ifStatement, formatStatement(toInsert));
                }
            }
            else
            {
                // nothing to insert, just remove if altogether
                root = root.RemoveNode(ifStatement, SyntaxRemoveOptions.KeepNoTrivia);
            }

            return document.WithSyntaxRoot(root);
        }
    }
}
