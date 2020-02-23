using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace API
{
    class APIType
    {
        public string ElmName { get; }
        public string ElmDecoder { get; }
        public string CsName { get; }

        public void WriteElmDecoder(IndentedTextWriter writer)
        {
            void WriteDecoder(string name, string elmType, string implementation)
            {
                writer.WriteLine($"{name}Decoder : Decoder {elmType}");
                writer.WriteLine($"{name}Decoder =");
                writer.WriteIndented(implementation);
            }

            switch (ElmName)
            {
                case "string":
                    WriteDecoder("string", "String", "Bytes.signedInt32 Bytes.LE |> Bytes.andThen Bytes.string");
                    break;
                case "bool":
                    WriteDecoder("bool", "Bool", "Bytes.unsignedInt8 |> Bytes.map ((/=) 0)");
                    break;
                case "byte":
                    WriteDecoder("byte", "Int", "Bytes.unsignedInt8");
                    break;
                case "sbyte":
                    WriteDecoder("sbyte", "Int", "Bytes.signedInt8");
                    break;
                case "short":
                    WriteDecoder("ushort", "Int", "Bytes.signedInt16 Bytes.LE");
                    break;
                case "ushort":
                    WriteDecoder("short", "Int", "Bytes.unsignedInt16 Bytes.LE");
                    break;
                case "int":
                    WriteDecoder("int", "Int", "Bytes.signedInt32 Bytes.LE");
                    break;
                case "uint":
                    WriteDecoder("uint", "Int", "Bytes.unsignedInt32 Bytes.LE");
                    break;
                case "char":
                    WriteDecoder("char", "Char", "Bytes.unsignedInt16 |> Bytes.map Char.fromCode");
                    break;
                case "float":
                    WriteDecoder("float", "Float", "Bytes.float");
                    break;
                case "double":
                    WriteDecoder("double", "Double", "Bytes.double");
                    break;
                case "list":
                    writer.WriteLine("listDecoder : Decoder a -> Decoder (List a)");
                    writer.WriteLine("listDecoder decoder =");
                    writer.WriteIndented(() =>
                    {
                        writer.WriteLine("Bytes.signedInt32 Bytes.LE");
                        writer.WriteIndented(
                            @"|> Bytes.andThen (\length -> Bytes.loop ( length, [] ) (listDecoderHelper decoder))");
                    });
                    writer.WriteLine();
                    writer.WriteLine();
                    writer.WriteLine("listDecoderHelper : Decoder a -> ( Int, List a ) -> Decoder (Bytes.Step ( Int, List a ) (List a))");
                    writer.WriteLine("listDecoderHelper decoder ( n, xs ) =");
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
                    break;
                default:
                    throw new NotImplementedException($"Elm decoder for type {ElmName}");
            }

        }

        public void WriteCsEncoder(IndentedTextWriter writer)
        {

        }

        public APIType[] ChildTypes { get; }

        public static APIType Int;
    }

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
                    foreach (APIType type in GetAllTypes(valids.Select(valid => valid.ReturnType)))
                    {
                        writer.WriteLine();
                        type.WriteCsEncoder(writer);
                    }
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
                    string typeName = valid.MethodInfo.DeclaringType.Name;
                    string methodName = valid.MethodInfo.Name;
                    if (valid.IsAsync)
                        writer.WriteLine($"var result = await new {typeName}().{methodName}();");
                    else
                        writer.WriteLine($"var result = new {typeName}().{methodName}();");
                    writer.WriteLine("Write(response, result);");
                    writer.WriteLine("return true;");
                });
            }
        }

        static void WriteElm(IndentedTextWriter writer, List<ValidMethod> valids)
        {
            string FirstLower(string name) => char.ToLower(name[0]) + name.Substring(1);

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
                writer.WriteLine($"{elmName} : ({valid.ReturnType.ElmName} -> msg) -> Task Http.Error msg");
                writer.WriteLine($"{elmName} toMsg =");
                writer.WriteIndented(() =>
                {
                    writer.WriteLine($"Http.task");
                    writer.WriteIndented(
                        $"{{ method = \"{valid.HttpMethod.ToUpperInvariant()}\"",
                        $", headers = []",
                        $", url = \"{valid.Path}\"",
                        $", body = Http.emptyBody",
                        $", resolver = Http.bytesResolver <| responseToResult {valid.ReturnType.ElmDecoder} toMsg ",
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

            HashSet<APIType> neededDecoders = GetAllTypes(valids.Select(v => v.ReturnType));
            bool first = true;
            foreach (APIType type in neededDecoders)
            {
                if (!first)
                    writer.WriteLine();
                first = false;
                writer.WriteLine();

                type.WriteElmDecoder(writer);
            }
        }

        static HashSet<APIType> GetAllTypes(IEnumerable<APIType> types)
        {
            var result = new HashSet<APIType>();
            var todo = new Queue<APIType>(types);
            while (todo.Count > 0)
            {
                APIType type = todo.Dequeue();
                if (result.Add(type))
                    todo.EnqueueAll(type.ChildTypes);
            }
            return result;
        }

        class ValidMethod
        {
            public String HttpMethod { get; set; }
            public bool IsAsync { get; set; }
            public string Path { get; set; }
            public MethodInfo MethodInfo { get; set; }
            public string ApiName { get; set; }
            public APIType ReturnType { get; set; }
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
                            HttpMethod = name == "GetAttribute" ? "Get" : "Post",
                            IsAsync = isAsync,
                            Path = path,
                            MethodInfo = method,
                            ApiName = apiNameProperty.GetValue(attr) as string ?? method.Name,
                            ReturnType = ToApiType(isAsync ? method.ReturnType.GenericTypeArguments[0] : method.ReturnType)
                        });
                }
            }

            return valids;
        }

        private static APIType ToApiType(Type type)
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
            void Write(BinaryWriter bw, object value)
            {
                switch (value)
                {
                    case bool b:
                        bw.Write(b);
                        break;
                    case byte b:
                        bw.Write(b);
                        break;
                    case sbyte b:
                        bw.Write(b);
                        break;
                    case short b:
                        bw.Write(b);
                        break;
                    case ushort b:
                        bw.Write(b);
                        break;
                    case char c:
                        bw.Write(c);
                        break;
                    case int b:
                        bw.Write(b);
                        break;
                    case uint b:
                        bw.Write(b);
                        break;
                    case long b:
                        bw.Write(b);
                        break;
                    case ulong b:
                        bw.Write(b);
                        break;
                    case float f:
                        bw.Write(f);
                        break;
                    case double d:
                        bw.Write(d);
                        break;
                    case decimal d:
                        bw.Write(d);
                        break;
                    case string s:
                        Write(bw, Encoding.UTF8.GetBytes(s));
                        break;
                    case byte[] b:
                        bw.Write(b.Length);
                        bw.Write(b);
                        break;
                    case IList list:
                        bw.Write(list.Count);
                        foreach (object item in list)
                            Write(bw, item);
                        break;
                    default:
                        throw new NotImplementedException($"Serialization for class {value.GetType()} not implemented");
                }
            }

            void WriteArray<T>(Action<T, BinaryWriter> writeItem, T[] value, BinaryWriter bw)
            {
                bw.Write(value.Length);
                foreach (T item in value)
                    writeItem(item, bw);
            }
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
