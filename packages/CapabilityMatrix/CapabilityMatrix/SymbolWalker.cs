using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Supabase.CapabilityMatrix;

/// <summary>
/// Extracts the SDK's declared types and members from C# source text, together
/// with any capability annotations on them. Pure over its input: it parses
/// syntax only (no compilation, no semantic model, no assembly loading), so the
/// same <see cref="SourceFile" /> list always yields the same symbols.
/// </summary>
public static class SymbolWalker
{
    /// <summary>A source file as (repo-relative) path and its text.</summary>
    public sealed record SourceFile(string Path, string Text);

    private const string ImplementedName = "ImplementedCapability";
    private const string PartialName = "PartialCapability";

    /// <summary>
    /// Walks every file and returns one <see cref="DeclaredSymbol" /> per declared
    /// type and member, ordered by qualified name then file for determinism.
    /// </summary>
    public static IReadOnlyList<DeclaredSymbol> Extract(IEnumerable<SourceFile> files)
    {
        var symbols = new List<DeclaredSymbol>();
        foreach (var file in files)
        {
            var root = CSharpSyntaxTree.ParseText(file.Text).GetCompilationUnitRoot();
            foreach (var member in root.Members)
                WalkMember(member, parentTypes: [], parentPublic: true, file.Path, symbols);
        }

        return symbols
            .OrderBy(s => s.QualifiedName, StringComparer.Ordinal)
            .ThenBy(s => s.FilePath, StringComparer.Ordinal)
            .ThenBy(s => s.Line)
            .ToList();
    }

    private static void WalkMember(
        MemberDeclarationSyntax member,
        IReadOnlyList<string> parentTypes,
        bool parentPublic,
        string filePath,
        List<DeclaredSymbol> output)
    {
        switch (member)
        {
            case BaseNamespaceDeclarationSyntax ns:
                // Namespaces are stripped from the qualified name; descend without pushing.
                foreach (var child in ns.Members)
                    WalkMember(child, parentTypes, parentPublic, filePath, output);
                break;

            case TypeDeclarationSyntax type:
                WalkType(type, type.Identifier.Text, type.Keyword, type.Members, parentTypes, parentPublic, filePath, output);
                break;

            case EnumDeclarationSyntax enumType:
                WalkEnum(enumType, parentTypes, parentPublic, filePath, output);
                break;
        }
    }

    private static void WalkType(
        TypeDeclarationSyntax type,
        string name,
        SyntaxToken keyword,
        SyntaxList<MemberDeclarationSyntax> members,
        IReadOnlyList<string> parentTypes,
        bool parentPublic,
        string filePath,
        List<DeclaredSymbol> output)
    {
        var isInterface = keyword.IsKind(SyntaxKind.InterfaceKeyword);
        var chain = Append(parentTypes, name);
        var isPublic = parentPublic && IsTypePublic(type.Modifiers);

        output.Add(Build(Join(chain), "class", type.Identifier, isPublic, type.AttributeLists, filePath));

        foreach (var child in members)
        {
            switch (child)
            {
                case BaseTypeDeclarationSyntax:
                    WalkMember(child, chain, isPublic, filePath, output);
                    break;
                case MethodDeclarationSyntax method:
                    Emit(output, chain, method.Identifier, "method", MemberIsPublic(method.Modifiers, isInterface), method.AttributeLists, isPublic, filePath);
                    break;
                case PropertyDeclarationSyntax property:
                    Emit(output, chain, property.Identifier, "property", MemberIsPublic(property.Modifiers, isInterface), property.AttributeLists, isPublic, filePath);
                    break;
                case FieldDeclarationSyntax field:
                    foreach (var variable in field.Declaration.Variables)
                        Emit(output, chain, variable.Identifier, "property", MemberIsPublic(field.Modifiers, isInterface), field.AttributeLists, isPublic, filePath);
                    break;
            }
        }
    }

    private static void WalkEnum(
        EnumDeclarationSyntax enumType,
        IReadOnlyList<string> parentTypes,
        bool parentPublic,
        string filePath,
        List<DeclaredSymbol> output)
    {
        var chain = Append(parentTypes, enumType.Identifier.Text);
        var isPublic = parentPublic && IsTypePublic(enumType.Modifiers);
        output.Add(Build(Join(chain), "class", enumType.Identifier, isPublic, enumType.AttributeLists, filePath));

        foreach (var value in enumType.Members)
            Emit(output, chain, value.Identifier, "property", memberPublic: isPublic, value.AttributeLists, isPublic, filePath);
    }

    private static void Emit(
        List<DeclaredSymbol> output,
        IReadOnlyList<string> chain,
        SyntaxToken identifier,
        string kind,
        bool memberPublic,
        SyntaxList<AttributeListSyntax> attributeLists,
        bool chainPublic,
        string filePath)
    {
        var name = Join(Append(chain, identifier.Text));
        output.Add(Build(name, kind, identifier, chainPublic && memberPublic, attributeLists, filePath));
    }

    private static DeclaredSymbol Build(
        string qualifiedName,
        string kind,
        SyntaxToken anchor,
        bool isPublic,
        SyntaxList<AttributeListSyntax> attributeLists,
        string filePath)
    {
        var line = anchor.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        return new DeclaredSymbol(qualifiedName, kind, filePath, line, isPublic, ReadAnnotations(attributeLists));
    }

    private static IReadOnlyList<CapabilityAnnotation> ReadAnnotations(SyntaxList<AttributeListSyntax> attributeLists)
    {
        var annotations = new List<CapabilityAnnotation>();
        foreach (var list in attributeLists)
        {
            foreach (var attribute in list.Attributes)
            {
                var status = StatusOf(SimpleName(attribute.Name));
                if (status is null)
                    continue;

                var (key, note) = ReadArguments(attribute.ArgumentList);
                if (key is not null)
                    annotations.Add(new CapabilityAnnotation(key, status.Value, note));
            }
        }

        return annotations;
    }

    private static CapabilityStatus? StatusOf(string simpleName) => simpleName switch
    {
        ImplementedName => CapabilityStatus.Implemented,
        PartialName => CapabilityStatus.PartiallyImplemented,
        _ => null,
    };

    private static (string? Key, string? Note) ReadArguments(AttributeArgumentListSyntax? argumentList)
    {
        if (argumentList is null)
            return (null, null);

        string? key = null;
        string? note = null;
        var positional = 0;
        foreach (var argument in argumentList.Arguments)
        {
            var value = LiteralValue(argument.Expression);
            var named = argument.NameColon?.Name.Identifier.Text ?? argument.NameEquals?.Name.Identifier.Text;
            if (named == "note")
                note = value;
            else if (named == "key")
                key = value;
            else if (positional++ == 0)
                key = value;
            else
                note = value;
        }

        return (key, note);
    }

    private static string? LiteralValue(ExpressionSyntax expression) =>
        expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)
            ? literal.Token.ValueText
            : null;

    private static string SimpleName(NameSyntax name)
    {
        var text = name switch
        {
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            SimpleNameSyntax simple => simple.Identifier.Text,
            _ => name.ToString(),
        };
        return text.EndsWith("Attribute", StringComparison.Ordinal)
            ? text[..^"Attribute".Length]
            : text;
    }

    private static bool IsTypePublic(SyntaxTokenList modifiers) =>
        modifiers.Any(SyntaxKind.PublicKeyword);

    private static bool MemberIsPublic(SyntaxTokenList modifiers, bool isInterfaceMember)
    {
        if (modifiers.Any(SyntaxKind.PrivateKeyword) || modifiers.Any(SyntaxKind.ProtectedKeyword) || modifiers.Any(SyntaxKind.InternalKeyword))
            return false;
        // Interface members are public by default; class/struct members are not.
        return isInterfaceMember || modifiers.Any(SyntaxKind.PublicKeyword);
    }

    private static IReadOnlyList<string> Append(IReadOnlyList<string> list, string value) => [.. list, value];

    private static string Join(IReadOnlyList<string> chain) => string.Join('.', chain);
}
