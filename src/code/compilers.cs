using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

/// <summary>
/// Contains various kinds of C# dynamic code compilers.
/// A dynamic code compiler takes C# code, compiles it and turns it into
/// compiled code. The compiled code can then be run.
/// </summary>
namespace CSharpCompilers
{

    /// <summary>
    /// Defines a C# dynamic code compiler.
    /// </summary>
    public interface Compiler
    {
        /// <summary>
        /// Compiles the given code.
        /// </summary>
        /// <param name="code">The code to be compiled.</param>
        /// <returns>The result of compiling the code (might be ready to run or contain compilation errors).</returns>
        Compilation Compile(string code);
    }

    /// <summary>
    /// Encapsulates a C# compilation and various methods to interact with it.
    /// </summary>
    public class Compilation
    {
        private CSharpCompilation cSharpCompilation;

        /// <summary>
        /// Constructs a new instance of this object.
        /// </summary>
        /// <param name="cSharpCompilation">A C# compilation resulting from compiling code via a Compiler.</param>
        public Compilation(CSharpCompilation cSharpCompilation)
        {
            this.cSharpCompilation = cSharpCompilation;
        }

        /// <summary>
        /// Retrieves the compilation errors for the current compilation.
        /// </summary>
        /// <returns>An enumerable that contains all the compilation errors.
        ///  Enumerable is empty if there were no compilation errors</returns>
        public List<Diagnostic> GetErrors()
        {
            using (var compileStream = new MemoryStream())
            {
                EmitResult result = cSharpCompilation.Emit(compileStream);
                if (!result.Success)
                {
                    return result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error)
                        .ToList();
                }
                else
                {
                    return new List<Diagnostic>();
                }
            }
        }

        /// <summary>
        /// Runs a method in the compiled code based on the given options. If the code does not compile,
        /// it throws an exception. If the method cannot be run or is not found, it throws an exception.
        /// </summary>
        /// <param name="opts">The configuration based on which the method is run.</param>
        /// <returns>The result of invoking the method. If the method is void, the result is set to null.</returns>
        public object Run(RunOptions opts)
        {
            ValidateRunOptions(opts);

            using (var compileStream = new MemoryStream())
            {
                EmitResult result = cSharpCompilation.Emit(compileStream);
                if (!result.Success)
                {
                    throw CompilationError.Build(GetErrors());
                }
                compileStream.Seek(0, SeekOrigin.Begin);
                Assembly assembly = AssemblyLoadContext.Default.LoadFromStream(compileStream);

                string namespaceAndClass = $"{opts.GetNamespace()}.{opts.GetClass()}";

                var type = assembly.GetType(namespaceAndClass);
                if (type == null)
                {
                    throw new RunError(
                        $"Could not find class '{opts.GetClass()}' in namespace '{opts.GetNamespace()}'");
                }

                var members = type.GetMember(opts.GetMethod());
                if (members == null || members.Count() == 0)
                {
                    throw new RunError(
                        $"Could not find method '{opts.GetMethod()}' in class '{opts.GetClass()}' and namespace '{opts.GetNamespace()}'");
                }
                var member = members.First() as MethodInfo;
                if (member == null)
                {
                    throw new RunError(
                        $"Could not find method '{opts.GetMethod()}' in class '{opts.GetClass()}' and namespace '{opts.GetNamespace()}'");
                }

                var instance = assembly.CreateInstance(namespaceAndClass);
                return member.Invoke(instance, opts.GetArgs());
            }
        }

        private void ValidateRunOptions(RunOptions opts)
        {
            if (opts.GetNamespace() == null || opts.GetNamespace().Length == 0)
            {
                throw new RunError("The namespace must not be blank");
            }
            if (opts.GetClass() == null || opts.GetClass().Length == 0)
            {
                throw new RunError("The class must not be blank");
            }
            if (opts.GetMethod() == null || opts.GetMethod().Length == 0)
            {
                throw new RunError("The method must not be blank");
            }
        }

        /// <summary>
        /// The options that configure a method run (i.e. idenitfy the method and its arguments).
        /// </summary>
        public class RunOptions
        {
            /// <summary>
            /// The C# namespace of the method.
            /// </summary>
            private string nspace;

            /// <summary>
            /// The C# class of the method. Must be in the given namespace.
            /// </summary>
            private string clazz;

            /// <summary>
            /// The C# method to invoke. Must be in the given namespace and class.
            /// </summary>
            private string method;

            /// <summary>
            /// The C# arguments that will be passed to the method when invoked.
            /// </summary>
            private object[] args;

            public RunOptions WithNamespace(string nspace)
            {
                this.nspace = nspace;
                return this;
            }

            public string GetNamespace()
            {
                return nspace;
            }

            public RunOptions WithClass(string clazz)
            {
                this.clazz = clazz;
                return this;
            }

            public string GetClass()
            {
                return this.clazz;
            }

            public RunOptions WithMethod(string method)
            {
                this.method = method;
                return this;
            }

            public string GetMethod()
            {
                return this.method;
            }

            public RunOptions WithArgs(object[] args)
            {
                this.args = args;
                if (this.args == null)
                {
                    this.args = new object[] { };
                }
                return this;
            }

            public object[] GetArgs()
            {
                return this.args;
            }
        }
    }

    /// <summary>
    /// Represents an error thrown when the caller tries to act on a Compilation in a way that is unsupported.
    /// For example, an unsupported use-case is when a caller tries to run a method on a compilation which has errors.
    /// </summary>
    public class CompilationError : Exception
    {
        /// <summary>
        /// Builds a new instance of CompilationError by taking the list of diagnostics and including their 
        /// string representation into the error message.
        /// </summary>
        /// <param name="diagnostics">A list of compilation diagnostics (usually errors)</param>
        /// <returns></returns>
        public static CompilationError Build(List<Diagnostic> diagnostics)
        {
            return new CompilationError(String.Concat(diagnostics));
        }

        public CompilationError(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Represents an error thrown when a certain construct in a Compilation cannot be  instantiated or run.
    /// </summary>
    public class RunError : Exception
    {
        public RunError(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Compiles SPAD Next C# scripts.
    /// </summary>
    public class SPADNextCompiler : Compiler
    {
        private string spadInterfacesPath;

        /// <summary>
        /// Constructs a new SPAD Next C# script compiler.
        /// </summary>
        /// <param name="spadInterfacesPath">The path to the DLL that contains the SPAD Next interfaces. 
        /// Usually this can be found in the SPAD Next application folder.</param>
        public SPADNextCompiler(string spadInterfacesPath)
        {
            this.spadInterfacesPath = spadInterfacesPath;
        }

        public Compilation Compile(string code)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code);

            // Get the directory of a core assembly. We need this directory to
            // build out our platform specific reference to mscorlib. mscorlib
            // and the private mscorlib must be supplied as references for
            // compilation to succeed. Of these two assemblies, only the private
            // mscorlib is discovered via enumerataing assemblies referenced by
            // this executing binary.
            var coreDirectory = Directory.GetParent(typeof(Enumerable).GetTypeInfo().Assembly.Location).FullName;

            List<string> refPaths = new List<string> {
                typeof(System.Object).GetTypeInfo().Assembly.Location,
                typeof(Console).GetTypeInfo().Assembly.Location,
                typeof(File).GetTypeInfo().Assembly.Location,
                Path.Combine(coreDirectory, "System.Runtime.dll"),
                Path.Combine(coreDirectory,  "mscorlib.dll"),
                this.spadInterfacesPath
            };

            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithAssemblyIdentityComparer(DesktopAssemblyIdentityComparer.Default);

            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName: Path.GetRandomFileName(),
                syntaxTrees: new[] { syntaxTree },
                references: refPaths
                .Where(r => r.Length > 0)
                .Select(r => MetadataReference.CreateFromFile(r)).ToArray(),
                options: compilationOptions
            );

            return new Compilation(compilation);
        }
    }
}