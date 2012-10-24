﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FreePIE.Core.Common;
using FreePIE.Core.Contracts;
using FreePIE.Core.Plugins;
using FreePIE.Core.ScriptEngine.CodeCompletion;
using FreePIE.Core.ScriptEngine.Globals;

namespace FreePIE.Core.ScriptEngine.CSharp
{
    public class CSharpScriptParser : IScriptParser
    {
        private readonly IPluginInvoker pluginInvoker;

        public CSharpScriptParser(IPluginInvoker pluginInvoker)
        {
            this.pluginInvoker = pluginInvoker;
        }

        public IEnumerable<IPlugin> InvokeAndConfigureAllScriptDependantPlugins(string script)
        {
            var pluginTypes = pluginInvoker.ListAllPluginTypes()
                .Select(pt =>
                        new
                        {
                            Name = GlobalsInfo.GetGlobalName(pt),
                            PluginType = pt
                        }
                )
                .Where(info => script.Contains(info.Name))
                .Select(info => info.PluginType).ToList();

            return pluginInvoker.InvokeAndConfigurePlugins(pluginTypes);
        }

        public TokenResult GetTokensFromExpression(string script, int offset)
        {
            return new TokenResult(new [] { new Token(TokenType.Identifier, "hgdrthdrthdrth") }, new Range(0, 0));
        }

        public string PrepareScript(string script, IEnumerable<object> globals)
        {
            return script;
        }
    }
}
