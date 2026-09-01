using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ResourceLocker.SourceGenerator
{
    [Generator]
    public class SerializerGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
                "ResourceLocker.Core.GenerateBinarySerializerAttribute",
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => (INamedTypeSymbol)ctx.TargetSymbol);

            context.RegisterSourceOutput(candidates, Generate);
        }

        private static void Generate(
            SourceProductionContext context,
            INamedTypeSymbol symbol)
        {
            var source = $$$"""
                            using System;
                            using System.IO;
                            using ResourceLocker.Core.Dto;
                            namespace {{{symbol.ContainingNamespace}}}
                            {
                                public partial class {{{symbol.Name}}}
                                {
                                    public byte[] SerializeToBinary()
                                    {
                                        using var stream = new MemoryStream();
                                        using var writer = new BinaryWriter(stream);

                                        writer.Write(ResourceType);
                                        writer.Write(OperationId);
                                        writer.Write(LockedAt.ToString("O"));
                                        writer.Write(LeaseExpiresAt.ToString("O"));
                                        writer.Flush();
                                        return stream.ToArray();
                                    }

                                    public static {{{symbol.Name}}} DeserializeFromBinary(byte[] value)
                                    {
                                        using var stream = new MemoryStream(value);
                                        using var reader = new BinaryReader(stream);

                                        var resourceType = reader.ReadString();
                                        var operationId = reader.ReadString();
                                        var lockedAt = DateTimeOffset.Parse(reader.ReadString());
                                        var leaseExpiresAt = DateTimeOffset.Parse(reader.ReadString());

                                        return new {{{symbol.Name}}}
                                        {
                                            ResourceType = resourceType,
                                            OperationId = operationId,
                                            LockedAt = lockedAt,
                                            LeaseExpiresAt = leaseExpiresAt
                                        };
                                    }
                                }
                            }
                            """;

            context.AddSource($"{symbol.Name}.g.cs", source);
        }
    }
}
