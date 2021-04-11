using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;

namespace Null.Valuation
{
    /// <summary>
    /// State object interface for valuations to interact with.
    /// Should be copied everywhere in valuation.
    /// </summary>
    /// <typeparam name="CRTP_Child">CRTP-style child, inherited from this interface</typeparam>
    internal interface IStateObject<CRTP_Child> where CRTP_Child : class, IStateObject<CRTP_Child>
    {
        /// <summary>
        /// Clones this state, so that it is possible to mutate the copy without affecting the original.
        /// </summary>
        /// <returns>The copied state.</returns>
        CRTP_Child Clone();

        /// <summary>
        /// Merges (mutates) this state with some other state.
        /// Behaviour of result object of <c>lhs.Merge(rhs)</code> must be the same as <code>rhs.Merge(lhs)</c>.
        /// </summary>
        /// <param name="other">Other state to merge with.</param>
        void Merge(CRTP_Child other);

        /// <summary>
        /// Clears this state. When being merged, the cleared state must produce an object with
        /// the same behaviour, as the other mergee.
        /// </summary>
        void Clear();
    }

    /// <summary>
    /// Context for valuation. Should be passed everywhere in valuation as the same instance.
    /// </summary>
    internal class ValuationContextBase
    {
        internal readonly SyntaxNodeAnalysisContext context;

        /// <summary>
        /// Creates new valuation context from syntax node analysis context.
        /// </summary>
        /// <param name="context">Syntax node analysis context</param>
        internal ValuationContextBase(SyntaxNodeAnalysisContext context)
        {
            this.context = context;
        }
    }

    /// <summary>
    /// Interface for object that provides optional valuation of an expression,
    /// while maintaining a state with additional information.
    /// </summary>
    /// <typeparam name="ResT">Expression result type</typeparam>
    /// <typeparam name="ValuationContext">Valuation context type</typeparam>
    /// <typeparam name="StateObject">Valuation state object type</typeparam>
    internal interface IValuationProvider<ResT, ValuationContext, StateObject>
        where ResT : struct
        where ValuationContext : ValuationContextBase
        where StateObject : class, IStateObject<StateObject>
    {
        /// <summary>
        /// Tries to valuate some expression. Returns the valuated expression (and optionally puts some information
        /// in the state) or returns <c>null</c> if valuation did not succeed (and clears the state).
        /// </summary>
        /// <param name="valContext">Valuation context</param>
        /// <param name="expression">Expression to valuate</param>
        /// <param name="state">The state that might be mutated when valuation succeeded
        /// and must be cleared when valuation failed</param>
        /// <returns>Valuation or <c>null</c>, when it failed.</returns>
        ResT? Valuate(ValuationContext valContext, ExpressionSyntax expression, StateObject state);
    }
}
