using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Null.Valuation;
using Null.Valuation.NullCheck;

namespace Null
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NullAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "Null";

        private static readonly LocalizableString Title =
            new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat =
            new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager,
                typeof(Resources));
        private static readonly LocalizableString Description =
            new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager,
                typeof(Resources));
        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning,
                isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.IfStatement);
        }

        private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var ifStatement = (IfStatementSyntax)context.Node;

            bool? valuation = ValidateParamNullCheck(context, ifStatement.Condition, out var paramSymbols);
            if (valuation.HasValue)
            {
                var diagnostic = Diagnostic.Create(
                    Rule, context.Node.GetLocation(),
                    new Dictionary<string, string>(){ { "isInverted", valuation.ToString() } }
                        .ToImmutableDictionary(),
                    string.Join(", ", from symbol in paramSymbols select symbol.Name)
                );
                context.ReportDiagnostic(diagnostic);
            }
        }

        /// <summary>
        /// Checks if a boolean expression is resolved, knowing that function parameters
        /// (from an optional filtering set, if provided) are non-<c>null</c>.
        /// Might return the valuation of this expression, if that is the case, otherwise returns <c>null</c>.
        /// </summary>
        /// <param name="context">Syntax node analysis context</param>
        /// <param name="expression">Expression to check</param>
        /// <param name="affectedParams">Outputs parameters being compared to null</param>
        /// <returns>If a boolean expression is resolved, knowing that function parameters are non-<c>null</c>,
        /// might return its valuation. Otherwise returns <c>null</c>.</returns>
        static internal bool? ValidateParamNullCheck(
            SyntaxNodeAnalysisContext context, ExpressionSyntax expression,
            out List<IParameterSymbol> affectedParams
        )
        {
            var provider = new BoolValuationProvider<NullCheckContext, NullCheckState>(
                new EitherValuationProvider<bool, NullCheckContext, NullCheckState>(
                    new IValuationProvider<bool, NullCheckContext, NullCheckState>[]
                    {
                        new CallValuationProvider(), new EqualsValuationProvider()
                    }
                )
            );
            var valContext = new NullCheckContext(context, provider);
            var state = new NullCheckState();
            var res = provider.Valuate(valContext, expression, state);
            affectedParams = state.affectedParams;
            return res;
        }
    }
}
