using FluentAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace HelloWorldGenerator.Tests
{
    public class HelloWorldGeneratorTests : SourceGeneratorTestsBase
    {
        [Fact]
        public void GivenPrintableAttribute_WhenFieldAnnotated_ThenAddPrintFieldMethod()
        {
            Compilation inputCompilation = GivenCompilation(@"
using PrintableFields;

namespace ConsoleApp
{
    public partial class UserClass
    {
        [Printable]
        private int _field = 44;
        public void UserMethod()
        {            
            Print_field();
        }
    }
}");
            var (output, diagnostics) = GivenGeneratorRunOnCompilation<PrintableFieldsGenerator>(inputCompilation);

            diagnostics.Should().BeEmpty();
            output.SyntaxTrees.Should().HaveCount(3);
            output.GetDiagnostics().Should().BeEmpty();

            output.SyntaxTrees.ResultOf("UserClass.Printables.cs").Should().Be(@"
using System;
namespace ConsoleApp
{
    public partial class UserClass
    {

private void Print_field()
{
    Console.WriteLine(""_field: "" + _field.ToString());
}} }");
        }

        [Fact]
        public void GivenSpecificPartialMethods_WhenHasFields_ThenAddPrintAllFieldsMethod()
        {
            Compilation inputCompilation = GivenCompilation(@"
namespace ConsoleApp
{
    public partial class UserClass
    {
#pragma warning disable CS0414
        private int _fieldA = 44;
        public partial void PrintAllFields();
    }
}");
            var (output, diagnostics) = GivenGeneratorRunOnCompilation<PrintableFieldsGenerator>(inputCompilation);

            diagnostics.Should().BeEmpty();
            output.SyntaxTrees.Should().HaveCount(3);
            output.GetDiagnostics().Should().BeEmpty();

            output.SyntaxTrees.ResultOf("UserClass.PrintAllFields.cs").Should().Be(@"
using System;
namespace ConsoleApp
{
    public partial class UserClass
    {
        public partial void PrintAllFields()
        {
            Console.WriteLine(""_fieldA: "" + _fieldA.ToString());
        }
    }
}");
        }
    }
}
