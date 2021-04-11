using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Null.Valuation.NullCheck
{
    /// <summary>
    /// Valuation provider for a param null-check inside a method call.
    /// </summary>
    class CallValuationProvider : IValuationProvider<bool, NullCheckContext, NullCheckState>
    {
        /// <summary>
        /// Returns <c>MethodDeclarationSyntax</c> instance, associated with this invocation,
        /// if it can be found (<c>null</c> otherwise).
        /// </summary>
        /// <param name="valContext">Valuation context</param>
        /// <param name="invocation">Invocation to process</param>
        /// <returns><c>MethodDeclarationSyntax</c> instance, associated with this invocation,
        /// if it can be found, and <c>null</c> otherwise.</returns>
        private MethodDeclarationSyntax GetFunctionDefinition(
            NullCheckContext valContext,
            InvocationExpressionSyntax invocation
        )
        {
            // tries to find method definition with this definition
            MethodDeclarationSyntax declToDef(SyntaxNode syntax)
            {
                Debug.Assert(syntax.IsKind(SyntaxKind.MethodDeclaration));
                var methodSyntax = (MethodDeclarationSyntax)syntax;
                return methodSyntax.Body != null || methodSyntax.ExpressionBody != null ? methodSyntax : null;
                //     ^^^  codeblock body  ^^^          ^^^  arrow body  ^^^
            }

            var context = valContext.context;
            if (!(context.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol is IMethodSymbol methodSymbol) ||
                !SymbolEqualityComparer.Default.Equals(
                    methodSymbol.ReturnType,
                    context.Compilation.GetSpecialType(SpecialType.System_Boolean)
                ))
            {
                return null; // either not a method symbol or does not return bool
            }

            // found method invocation that returns bool. return the found definition.
            return
                (from declaration in methodSymbol.DeclaringSyntaxReferences
                 where declToDef(declaration.GetSyntax()) != null
                 select declToDef(declaration.GetSyntax())).FirstOrDefault();
        }

        /// <summary>
        /// Checks if expression is a call to a method that has
        /// a null-check of reference-typed function parameters (contained in a filtering set,
        /// if it is provided) as its body and stores the parameters' symbols into state.
        /// Otherwise (or if method's body cannot be found) clears the state and returns <c>null</c>.
        /// </summary>
        /// <param name="valContext">Valuation context</param>
        /// <param name="expression">Expression to check</param>
        /// <param name="state">State to mutate</param>
        /// <returns>If the expression is a call to a method that has a null-check of
        /// reference-typed function parameters, the result is valuation of the method (<c>param != null</c>),
        /// and <c>null</c> otherwise or if method's body cannot be accessed.</returns>
        public bool? Valuate(NullCheckContext valContext, ExpressionSyntax expression, NullCheckState state)
        {
            if (!state.HasFilters)
            {
                state.Clear();
                return null; // everything is filtered out. no point in continuing.
            }

            if (!(expression is InvocationExpressionSyntax invocation))
            {
                return null; // expression is not an invocation
            }
            var definition = GetFunctionDefinition(valContext, invocation);
            if (definition == null || definition.SyntaxTree != valContext.context.SemanticModel.SyntaxTree)
            {
                // does not return bool or could not find body in this syntax tree
                return null;
            }

            // create (callee argument, callee parameter) pairs
            var argZipParam = invocation.ArgumentList.Arguments.Zip(
                from param in definition.ParameterList.Parameters
                select valContext.context.SemanticModel.GetDeclaredSymbol(param),
                (arg, param) => new { arg, param }
            );

            // select only callee arguments that are passed through from caller parameters
            var passthruArgsAndParams = from pair in argZipParam
                                        select new
                                        {
                                            arg = ParameterRefValidator.Validate(valContext, pair.arg.Expression, state),
                                            pair.param
                                        };
            passthruArgsAndParams = from pair in passthruArgsAndParams where pair.arg != null select pair;

            // select the callee parameters for filtering
            var passthruParams = from pair in passthruArgsAndParams select pair.param;

            ExpressionSyntax bodyExpr;
            if (definition.Body != null)
            {
                // supports only body consisting of one return statement.
                bodyExpr = (definition.Body.Statements.FirstOrDefault() as ReturnStatementSyntax)?.Expression;
                if (bodyExpr == null)
                {
                    return null;
                }
            }
            else
            {
                bodyExpr = definition.ExpressionBody.Expression;
            }
            // find null check of passed-through caller parameters inside the callee
            var localState = new NullCheckState(new HashSet<IParameterSymbol>(passthruParams));
            var valuation = valContext.valuationProvider.Valuate(valContext, bodyExpr, localState);
            // for each found checked callee parameter find corresponding caller parameter,
            // as correct filtering is already done
            // (this also clears the state if valuation failed)
            state.affectedParams =
                (from el in (from localSymbol in localState.affectedParams
                             select passthruArgsAndParams.First(
                                 pair => SymbolEqualityComparer.Default.Equals(pair.param, localSymbol)
                             ).arg)
                 select el).ToList();
            return valuation;
        }
    }
}
