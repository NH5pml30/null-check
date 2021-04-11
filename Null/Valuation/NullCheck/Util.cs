using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Null.Valuation.NullCheck
{
    /// <summary>
    /// Valuation state for param null-check. Contains the parameters being checked and a filtering set for computing them.
    /// </summary>
    internal class NullCheckState : IStateObject<NullCheckState>
    {
        internal List<IParameterSymbol> affectedParams;
        internal readonly HashSet<IParameterSymbol> filterParams;

        /// <summary>
        /// Creates new valuation state for param null-check.
        /// </summary>
        /// <param name="affectedParams">Parameters, affected by null-check</param>
        /// <param name="filterParams">Parameters to filter through</param>
        private NullCheckState(
            List<IParameterSymbol> affectedParams,
            HashSet<IParameterSymbol> filterParams
        )
        {
            this.affectedParams = affectedParams;
            this.filterParams = filterParams;
        }

        /// <summary>
        /// Creates new valuation state for param null-check.
        /// </summary>
        /// <param name="filterParams">Parameters to filter through (optional)</param>
        internal NullCheckState(HashSet<IParameterSymbol> filterParams = null) :
            this(new List<IParameterSymbol>(), filterParams)
        {
        }

        /// <summary>
        /// Clears the state.
        /// </summary>
        public void Clear()
        {
            affectedParams.Clear();
        }

        /// <summary>
        /// Clones the state.
        /// </summary>
        public NullCheckState Clone()
        {
            return new NullCheckState(
                new List<IParameterSymbol>(affectedParams),
                filterParams == null ? null : new HashSet<IParameterSymbol>(filterParams)
            );
        }

        /// <summary>
        /// Merges two states.
        /// </summary>
        public void Merge(NullCheckState other)
        {
            affectedParams.AddRange(other.affectedParams);
        }

        /// <summary>
        /// Returns false iff there are no params to filter through, meaning
        /// that further param null-check search is pointless.
        /// </summary>
        internal bool HasFilters
        {
            get { return filterParams == null || filterParams.Any(); }
        }

        /// <summary>
        /// Returns true iff the specified symbol passes through the filter.
        /// </summary>
        /// <param name="symbol">Parameter symbol to check</param>
        /// <returns>true iff the specified symbol passes through the filter.</returns>
        internal bool PassesFilter(IParameterSymbol symbol)
        {
            return filterParams == null || filterParams.Contains(symbol);
        }
    }

    /// <summary>
    /// Valuation context for param null-check. Contains valuation provider to recurse into.
    /// </summary>
    internal class NullCheckContext : ValuationContextBase
    {
        internal readonly IValuationProvider<bool, NullCheckContext, NullCheckState> valuationProvider;

        /// <summary>
        /// Creates new valuation context for param null-check.
        /// </summary>
        /// <param name="context">Syntax node analysis context</param>
        /// <param name="valuationProvider">Valuation provider to recurse into</param>
        internal NullCheckContext(
            SyntaxNodeAnalysisContext context,
            IValuationProvider<bool, NullCheckContext, NullCheckState> valuationProvider
        ) : base(context)
        {
            this.valuationProvider = valuationProvider;
        }
    }

    /// <summary>
    /// Helper class, containing code for checking wether the provided expression is a reference-typed parameter name.
    /// </summary>
    internal abstract class ParameterRefValidator
    {
        /// <summary>
        /// Checks if expression is a reference-typed parameter name
        /// (contained in a filtering set, if it is provided) and returns the parameter's symbol.
        /// Otherwise returns <c>null</c>.
        /// </summary>
        /// <param name="valContext">Valuation context</param>
        /// <param name="expression">Expression to check for being a paremeter</param>
        /// <param name="state">State with context and parameter filtering set (optional)</param>
        /// <returns>If the expression is a reference-typed parameter name,
        /// the result is an associated with it parameter symbol, and <c>null</c> otherwise.
        /// </returns>
        static internal IParameterSymbol Validate(
            NullCheckContext valContext,
            ExpressionSyntax expression,
            NullCheckState state
        )
        {
            if (!state.HasFilters)
            {
                return null;
            }

            var nameInfo = valContext.context.SemanticModel.GetSymbolInfo(expression);
            if (!(nameInfo.Symbol is IParameterSymbol paramSymbol) ||
                paramSymbol.Type.IsValueType || !state.PassesFilter(paramSymbol))
            {
                return null; // not parameter, not reference or filtered out
            }
            return paramSymbol;
        }
    }
}
