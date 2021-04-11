using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Linq;

namespace Null.Valuation.NullCheck
{
    /// <summary>
    /// Valuation provider for an explicit param null-check.
    /// </summary>
    class EqualsValuationProvider : IValuationProvider<bool, NullCheckContext, NullCheckState>
    {
        /// <summary>
        /// Checks if expression is an explicit function parameter null-check and, if so, valuates it
        /// based on the assumption that parameter is non-null.
        /// </summary>
        /// <param name="valContext">Valuation context</param>
        /// <param name="expression">Expression to valuate</param>
        /// <param name="state">Valuation state</param>
        /// <returns>Expression valuation</returns>
        public bool? Valuate(NullCheckContext valContext, ExpressionSyntax expression, NullCheckState state)
        {
            if (state.filterParams != null && !state.filterParams.Any())
            {
                return null;
            }

            bool IsNullLiteral(ExpressionSyntax expr) =>
                expr.IsKind(SyntaxKind.NullLiteralExpression);

            bool IsParameterRef(NullCheckContext lContext, ExpressionSyntax lExpression, NullCheckState lState)
            {
                var param = ParameterRefValidator.Validate(lContext, lExpression, lState);
                if (param != null)
                {
                    lState.affectedParams.Add(param);
                    return true;
                }
                return false;
            }

            if (!expression.IsKind(SyntaxKind.EqualsExpression) &&
                !expression.IsKind(SyntaxKind.NotEqualsExpression))
            {
                return null; // not <smth> ==(!=) <smth>
            }
            // param != null === true
            bool valuation = expression.IsKind(SyntaxKind.NotEqualsExpression);
            var binaryExpr = (BinaryExpressionSyntax)expression;

            if (IsParameterRef(valContext, binaryExpr.Left, state) &&
                IsNullLiteral(binaryExpr.Right) ||
                IsParameterRef(valContext, binaryExpr.Right, state) &&
                IsNullLiteral(binaryExpr.Left))
            {
                // ok, param ==(!=) null or vice-versa
                return valuation;
            }
            return null;
        }
    }
}
