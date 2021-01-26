using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace HelloWorldGenerator
{
    [Generator]
    public class PrintableFieldsGenerator : ISourceGenerator
    {
        private const string attributeText = @"
using System;
namespace PrintableFields
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    sealed class PrintableAttribute : Attribute
    {
        public PrintableAttribute()
        {
        }
    }
}";

        public void Execute(GeneratorExecutionContext context)
        {
            MySyntaxReceiver syntaxReceiver = (MySyntaxReceiver)context.SyntaxReceiver;

            // Pretty ugly way (currently) to get a symbol for our attribute
            CSharpParseOptions options = (context.Compilation as CSharpCompilation).SyntaxTrees[0].Options as CSharpParseOptions;
            Compilation compilation = context.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(SourceText.From(attributeText, Encoding.UTF8), options));
            INamedTypeSymbol attributeSymbol = compilation.GetTypeByMetadataName("PrintableFields.PrintableAttribute");

            ProcessPrintableAttributedFields(context, syntaxReceiver, compilation, attributeSymbol);
            ProcessPrintAllFieldPartialMethods(context, syntaxReceiver, compilation);
        }

        private void ProcessPrintAllFieldPartialMethods(GeneratorExecutionContext context, MySyntaxReceiver syntaxReceiver, Compilation compilation)
        {
            foreach (var methodDeclarationSyntax in syntaxReceiver.CandidateMethods)
            {
                var semanticModel = compilation.GetSemanticModel(methodDeclarationSyntax.SyntaxTree);
                var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclarationSyntax);
                var containingTypeSymbol = methodSymbol.ContainingType;
                var fieldSymbols = containingTypeSymbol.GetMembers().OfType<IFieldSymbol>()
                    .Where(f => f.CanBeReferencedByName && !f.IsStatic);
                var classSource = ProcessClassPrintAllFieldsMethod(containingTypeSymbol, fieldSymbols);
                context.AddSource($"{containingTypeSymbol.Name}.PrintAllFields.cs", SourceText.From(classSource, Encoding.UTF8));
            }
        }

        private string ProcessClassPrintAllFieldsMethod(INamedTypeSymbol classSymbol, IEnumerable<IFieldSymbol> fieldSymbols)
        {
            if (!classSymbol.ContainingSymbol.Equals(classSymbol.ContainingNamespace, SymbolEqualityComparer.Default))
            {
                return null; //TODO: issue a diagnostic that it must be top level
            }
            string namespaceName = classSymbol.ContainingNamespace.ToDisplayString();

            StringBuilder source = new StringBuilder($@"
using System;
namespace {namespaceName}
{{
    public partial class {classSymbol.Name}
    {{
        public partial void PrintAllFields()
        {{
");
            foreach (var fieldSymbol in fieldSymbols)
            {
                string fieldName = fieldSymbol.Name;
                source.Append($@"            Console.WriteLine(""{fieldName}: "" + {fieldName}.ToString());");
            }
            source.Append($@"
        }}
    }}
}}");
            return source.ToString();
        }

        private void ProcessPrintableAttributedFields(GeneratorExecutionContext context, MySyntaxReceiver syntaxReceiver, Compilation compilation, INamedTypeSymbol attributeSymbol)
        {
            List<IFieldSymbol> fieldSymbols = new List<IFieldSymbol>();
            foreach (FieldDeclarationSyntax field in syntaxReceiver.CandidateFields)
            {
                SemanticModel model = compilation.GetSemanticModel(field.SyntaxTree);
                foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
                {
                    // Get the symbol being decleared by the field, and keep it if its annotated
                    IFieldSymbol fieldSymbol = model.GetDeclaredSymbol(variable) as IFieldSymbol;
                    if (fieldSymbol.GetAttributes()
                                   .Any(ad => ad.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default)))
                    {
                        fieldSymbols.Add(fieldSymbol);
                    }
                }
            }

            // group the fields by class, and generate the source
            foreach (IGrouping<INamedTypeSymbol, IFieldSymbol> group in fieldSymbols.GroupBy(f => f.ContainingType))
            {
                string classSource = ProcessClassPrintableAttribute(group.Key, group.ToList(), attributeSymbol, context);
                context.AddSource($"{group.Key.Name}.Printables.cs", SourceText.From(classSource, Encoding.UTF8));
            }

            context.AddSource("PrintableFields.PrintableAttribute", SourceText.From(attributeText, Encoding.UTF8));
        }

        private string ProcessClassPrintableAttribute(INamedTypeSymbol classSymbol, List<IFieldSymbol> fields, ISymbol attributeSymbol, GeneratorExecutionContext context)
        {
            if (!classSymbol.ContainingSymbol.Equals(classSymbol.ContainingNamespace, SymbolEqualityComparer.Default))
            {
                return null; //TODO: issue a diagnostic that it must be top level
            }
            string namespaceName = classSymbol.ContainingNamespace.ToDisplayString();

            // begin building the generated source
            StringBuilder source = new StringBuilder($@"
using System;
namespace {namespaceName}
{{
    public partial class {classSymbol.Name}
    {{
");
            // create method for each field 
            foreach (IFieldSymbol fieldSymbol in fields)
            {
                ProcessFieldPrintableAttribute(source, fieldSymbol, attributeSymbol);
            }
            source.Append("} }");
            return source.ToString();
        }

        private void ProcessFieldPrintableAttribute(StringBuilder source, IFieldSymbol fieldSymbol, ISymbol attributeSymbol)
        {
            // get the name and type of the field
            string fieldName = fieldSymbol.Name;
            source.Append($@"
private void Print{fieldName}()
{{
    Console.WriteLine(""{fieldName}: "" + {fieldName}.ToString());
}}");
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            // Register a factory that can create our custom syntax receiver
            context.RegisterForSyntaxNotifications(() => new MySyntaxReceiver());
        }
    }

    class MySyntaxReceiver : ISyntaxReceiver
    {
        public List<FieldDeclarationSyntax> CandidateFields { get; } = new List<FieldDeclarationSyntax>();
        public List<MethodDeclarationSyntax> CandidateMethods { get; } = new List<MethodDeclarationSyntax>();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            // Business logic to decide what we're interested in goes here
            if (syntaxNode is FieldDeclarationSyntax fds &&
                fds.AttributeLists.Count > 0)
            {
                CandidateFields.Add(fds);
            }
            if (syntaxNode is MethodDeclarationSyntax mds &&
                mds.Modifiers.Any(m => m.Kind() == SyntaxKind.PartialKeyword) &&
                mds.Identifier.Text == "PrintAllFields")
            {
                CandidateMethods.Add(mds);
            }
        }
    }
}
