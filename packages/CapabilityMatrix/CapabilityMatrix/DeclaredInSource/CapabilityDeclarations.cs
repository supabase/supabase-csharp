using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Supabase.CapabilityMatrix.Capabilities;

namespace Supabase.CapabilityMatrix.DeclaredInSource;

/// <summary>A source file to read: its repo-relative path and its text.</summary>
public sealed record SourceFile(string Path, string Text);

/// <summary>
/// The capabilities the SDK declares about itself, read from its C# source. Parses
/// syntax only — no compilation, no assembly loading — so the same files always yield
/// the same declarations. A member's symbol is named the way the matrix spells it:
/// the containing-type chain with the namespace stripped (e.g. <c>Constants.Operator.Equals</c>).
/// </summary>
public static class CapabilityDeclarations
{
    public static IReadOnlyList<DeclaredCapability> From(IEnumerable<SourceFile> sources) =>
        sources
            .SelectMany(source => CSharpSyntaxTree.ParseText(source.Text).GetCompilationUnitRoot().Members)
            .SelectMany(member => In(member, containingTypes: [], containerIsPublic: true))
            .OrderBy(declaration => declaration.Symbol, StringComparer.Ordinal)
            .ThenBy(declaration => declaration.FeatureKey, StringComparer.Ordinal)
            .ToList();

    private static IEnumerable<DeclaredCapability> In(
        MemberDeclarationSyntax node, IReadOnlyList<string> containingTypes, bool containerIsPublic) =>
        node switch
        {
            BaseNamespaceDeclarationSyntax ns =>
                ns.Members.SelectMany(child => In(child, containingTypes, containerIsPublic)),

            EnumDeclarationSyntax @enum => Type(
                @enum.AttributeLists, @enum.Modifiers, [.. containingTypes, @enum.Identifier.Text], containerIsPublic,
                members: @enum.Members.SelectMany(value =>
                    DeclaredBy(value.AttributeLists, Name(containingTypes, @enum.Identifier.Text, value.Identifier.Text), fromPublicApi: containerIsPublic && Public(@enum.Modifiers)))),

            TypeDeclarationSyntax type => Type(
                type.AttributeLists, type.Modifiers, [.. containingTypes, type.Identifier.Text], containerIsPublic,
                members: type.Members.SelectMany(member =>
                    Member(member, [.. containingTypes, type.Identifier.Text], containerIsPublic && Public(type.Modifiers), type.Keyword.IsKind(SyntaxKind.InterfaceKeyword)))),

            _ => [],
        };

    private static IEnumerable<DeclaredCapability> Type(
        SyntaxList<AttributeListSyntax> attributes, SyntaxTokenList modifiers,
        IReadOnlyList<string> chain, bool containerIsPublic, IEnumerable<DeclaredCapability> members)
    {
        var isPublic = containerIsPublic && Public(modifiers);
        return DeclaredBy(attributes, string.Join('.', chain), isPublic).Concat(members);
    }

    private static IEnumerable<DeclaredCapability> Member(
        MemberDeclarationSyntax node, IReadOnlyList<string> chain, bool chainIsPublic, bool onInterface) =>
        node switch
        {
            BaseTypeDeclarationSyntax nested => In(nested, chain, chainIsPublic),

            MethodDeclarationSyntax method =>
                DeclaredBy(method.AttributeLists, Name(chain, method.Identifier.Text), chainIsPublic && MemberPublic(method.Modifiers, onInterface)),

            PropertyDeclarationSyntax property =>
                DeclaredBy(property.AttributeLists, Name(chain, property.Identifier.Text), chainIsPublic && MemberPublic(property.Modifiers, onInterface)),

            FieldDeclarationSyntax field => field.Declaration.Variables.SelectMany(variable =>
                DeclaredBy(field.AttributeLists, Name(chain, variable.Identifier.Text), chainIsPublic && MemberPublic(field.Modifiers, onInterface))),

            _ => [],
        };

    private static IEnumerable<DeclaredCapability> DeclaredBy(
        SyntaxList<AttributeListSyntax> attributeLists, string symbol, bool fromPublicApi) =>
        attributeLists
            .SelectMany(list => list.Attributes)
            .Select(attribute => Capability(attribute, symbol, fromPublicApi))
            .OfType<DeclaredCapability>();

    private static DeclaredCapability? Capability(AttributeSyntax attribute, string symbol, bool fromPublicApi) =>
        (Status(attribute), KeyAndNote(attribute.ArgumentList)) switch
        {
            ({ } status, ({ } key, var note)) => new DeclaredCapability(key, status, note, symbol, fromPublicApi),
            _ => null,
        };

    private static CapabilityStatus? Status(AttributeSyntax attribute) => SimpleName(attribute.Name) switch
    {
        "ImplementedCapability" => CapabilityStatus.Implemented,
        "PartialCapability" => CapabilityStatus.Partial,
        _ => null,
    };

    private static (string? Key, string? Note) KeyAndNote(AttributeArgumentListSyntax? arguments)
    {
        var literals = arguments?.Arguments ?? [];
        string? Named(string name) => literals
            .FirstOrDefault(argument => argument.NameColon?.Name.Identifier.Text == name) is { } match ? Text(match) : null;
        var positional = literals.Where(argument => argument.NameColon is null).Select(Text).ToList();

        return (Named("key") ?? positional.ElementAtOrDefault(0), Named("note") ?? positional.ElementAtOrDefault(1));
    }

    private static string? Text(AttributeArgumentSyntax argument) =>
        argument.Expression is LiteralExpressionSyntax { RawKind: (int) SyntaxKind.StringLiteralExpression } literal
            ? literal.Token.ValueText
            : null;

    private static string SimpleName(NameSyntax name) =>
        (name switch
        {
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            SimpleNameSyntax simple => simple.Identifier.Text,
            _ => name.ToString(),
        }).Replace("Attribute", string.Empty, StringComparison.Ordinal);

    private static string Name(IReadOnlyList<string> chain, params string[] rest) => string.Join('.', [.. chain, .. rest]);

    private static bool Public(SyntaxTokenList modifiers) => modifiers.Any(SyntaxKind.PublicKeyword);

    private static bool MemberPublic(SyntaxTokenList modifiers, bool onInterface) =>
        !modifiers.Any(SyntaxKind.PrivateKeyword) && !modifiers.Any(SyntaxKind.ProtectedKeyword) && !modifiers.Any(SyntaxKind.InternalKeyword)
            && (onInterface || modifiers.Any(SyntaxKind.PublicKeyword));
}
