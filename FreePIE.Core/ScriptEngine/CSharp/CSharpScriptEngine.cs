using System.ComponentModel;
using System.Dynamic;
using System.Threading;
using FreePIE.Core.Common.Extensions;
using FreePIE.Core.Contracts;
using FreePIE.Core.ScriptEngine.Globals;
using Roslyn.Compilers;
using Roslyn.Scripting;

namespace FreePIE.Core.ScriptEngine.CSharp
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Roslyn.Scripting.CSharp;

    public class CSharpScriptEngine : IScriptEngine
    {
        private readonly IScriptParser parser;
        private readonly IEnumerable<IGlobalProvider> globalProviders;
        private readonly ScriptEngine engine;
        private IEnumerable<IPlugin> usedPlugins;
        private bool stopRequested;
        private const int LoopDelay = 1;

        public CSharpScriptEngine(IScriptParser parser, IEnumerable<IGlobalProvider> globalProviders)
        {
            this.parser = parser;
            this.globalProviders = globalProviders;
            engine = new ScriptEngine();
            engine.AddReference(typeof(PluginHost).Assembly);
            engine.AddReference(typeof(IPlugin).Assembly);
        }

        public void Start(string script)
        {
            ThreadPool.QueueUserWorkItem(x => ExecuteSafe(() =>
                {
                    usedPlugins = parser.InvokeAndConfigureAllScriptDependantPlugins(script).ToList();

                    var host = new PluginHost {starting = true};

                    usedPlugins.ForEach(StartPlugin);

                    var session = InitSession(usedPlugins, host);

                    var method = GenerateMethodFor(session, script);

                    while (!stopRequested)
                    {
                        usedPlugins.ForEach(p => p.DoBeforeNextExecute());
                        method.Execute();
                        host.starting = false;
                    }
                }));
        }

        void ExecuteSafe(Action action)
        {
            try
            {
                action();
            } catch(Exception e)
            {
                TriggerErrorEventNotOnLuaThread(e);
            }
        }

        private void TriggerErrorEventNotOnLuaThread(Exception e)
        {
            if (e.InnerException != null)
            {
                TriggerErrorEventNotOnLuaThread(e.InnerException);
                return;
            }

            OnError(this, new ScriptErrorEventArgs(e));
        }

        private void OnError(object sender, ScriptErrorEventArgs e)
        {
            if (Error != null)
                Error(sender, e);
        }

        private Submission<object> GenerateMethodFor(Session session, string script)
        {
            try
            {
                return session.CompileSubmission<object>(script);
            } catch(CompilationErrorException exception)
            {
                var errors = exception.Diagnostics.Aggregate(string.Empty, (x, y) => x + y + Environment.NewLine);
                throw new Exception(errors.TrimEnd( Environment.NewLine.ToCharArray()));
            }
        }

        Session InitSession(IEnumerable<IPlugin> plugins, PluginHost host)
        {
            var globals = globalProviders.SelectMany(gp => gp.ListGlobals()).ToList();

            host.__globals = plugins.ToDictionary(GlobalsInfo.GetGlobalName, x => x.CreateGlobal())
                                    .Union(globals.ToDictionary(GlobalsInfo.GetGlobalName, x => x))
                                    .ToDictionary(x => x.Key, x => x.Value);

            var globalsAssemblies = host.__globals.Values.Select(x => x.GetType().Assembly);

            foreach (var assembly in globalsAssemblies)
                engine.AddReference(assembly);

            var namespaces = host.__globals.Select(x => x.Value.GetType().Namespace).Distinct();

            foreach (var @namespace in namespaces)
                engine.ImportNamespace(@namespace);

            var session = engine.CreateSession(host);

            foreach (var global in host.__globals)
                session.Execute(string.Format("var {0} = ({1})__globals[\"{0}\"];", global.Key, global.Value.GetType().Name));

            return session;
        }

        private void StartPlugin(IPlugin plugin)
        {
            plugin.Start();
        }

        public void Stop()
        {
            stopRequested = true;
            Thread.Sleep(100);
            usedPlugins.ForEach(p => p.Stop());
        }

        public event EventHandler<ScriptErrorEventArgs> Error;
    }

    public class PluginHost
    {
        public bool starting { get; set; }
        public Dictionary<string, object> __globals = new Dictionary<string, object>(); 
    }
}
