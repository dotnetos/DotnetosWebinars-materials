using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;

namespace HelloWorldGenerator.Tests
{
    public class SourceGeneratorTestsBase
    {

        protected static Compilation GivenCompilation(string source)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(source);
            var references = new List<MetadataReference>();
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                if (!assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
                {
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
                }
            }
            var compilation = CSharpCompilation.Create("compilation",
                       new[] { syntaxTree },
                       references,
                       new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            return compilation;
        }

        protected static (Compilation, ImmutableArray<Diagnostic>) GivenGeneratorRunOnCompilation<T>(Compilation inputCompilation)
            where T : ISourceGenerator, new()
        {
            var generator = new T();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var outputCompilation, out var diagnostics);
            return (outputCompilation, diagnostics);
        }
    }
}