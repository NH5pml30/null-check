using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Null.Valuation
{
    /// <summary>
    /// Valuation provider for a combination of some other valuation providers.
    /// </summary>
    /// <typeparam name="ValuationContext">Valuation context type</typeparam>
    /// <typeparam name="StateObject">Valuation state object type</typeparam>
    internal class EitherValuationProvider<ResT, ValuationContext, StateObject> :
        IValuationProvider<ResT, ValuationContext, StateObject>
        where ResT : struct
        where ValuationContext : ValuationContextBase
        where StateObject : class, IStateObject<StateObject>
    {
        private readonly List<IValuationProvider<ResT, ValuationContext, StateObject>> providers;

        /// <summary>
        /// Creates a new valuation composition.
        /// </summary>
        /// <param name="providers">The valuation providers to compose</param>
        public EitherValuationProvider(
            IEnumerable<IValuationProvider<ResT, ValuationContext, StateObject>> providers
        ) => this.providers = providers.ToList();

        /// <summary>
        /// Valuates expression with first successful valuation from provided valuation providers.
        /// </summary>
        /// <param name="valContext">Valuation context</param>
        /// <param name="expression">Expression to valuate</param>
        /// <param name="state">Valuation state</param>
        /// <returns>Result valuation, if any</returns>
        public ResT? Valuate(ValuationContext valContext, ExpressionSyntax expression, StateObject state)
        {
            foreach (var provider in providers)
            {
                var res = provider.Valuate(valContext, expression, state);
                if (res.HasValue)
                {
                    return res;
                }
            }
            return null;
        }
    }
}
