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

        public void Write(BinaryWriter writer, object value) { }
#endif

        void Write(HttpListenerResponse response, object value)
        {
            using (Stream compressed = new GZipStream(response.OutputStream, CompressionMode.Compress))
            using (var bw = new BinaryWriter(compressed))
                Write(bw, value);
        }
    }
}
