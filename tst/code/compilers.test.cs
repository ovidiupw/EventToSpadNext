using Xunit;
using Xunit.Abstractions;
using CSharpCompilers;
using System;

public class SPADNextCompilationTests
{
    private readonly ITestOutputHelper LOG;
    private readonly Compiler compiler;

    public SPADNextCompilationTests(ITestOutputHelper output)
    {
        this.LOG = output;
        compiler = new SPADNextCompiler(string.Empty);
    }

    [Fact]
    public void When_EmptyCodeSupplied_Then_CompilationSucceeds()
    {
        CSharpCompilers.Compilation compilation = compiler.Compile(string.Empty);
        Assert.Empty(compilation.GetErrors());
    }

    [Fact]
    public void When_ValidCodeSupplied_Then_CompilationSucceeds()
    {
        CSharpCompilers.Compilation compilation = compiler.Compile(TestUtils.CODE);
        Assert.Empty(compilation.GetErrors());
    }

    [Fact]
    public void When_InvalidCodeSupplied_Then_CompilationFails()
    {
        string code = @"usi ng System;";
        CSharpCompilers.Compilation compilation = compiler.Compile(code);
        Assert.NotEmpty(compilation.GetErrors());
    }

    [Fact]
    public void When_InvalidCodeSupplied_Then_CompilationErrorsAreAccurate()
    {
        string code = @"usi ng System;";
        CSharpCompilers.Compilation compilation = compiler.Compile(code);
        Assert.Equal(4, compilation.GetErrors().ToArray().Length);
        Assert.True(compilation.GetErrors().Exists(e => e.Id == "CS1002"));
        Assert.True(compilation.GetErrors().Exists(e => e.Id == "CS0116"));
        Assert.True(compilation.GetErrors().Exists(e => e.Id == "CS1022"));
        Assert.True(compilation.GetErrors().Exists(e => e.Id == "CS0246"));
    }

    [Fact]
    public void When_CompilationErrors_And_RunCalled_Then_ExceptionThrown()
    {
        string code = @"usi ng System;";
        CSharpCompilers.Compilation compilation = compiler.Compile(code);
        var e = Assert.Throws<CompilationError>(() => compilation.Run(
            new Compilation.RunOptions().WithNamespace("A").WithClass("B").WithMethod("C")));
        Assert.Equal(e.Message, String.Concat(compilation.GetErrors()));
    }

    [Fact]
    public void When_NamespaceNull_Then_RunFails()
    {
        CSharpCompilers.Compilation compilation = compiler.Compile(TestUtils.CODE);
        var e = Assert.Throws<RunError>(() => compilation.Run(new Compilation.RunOptions()));
        Assert.Equal(e.Message, "The namespace must not be blank");
    }

    [Fact]
    public void When_NamespaceEmpty_Then_RunFails()
    {
        CSharpCompilers.Compilation compilation = compiler.Compile(TestUtils.CODE);
        var e = Assert.Throws<RunError>(() => compilation.Run(
            new Compilation.RunOptions()
            .WithNamespace(string.Empty)));
        Assert.Equal(e.Message, "The namespace must not be blank");
    }

    [Fact]
    public void When_ClassNull_Then_RunFails()
    {
        CSharpCompilers.Compilation compilation = compiler.Compile(TestUtils.CODE);
        var e = Assert.Throws<RunError>(() => compilation.Run(
            new Compilation.RunOptions()
            .WithNamespace(TestUtils.NAMESPACE)));
        Assert.Equal(e.Message, "The class must not be blank");
    }

    [Fact]
    public void When_ClassEmpty_Then_RunFails()
    {
        CSharpCompilers.Compilation compilation = compiler.Compile(TestUtils.CODE);
        var e = Assert.Throws<RunError>(() => compilation.Run(
            new Compilation.RunOptions()
            .WithNamespace(TestUtils.NAMESPACE)
            .WithClass(string.Empty)));
        Assert.Equal(e.Message, "The class must not be blank");
    }

    [Fact]
    public void When_MethodNull_Then_RunFails()
    {
        CSharpCompilers.Compilation compilation = compiler.Compile(TestUtils.CODE);
        var e = Assert.Throws<RunError>(() => compilation.Run(
            new Compilation.RunOptions()
            .WithNamespace(TestUtils.NAMESPACE)
            .WithClass(TestUtils.CLASS)));
        Assert.Equal(e.Message, "The method must not be blank");
    }

    [Fact]
    public void When_MethodEmpty_Then_RunFails()
    {
        CSharpCompilers.Compilation compilation = compiler.Compile(TestUtils.CODE);
        var e = Assert.Throws<RunError>(() => compilation.Run(
            new Compilation.RunOptions()
            .WithNamespace(TestUtils.NAMESPACE)
            .WithClass(TestUtils.CLASS)
            .WithMethod(string.Empty)));
        Assert.Equal(e.Message, "The method must not be blank");
    }

    [Fact]
    public void When_NamespaceDoesNotExist_Then_RunFails()
    {
        CSharpCompilers.Compilation compilation = compiler.Compile(TestUtils.CODE);
        var runOpts = TestUtils.builRunOpts().WithNamespace("NonExistentNamespace");
        var e = Assert.Throws<RunError>(() => compilation.Run(runOpts));
        Assert.Equal(e.Message,
        $"Could not find class '{runOpts.GetClass()}' in namespace 'NonExistentNamespace'");
    }

    [Fact]
    public void When_ClassDoesNotExist_Then_RunFails()
    {
        CSharpCompilers.Compilation compilation = compiler.Compile(TestUtils.CODE);
        var runOpts = TestUtils.builRunOpts().WithClass("NonExistentClass");
        var e = Assert.Throws<RunError>(() => compilation.Run(runOpts));
        Assert.Equal(e.Message,
        $"Could not find class 'NonExistentClass' in namespace '{runOpts.GetNamespace()}'");
    }

    [Fact]
    public void When_MethodDoesNotExist_Then_RunFails()
    {
        CSharpCompilers.Compilation compilation = compiler.Compile(TestUtils.CODE);
        var runOpts = TestUtils.builRunOpts().WithMethod("NonExistentMethod");
        var e = Assert.Throws<RunError>(() => compilation.Run(runOpts));
        Assert.Equal(e.Message,
        $"Could not find method 'NonExistentMethod' in class '{runOpts.GetClass()}' and namespace '{runOpts.GetNamespace()}'");
    }

    [Fact]
    public void When_FieldWithSameNameExists_ButNotMethod_Then_RunFails()
    {
        CSharpCompilers.Compilation compilation = compiler.Compile(TestUtils.CODE);
        var runOpts = TestUtils.builRunOpts().WithMethod(TestUtils.FIELD);
        var e = Assert.Throws<RunError>(() => compilation.Run(runOpts));
        Assert.Equal(e.Message,
        $"Could not find method '{TestUtils.FIELD}' in class '{runOpts.GetClass()}' and namespace '{runOpts.GetNamespace()}'");
    }

    [Fact]
    public void When_VoidMethod_And_NoArgs_Then_RunSucceeds()
    {
        CSharpCompilers.Compilation compilation = compiler.Compile(TestUtils.CODE);
        var runOpts = TestUtils.builRunOpts().WithMethod(TestUtils.VOID_METHOD_NO_ARGS);
        var result = compilation.Run(runOpts);
        Assert.Null(result);
    }

    [Fact]
    public void When_NonVoidMethod_And_NoArgs_Then_RunSucceeds()
    {
        CSharpCompilers.Compilation compilation = compiler.Compile(TestUtils.CODE);
        var runOpts = TestUtils.builRunOpts().WithMethod(TestUtils.NON_VOID_METHOD_NO_ARGS);
        var result = compilation.Run(runOpts);
        Assert.Equal(result, TestUtils.METHOD_RETURN_VALUE);
    }

    [Fact]
    public void When_MethodHasArgs_Then_RunSucceeds()
    {
        CSharpCompilers.Compilation compilation = compiler.Compile(TestUtils.CODE);

        string firstArg = "abcd";
        string secondArg = "-1234";

        var runOpts = TestUtils.builRunOpts()
        .WithMethod(TestUtils.METHOD_WITH_ARGS)
        .WithArgs(new string[] {firstArg, secondArg});
        var result = compilation.Run(runOpts);
        Assert.Equal(result, firstArg + secondArg);
    }
}

public class TestUtils
{
    public static string NAMESPACE = "TestNamespace";
    public static string CLASS = "TestClass";
    public static string VOID_METHOD_NO_ARGS = "VoidTestMethod";
    public static string NON_VOID_METHOD_NO_ARGS = "NonVoidTestMethod";
    public static string METHOD_WITH_ARGS = "TestMethodWithArgs";
    public static string METHOD_RETURN_VALUE = "this is a test";
    public static string FIELD = "testField";

    public static string CODE = @"
            using System;
            namespace TestNamespace
            {
                public class TestClass
                {
                    public string testField;

                    public void VoidTestMethod()
                    {
                        var a = 1;
                        a++;
                    }

                    public string NonVoidTestMethod()
                    {
                        return ""this is a test"";
                    }

                    public string TestMethodWithArgs(string firstArg, string secondArg)
                    {
                        return firstArg + secondArg;
                    }
                }
            }";

    public static Compilation.RunOptions builRunOpts()
    {
        return new Compilation.RunOptions()
        .WithNamespace(NAMESPACE)
        .WithClass(CLASS)
        .WithMethod(VOID_METHOD_NO_ARGS);
    }
}