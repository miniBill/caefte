using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace API
{
    static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: API.exe source.exe destination.cs/destination.elm");
                return 1;
            }
            Console.WriteLine($"source: {args[0]}");
            Console.WriteLine($"destination: {args[1]}");

            List<ValidMethod> valids = GetValidMethods(args[0]);

            using (var fileStream = File.Open(args[1], FileMode.Create))
            using (var baseWriter = new StreamWriter(fileStream))
            using (var writer = new IndentedTextWriter(baseWriter, "    "))
                if (args[1].EndsWith(".cs"))
                    WriteCs(writer, valids);
                else
                    WriteElm(writer, valids);

            return 0;
        }

        static void WriteCs(IndentedTextWriter writer, List<ValidMethod> valids)
        {
            writer.WriteLine("using System.Net;");
            writer.WriteLine("using System.Threading.Tasks;");
            writer.WriteLine();
            writer.WriteLine("namespace Caefte");
            writer.WriteBlock(() =>
            {
                writer.WriteLine("public sealed partial class Controller");
                writer.WriteBlock(() =>
                {
                    writer.WriteLine("public async Task<bool> Handle(HttpListenerRequest request, HttpListenerResponse response)");
                    writer.WriteBlock(() =>
                    {
                        writer.WriteLine("switch (request.Url.AbsolutePath)");
                        writer.WriteBlock(() =>
                        {
                            WriteSwitch(writer, valids);
                            writer.WriteLine("default:");
                            writer.Indent++;
                            writer.WriteLine("await Task.Yield();");
                            writer.WriteLine("return false;");
                            writer.Indent--;
                        });
                    });
                });
            });
        }

        static void WriteSwitch(IndentedTextWriter writer, List<ValidMethod> valids)
        {
            foreach (var valid in valids)
            {
                writer.WriteLine($"case \"{valid.Path}\":");
                writer.WriteBlock(() =>
                {
                    if (valid.IsAsync)
                        writer.WriteLine($"var result = await new {valid.Type.Name}().{valid.MethodInfo.Name}();");
                    else
                        writer.WriteLine($"var result = new {valid.Type.Name}().{valid.MethodInfo.Name}();");
                    writer.WriteLine("Write(response, result);");
                    writer.WriteLine("return true;");
                });
            }
        }

        class ElmType
        {
            public ElmType(string type, string decoder)
            {
                Type = type;
                Decoder = decoder;
            }

            public string Type { get; }
            public string Decoder { get; }
        }

        static void WriteElm(IndentedTextWriter writer, List<ValidMethod> valids)
        {
            string FirstLower(string name) => char.ToLower(name[0]) + name.Substring(1);

            var neededDecoders = new HashSet<string>();

            ElmType ToElmType(Type type)
            {
                switch (type.FullName)
                {
                    case "System.String":
                        neededDecoders.Add("string");
                        return new ElmType("String", "stringDecoder");
                    case "System.Collections.Generic.List`1":
                        neededDecoders.Add("list");
                        ElmType itemType = ToElmType(type.GenericTypeArguments[0]);
                        return new ElmType(
                            itemType.Type.Contains(" ")
                                ? $"List ({itemType.Type})"
                                : $"List {itemType.Type}",
                            $"(listDecoder {itemType.Decoder})"
                        );
                    case "System.Bool":
                        neededDecoders.Add("bool");
                        return new ElmType("Bool", "boolDecoder");
                    case "System.Byte":
                        neededDecoders.Add("byte");
                        return new ElmType("Int", "byteDecoder");
                    case "System.SByte":
                        neededDecoders.Add("sbyte");
                        return new ElmType("Int", "sbyteDecoder");
                    case "System.Int16":
                        neededDecoders.Add("short");
                        return new ElmType("Int", "shortDecoder");
                    case "System.UInt16":
                        neededDecoders.Add("ushort");
                        return new ElmType("Int", "ushortDecoder");
                    case "System.Int32":
                        neededDecoders.Add("int");
                        return new ElmType("Int", "intDecoder");
                    case "System.UInt32":
                        neededDecoders.Add("uint");
                        return new ElmType("Int", "uintDecoder");
                    case "System.Char":
                        neededDecoders.Add("char");
                        return new ElmType("Char", "charDecoder");
                    case "System.Float":
                        neededDecoders.Add("float");
                        return new ElmType("Float", "floatDecoder");
                    case "System.Double":
                        neededDecoders.Add("double");
                        return new ElmType("Double", "doubleDecoder");
                    default:
                        if (type.IsArray)
                        {
                            neededDecoders.Add("list");
                            ElmType elementType = ToElmType(type.GetElementType());
                            return new ElmType(
                                elementType.Type.Contains(" ")
                                    ? $"List ({elementType.Type})"
                                    : $"List {elementType.Type}",
                                $"(listDecoder {elementType.Decoder})"
                            );
                        }
                        throw new NotImplementedException($"Mapping for {type.FullName}");
                }
            }

            string exposed = string.Join(", ",
                valids.Select(v => FirstLower(v.ApiName)).OrderBy(n => n));
            writer.WriteLine($"module Api exposing ({exposed})");
            writer.WriteLine();
            writer.WriteLine("import Bytes exposing (Bytes)");
            writer.WriteLine("import Bytes.Decode as Bytes exposing (Decoder)");
            writer.WriteLine("import Http");
            writer.WriteLine("import Task exposing (Task)");
            foreach (ValidMethod valid in valids.OrderBy(v => v.ApiName))
            {
                writer.WriteLine();
                writer.WriteLine();
                string elmName = FirstLower(valid.ApiName);
                ElmType elmType = ToElmType(valid.ReturnType);
                writer.WriteLine($"{elmName} : ({elmType.Type} -> msg) -> Task Http.Error msg");
                writer.WriteLine($"{elmName} toMsg =");
                writer.WriteIndented(() =>
                {
                    writer.WriteLine($"Http.task");
                    writer.WriteIndented(
                        $"{{ method = \"{valid.Method.ToUpperInvariant()}\"",
                        $", headers = []",
                        $", url = \"{valid.Path}\"",
                        $", body = Http.emptyBody",
                        $", resolver = Http.bytesResolver <| responseToResult {elmType.Decoder} toMsg ",
                        $", timeout = Nothing",
                        $"}}"
                    );
                });
            }

            writer.WriteLine();
            writer.WriteLine();
            writer.WriteLine("responseToResult : Decoder a -> (a -> msg) -> Http.Response Bytes -> Result Http.Error msg");
            writer.WriteLine("responseToResult decoder toMsg response =");
            writer.WriteIndented(() =>
            {
                writer.WriteLine("case response of");
                writer.WriteIndented(() =>
                {
                    void WriteClause(string pattern, string result)
                    {
                        writer.WriteLine($"{pattern} -> ");
                        writer.WriteIndented(result);
                        writer.WriteEmptyLine();
                    }
                    WriteClause("Http.BadUrl_ u",
                        "Err <| Http.BadUrl u");
                    WriteClause("Http.Timeout_",
                        "Err Http.Timeout");
                    WriteClause("Http.NetworkError_",
                        "Err Http.NetworkError");
                    WriteClause("Http.BadStatus_ metadata _",
                        "Err <| Http.BadStatus metadata.statusCode");
                    writer.WriteLine("Http.GoodStatus_ _ body ->");
                    writer.WriteIndented(() =>
                    {
                        writer.WriteLine("case Bytes.decode decoder body of");
                        writer.WriteIndented(() =>
                        {
                            WriteClause("Just r",
                                "Ok <| toMsg r");
                            WriteClause("Nothing",
                                "Err <| Http.BadBody \"Decoding failed\"");
                        });
                    });
                });
            });

            bool first = true;
            foreach (string type in neededDecoders)
            {
                if (!first)
                    writer.WriteLine();
                first = false;
                writer.WriteLine();

                switch (type)
                {
                    case "string":
                        writer.WriteLine("stringDecoder : Decoder String");
                        writer.WriteLine("stringDecoder =");
                        writer.WriteIndented(
                            "Bytes.signedInt32 Bytes.LE |> Bytes.andThen Bytes.string");
                        break;
                    case "list":
                        writer.WriteLine("listDecoder : Decoder a -> Decoder (List a)");
                        writer.WriteLine("listDecoder decoder =");
                        writer.WriteIndented(() =>
                        {
                            writer.WriteLine("let");
                            writer.WriteIndented(() =>
                            {
                                writer.WriteLine("listStep ( n, xs ) =");
                                writer.WriteIndented(() =>
                                {
                                    writer.WriteLine("if n <= 0 then");
                                    writer.WriteIndented(
                                        "Bytes.succeed <| Bytes.Done xs");
                                    writer.WriteEmptyLine();
                                    writer.WriteLine("else");
                                    writer.WriteIndented(
                                        @"Bytes.map (\x -> Bytes.Loop ( n - 1, x :: xs )) decoder");
                                });
                            });
                            writer.WriteLine("in");
                            writer.WriteLine("Bytes.signedInt32 Bytes.LE");
                            writer.WriteIndented(
                                @"|> Bytes.andThen (\length -> Bytes.loop ( length, [] ) listStep)");
                        });
                        break;
                    case "bool":
                        writer.WriteLine("boolDecoder : Decoder Bool");
                        writer.WriteLine("boolDecoder =");
                        writer.WriteIndented(
                            "Bytes.unsignedInt8 |> Bytes.map ((/=) 0)");
                        break;
                    case "byte":
                        writer.WriteLine("byteDecoder : Decoder Int");
                        writer.WriteLine("byteDecoder =");
                        writer.WriteIndented(
                            "Bytes.unsignedInt8");
                        break;
                    case "sbyte":
                        writer.WriteLine("sbyteDecoder : Decoder Int");
                        writer.WriteLine("sbyteDecoder =");
                        writer.WriteIndented(
                            "Bytes.signedInt8");
                        break;
                    case "short":
                        writer.WriteLine("ushortDecoder : Decoder Int");
                        writer.WriteLine("ushortDecoder =");
                        writer.WriteIndented(
                            "Bytes.signedInt16 Bytes.LE");
                        break;
                    case "ushort":
                        writer.WriteLine("shortDecoder : Decoder Int");
                        writer.WriteLine("shortDecoder =");
                        writer.WriteIndented(
                            "Bytes.unsignedInt16 Bytes.LE");
                        break;
                    case "int":
                        writer.WriteLine("intDecoder : Decoder Int");
                        writer.WriteLine("intDecoder =");
                        writer.WriteIndented(
                            "Bytes.signedInt32 Bytes.LE");
                        break;
                    case "uint":
                        writer.WriteLine("uintDecoder : Decoder Int");
                        writer.WriteLine("uintDecoder =");
                        writer.WriteIndented(
                            "Bytes.unsignedInt32 Bytes.LE");
                        break;
                    case "char":
                        writer.WriteLine("charDecoder : Decoder Char");
                        writer.WriteLine("charDecoder =");
                        writer.WriteIndented(
                            "Bytes.unsignedInt16 |> Bytes.map Char.fromCode");
                        break;
                    case "float":
                        writer.WriteLine("floatDecoder : Decoder Float");
                        writer.WriteLine("floatDecoder =");
                        writer.WriteIndented(
                            "Bytes.float");
                        break;
                    case "double":
                        writer.WriteLine("doubleDecoder : Decoder Double");
                        writer.WriteLine("doubleDecoder =");
                        writer.WriteIndented(
                            "Bytes.double");
                        break;
                    default:
                        throw new NotImplementedException($"Elm decoder for {type}");
                }
            }
        }

        class ValidMethod
        {
            public String Method { get; set; }
            public bool IsAsync { get; set; }
            public string Path { get; set; }
            public Type Type { get; set; }
            public MethodInfo MethodInfo { get; set; }
            public string ApiName { get; set; }
            public Type ReturnType { get; set; }
        }

        static List<ValidMethod> GetValidMethods(string assemblyPath)
        {
            var valids = new List<ValidMethod>();
            Assembly source = Assembly.LoadFile(Path.GetFullPath(assemblyPath));
            foreach (Type type in source.GetTypes())
            {
                foreach (MethodInfo method in type.GetMethods())
                {
                    object[] attrs = method.GetCustomAttributes(true);
                    valids.AddRange(
                        from attr in attrs.OfType<Attribute>()
                        let name = attr.GetType().Name
                        where name == "GetAttribute" || name == "PostAttribute"
                        let pathProperty = attr.GetType().GetProperty("Path")
                        where pathProperty != null
                        let apiNameProperty = attr.GetType().GetProperty("ApiName")
                        where apiNameProperty != null
                        let path = pathProperty.GetValue(attr) as string
                        where path != null
                        let isAsync = method.ReturnType.Name == "Task`1"
                        select new ValidMethod
                        {
                            Method = name == "GetAttribute" ? "Get" : "Post",
                            IsAsync = isAsync,
                            Path = path,
                            Type = type,
                            MethodInfo = method,
                            ApiName = apiNameProperty.GetValue(attr) as string ?? method.Name,
                            ReturnType = isAsync ? method.ReturnType.GenericTypeArguments[0] : method.ReturnType
                        });
                }
            }

            return valids;
        }

        public static void WriteBlock(this IndentedTextWriter writer, Action block)
        {
            writer.WriteLine("{");
            writer.Indent++;
            block();
            writer.Indent--;
            writer.WriteLine("}");
        }

        public static void WriteIndented(this IndentedTextWriter writer, Action block)
        {
            writer.Indent++;
            block();
            writer.Indent--;
        }

        public static void WriteIndented(this IndentedTextWriter writer, params string[] lines)
        {
            writer.Indent++;
            foreach (string line in lines)
                writer.WriteLine(line);
            writer.Indent--;
        }

        public static void WriteEmptyLine(this IndentedTextWriter writer)
        {
            int indent = writer.Indent;
            writer.Indent = 0;
            writer.WriteLine();
            writer.Indent = indent;
        }
    }
}
