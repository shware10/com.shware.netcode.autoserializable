using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Text;
using System.Collections.Generic;


[Generator] //소스 제네레이터 명시
public sealed class AutoSerializableGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context) 
    {

        // attribute 캐시
        var autoAttrType = context.CompilationProvider
            .Select((c, _) => c.GetTypeByMetadataName("Shware.Netcode.AutoSerializableAttribute"));

        var ignoreAttrType = context.CompilationProvider
            .Select((c, _) => c.GetTypeByMetadataName("Shware.Netcode.IgnoreSerializableAttribute"));

        var structCandidates = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => node is StructDeclarationSyntax s && s.AttributeLists.Count > 0,
            transform: static (ctx, _) => (StructDeclarationSyntax)ctx.Node
            );

        var structsWithSymbols = structCandidates.Combine(context.CompilationProvider);

        context.RegisterSourceOutput(structsWithSymbols.Combine(autoAttrType).Combine(ignoreAttrType),
                static (context, tuple) =>
                {
                    var (((structDecl, compilation), autoAttr), ignoreAttr) = tuple;

                    if (autoAttr == null) return;

                    var semanticModel = compilation.GetSemanticModel(structDecl.SyntaxTree);

                    if (!HasAutoSerializableAttribute(structDecl, semanticModel, autoAttr)) return;

                    if (!structDecl.Modifiers.Any(SyntaxKind.PartialKeyword))
                    {
                        ReportError(
                        context,
                        structDecl,
                        "AS001",
                        "Struct must be partial",
                        "Struct marked with [AutoSerializable] must be declared partial.");
                        return;
                    }

                    bool hasManualSerialize = structDecl.Members.OfType<MethodDeclarationSyntax>().Any(m => m.Identifier.Text == "NetworkSerialize");
                    
                    if(hasManualSerialize)
                    {
                        ReportError(
                            context,
                            structDecl,
                            "AS002",
                            "NetworkSerialize already exists",
                            "Do not manually implement NetworkSerialize when using [AutoSerializable]."
                            );
                        return;
                    }

                    bool generateEquatable = ShouldGenerateEquatable(structDecl, semanticModel, autoAttr);

                    var ns = GetNamespace(structDecl);
                    var structName = structDecl.Identifier.Text;

                    var fields = new List<string>();
                    foreach (var fieldDecl in structDecl.Members.OfType<FieldDeclarationSyntax>())
                    {
                        if (!IsSerializableField(context, fieldDecl, semanticModel, ignoreAttr)) continue;

                        foreach (var v in fieldDecl.Declaration.Variables)
                        {
                            fields.Add(v.Identifier.Text);
                        }
                    }

                    if (fields.Count == 0) return;

                    var source = Generate(ns, structName, fields, generateEquatable);

                    context.AddSource(
                        $"{structName}.AutoSerializable.g.cs",
                        SourceText.From(source, Encoding.UTF8));
                });
    }


    private static bool ShouldGenerateEquatable(
        StructDeclarationSyntax structDecl,
        SemanticModel semanticModel,
        INamedTypeSymbol autoAttrType)
    {
        foreach(var attr in structDecl.AttributeLists.SelectMany(a => a.Attributes))
        {
            var constructor = semanticModel.GetSymbolInfo(attr).Symbol as IMethodSymbol;
            if (constructor == null || !SymbolEqualityComparer.Default.Equals(constructor.ContainingType, autoAttrType))
                continue;

            if(attr.ArgumentList?.Arguments.Count > 0)
            {
                var val = semanticModel.GetConstantValue(
                    attr.ArgumentList.Arguments[0].Expression);

                if (val.HasValue && val.Value is bool b) return b;
            }

            foreach (var arg in attr.ArgumentList?.Arguments ?? default)
            {
                if(arg.NameEquals?.Name.Identifier.Text == "GenerateEquatable")
                {
                    var val = semanticModel.GetConstantValue(arg.Expression);
                    if (val.HasValue && val.Value is bool b) return b;
                }
            }

            return true;
        }
        return true;
    }

    private static bool IsSerializableField(
        SourceProductionContext context,
        FieldDeclarationSyntax fieldDecl,
        SemanticModel semanticModel,
        INamedTypeSymbol ignoreAttributeType)
    {
        //static이나 const 키워드가 있으면 직렬화 불가
        if (fieldDecl.Modifiers.Any(SyntaxKind.StaticKeyword) ||
            fieldDecl.Modifiers.Any(SyntaxKind.ConstKeyword))
            return false;

        var fieldType = semanticModel.GetTypeInfo(fieldDecl.Declaration.Type).Type;

        if (fieldType == null) return false;

        if(fieldType.IsReferenceType)
        {
            ReportError(
                context,
                fieldDecl,
                "AS003",
                "Reference types are not supported",
                $"Field '{fieldDecl.Declaration.Variables.First().Identifier.Text}' " + "is a reference type. Only value types are supported in AutoSerializable structs.");
            return false;
        }

        foreach(var attrList in fieldDecl.AttributeLists)
        {
            foreach(var attr in attrList.Attributes)
            {
                var attrType = GetAttributeType(semanticModel, attr);

                if (attrType == null)
                    continue;

                if (attrType.Name == "NonSerializedAttribute")
                    return false;

                if (ignoreAttributeType != null && SymbolEqualityComparer.Default.Equals(attrType, ignoreAttributeType))
                    return false;
            }
        }
        return true;
    }

    private static INamedTypeSymbol GetAttributeType(SemanticModel semanticModel, AttributeSyntax attr)
    {
        // Attribute의 실제 심볼데이터 가져오기
        var symbolInfo = semanticModel.GetSymbolInfo(attr);
        // 컴파일러는 Attribute 문법을 “Attribute 타입의 생성자 호출”로 해석 때문에 Symbol이 IMethodSymbol로 나옴
        var constructorSymbol = symbolInfo.Symbol as IMethodSymbol;
        // ContainingType을 통해 실제 생성자가 만든 Attribute 객체 클래스 타입을 획득
        var attrType = constructorSymbol?.ContainingType;

        return attrType;
    }



    // seamticModel을 통한 Attribute 실제 타입 검사
    private static bool HasAutoSerializableAttribute(
        StructDeclarationSyntax structDecl, 
        SemanticModel semanticModel, 
        INamedTypeSymbol targetAttributeType)
    {
        foreach(var attrList in structDecl.AttributeLists)
        {
            foreach(var attr in attrList.Attributes)
            {
                var attrType = GetAttributeType(semanticModel, attr);

                if (attrType == null) 
                    continue;

                if (SymbolEqualityComparer.Default.Equals(attrType, targetAttributeType)) 
                    return true;
            }
        }
        return false;
    }

    // struct의 상위 네임스페이스를 문자열로 가져오는 함수
    private static string GetNamespace(StructDeclarationSyntax structDecl)
    {
        //struct의 부모를 타고 올라가서 최종 네임스페이스 접근 문자열 생성
        var namespaces = structDecl.Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Select(n => n.Name.ToString())
            .Reverse()
            .ToArray();
        if (namespaces.Length == 0)
            return null;

        return string.Join(".", namespaces);
    }




    /// <summary>
    /// 코드를 생성하는 함수
    /// </summary>
    /// <param name="ns"> 네임스페이스 </param>
    /// <param name="structName"> 구조체 이름 </param>
    /// <param name="fields"> 구조체 필드리스트 </param>
    /// <returns></returns>
    private static string Generate(string ns, string structName, List<string> fields, bool generateEquatable)
    {
        var sb = new StringBuilder();

        //namespace가 있으면
        if(!string.IsNullOrEmpty(ns))
        {
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
        }

        sb.AppendLine($"public partial struct {structName} : Unity.Netcode.INetworkSerializable");
        if (generateEquatable) 
            sb.Append($", System.IEquatable<{structName}>");
        sb.AppendLine();
        sb.AppendLine("{");

        //NetworkSerializable
        sb.AppendLine("    public void NetworkSerialize<T>(Unity.Netcode.BufferSerializer<T> serializer)");
        sb.AppendLine("        where T : Unity.Netcode.IReaderWriter");
        sb.AppendLine("    {");
        foreach(var field in fields)
        {
            sb.AppendLine($"        serializer.SerializeValue(ref {field});");
        }
        sb.AppendLine("    }");

        if(generateEquatable)
        {

            //Equals<T>
            sb.AppendLine();
            sb.AppendLine($"    public bool Equals({structName} other)");
            sb.AppendLine("    {");
            if (fields.Count == 1)
            {
                sb.AppendLine($"        return {fields[0]} == other.{fields[0]};");
            }
            else
            {
                sb.AppendLine("        return " +
                    string.Join(" && ", fields.Select(f => $"{f} == other.{f}")) + ";");
            }
            sb.AppendLine("    }");

            // Equals(object)
            sb.AppendLine();
            sb.AppendLine("    public override bool Equals(object obj)");
            sb.AppendLine("    {");
            sb.AppendLine($"        return obj is {structName} other && Equals(other);");
            sb.AppendLine("    }");

            // GetHashCode
            sb.AppendLine();
            sb.AppendLine("    public override int GetHashCode()");
            sb.AppendLine("    {");
            sb.AppendLine($"        return System.HashCode.Combine({string.Join(", ", fields)});");
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");

        if (!string.IsNullOrEmpty(ns))
        { sb.AppendLine("}"); }

        return sb.ToString();
    }

    //버그 리포트 함수
    private static void ReportError(
        SourceProductionContext context,
        SyntaxNode node,
        string id,
        string title,
        string message)
    {
        var descriptor = new DiagnosticDescriptor(
            id,
            title,
            message,
            "AutoSerializable",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        context.ReportDiagnostic(Diagnostic.Create(descriptor, node.GetLocation()));
    }

}
