using System.Net;
using System.Threading.Tasks;

namespace Caefte
{
    static class HttpListenerExtensions
    {
        public static Task<HttpListenerContext> GetContextAsync(this HttpListener listener) =>
            Task<HttpListenerContext>.Factory.FromAsync(
                listener.BeginGetContext,
                listener.EndGetContext,
                listener);
    }
}
