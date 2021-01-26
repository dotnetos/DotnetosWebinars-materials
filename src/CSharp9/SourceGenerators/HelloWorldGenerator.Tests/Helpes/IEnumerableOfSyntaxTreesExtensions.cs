using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace HelloWorldGenerator.Tests
{
    public static class IEnumerableOfSyntaxTreesExtensions
    {
        public static string? ResultOf(this IEnumerable<SyntaxTree> trees, string filename)
            => trees.FirstOrDefault(x => x.FilePath.EndsWith(filename))?.ToString();
    }
}
