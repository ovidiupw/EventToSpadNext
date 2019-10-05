using Xunit;
using Xunit.Abstractions;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSharpCompilers;

public class ScriptTests
{
    private readonly ITestOutputHelper LOG;

    public ScriptTests(ITestOutputHelper output)
    {
        this.LOG = output;
    }

    [Fact]
    public void TestCompile()
    {
        string code = File.ReadAllText(@"C:\Users\Ovidiu\Desktop\EventsToSpadNext\src\demo-scripts.cs");
        Compiler compiler = new SPADNextCompiler(@"E:\SPAD.neXt\SPAD.Interfaces.dll");
        CSharpCompilers.Compilation compilation = compiler.Compile(code);
        IEnumerable<Diagnostic> errors = compilation.GetErrors();

        if (errors.Count() > 0)
        {
            foreach (Diagnostic diagnostic in errors)
            {
                LOG.WriteLine($"\t{diagnostic.Id}: {diagnostic.GetMessage()}");
            }
            Assert.True(false, "Code compilation failed. See the test output logs for details.");
        }
    }
}
