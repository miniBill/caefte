using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
#if API
using System.Threading.Tasks;
#endif

namespace Caefte
{
    public sealed partial class Controller
    {
#if API
        public Task<bool> Handle(HttpListenerRequest request, HttpListenerResponse response) => Task.FromResult(false);
#endif

        void Write(HttpListenerResponse response, object value)
        {
            using (Stream compressed = new GZipStream(response.OutputStream, CompressionMode.Compress))
            using (var bw = new BinaryWriter(compressed))
                Write(bw, value);
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
                default: throw new NotImplementedException($"Serialization for class {value.GetType()} not implemented");
            }
        }

        private static void WriteArray<T>(Action<T, BinaryWriter> writeItem, T[] value, BinaryWriter bw)
        {
            bw.Write(value.Length);
            foreach (T item in value)
                writeItem(item, bw);
        }
    }
}
