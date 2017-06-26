using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Akari.Web.Controllers
{
    [Route("api/[controller]")]
    public class CSharpController : Controller
    {
        private static readonly Evaluator _evaluator = new Evaluator();

        [HttpPost]
        public async Task<IActionResult> Evaluate([FromBody]string script)
        {
            try
            {
                return Ok(await _evaluator.Evaluate(script));
            }
            catch(CompilationErrorException ex)
            {
                return Ok(string.Join("\r\n", ex.Diagnostics));
            }
            catch(Exception ex)
            {
                return Ok(ex.ToString());
            }
        }

        class Evaluator
        {
            private readonly ActionBlock<(string, TaskCompletionSource<string>)> _frames;

            public Evaluator()
            {
                _frames = new ActionBlock<(string, TaskCompletionSource<string>)>(OnEvaluate, new ExecutionDataflowBlockOptions
                {
                    
                });
            }

            private async Task OnEvaluate((string script, TaskCompletionSource<string> tcs) arg)
            {
                try
                {
                    arg.tcs.SetResult(await EvaluateCore(arg.script));
                }
                catch (Exception ex)
                {
                    arg.tcs.SetException(ex);
                }
            }

            private static readonly ScriptOptions _scriptOptions = ScriptOptions.Default
                .WithReferences("System.Runtime", "System.Collections", "System.Linq")
                .WithImports("System", "System.Collections.Generic", "System.Linq", "System.Threading.Tasks");

            private async Task<string> EvaluateCore(string code)
            {
                var script = CSharpScript.Create(code, _scriptOptions);
                var state = await script.RunAsync();
                return state.ReturnValue?.ToString() ?? "<N/A>";
            }

            public async Task<string> Evaluate(string script)
            {
                var tcs = new TaskCompletionSource<string>();
                await _frames.SendAsync((script, tcs));
                return await tcs.Task;
            }
        }
    }
}
