using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;

namespace Null.Valuation
{
    // recursion counter and guard for simple reentrancy tracking
    internal class DefaultTag { };
    internal abstract class RecursionDepthCounter<Tag>
    {
        internal int recursionDepth = 0;
    }

    internal class RecursionDepthCounterGuard<Tag>
    {
        private readonly RecursionDepthCounter<Tag> instance;

        internal RecursionDepthCounterGuard(RecursionDepthCounter<Tag> enclosing)
        {
            instance = enclosing;
            ++instance.recursionDepth;
        }

        ~RecursionDepthCounterGuard()
        {
            --instance.recursionDepth;
        }
    }

    /// <summary>
    /// Valuation of a boolean logic expression (with <i>logical or</i>, <i>logical and</i> and <i>logical not</i> operations),
    /// that builds up on some other valuation used to valuate leaves of this boolean tree.
    /// </summary>
    /// <typeparam name="ValuationContext">Valuation context type</typeparam>
    /// <typeparam name="StateObject">Valuation state object type</typeparam>
    internal class BoolValuationProvider<ValuationContext, StateObject> :
        RecursionDepthCounter<DefaultTag>, IValuationProvider<bool, ValuationContext, StateObject>
        where ValuationContext : ValuationContextBase
        where StateObject : class, IStateObject<StateObject>
    {
        private static readonly int MAX_RECURSION_DEPTH = 30;
        private readonly IValuationProvider<bool, ValuationContext, StateObject> leafValuation;

        /// <summary>
        /// Creates new boolean logic expression valuation based on leaf valuation.
        /// </summary>
        /// <param name="leafValuation">The leaf valuation provider</param>
        public BoolValuationProvider(IValuationProvider<bool, ValuationContext, StateObject> leafValuation) =>
            this.leafValuation = leafValuation;

        /// <summary>
        /// Checks if expression is a boolean logic expression and valuates it.
        /// </summary>
        /// <param name="valContext">Valuation context</param>
        /// <param name="expression">Expression to check</param>
        /// <param name="state">State to mutate: is cleared on fail, left unchanged on unary operation
        /// and merged with <c>IValuationProvider.Merge</c> of provided valuation on binary operation.</param>
        /// <returns>The valuation result.</returns>
        public bool? Valuate(ValuationContext valContext, ExpressionSyntax expression, StateObject state)
        {
            var guard = new RecursionDepthCounterGuard<DefaultTag>(this);

            if (recursionDepth > MAX_RECURSION_DEPTH)
            {
                state.Clear();
                return null;
            }

            if (expression.IsKind(SyntaxKind.ParenthesizedExpression))
            {
                return Valuate(
                    valContext,
                    ((ParenthesizedExpressionSyntax)expression).Expression,
                    state
                );
            }
            if (expression.IsKind(SyntaxKind.LogicalNotExpression))
            {
                return Valuate(
                    valContext,
                    ((PrefixUnaryExpressionSyntax)expression).Operand,
                    state
                ) ^ true;
            }
            else if (expression.IsKind(SyntaxKind.LogicalAndExpression) ||
                     expression.IsKind(SyntaxKind.LogicalOrExpression))
            {
                var binaryExpr = (BinaryExpressionSyntax)expression;
                // right: copy
                // left: change in-place
                var rightState = state.Clone();
                var left  = Valuate(valContext, binaryExpr.Left, state);
                var right = Valuate(valContext, binaryExpr.Right, rightState);

                if (left  == expression.IsKind(SyntaxKind.LogicalOrExpression) ||
                    right == expression.IsKind(SyntaxKind.LogicalOrExpression) ||
                    left.HasValue && right.HasValue)
                {
                    // result is certain if (false && __) or (true || __) or both operands are certain
                    state.Merge(rightState);
                    return expression.IsKind(SyntaxKind.LogicalAndExpression)
                            ? left & right
                            : left | right; // guaranteed to be non-null;
                }
                return null;
            }
            else
            {
                // tree leaf. check for a simple valuation.
                return leafValuation.Valuate(valContext, expression, state);
            }
        }
    }
}
