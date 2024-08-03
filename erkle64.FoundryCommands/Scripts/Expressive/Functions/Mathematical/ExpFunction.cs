﻿using Expressive.Expressions;
using System;

namespace Expressive.Functions.Mathematical
{
    internal class ExpFunction : FunctionBase
    {
        #region FunctionBase Members

        public override string Name { get { return "Exp"; } }

        public override object Evaluate(IExpression[] parameters, Context context)
        {
            this.ValidateParameterCount(parameters, 1, 1);

            return Math.Exp(Convert.ToDouble(parameters[0].Evaluate(Variables)));
        }

        #endregion
    }
}
