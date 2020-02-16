using System;
using System.IO;
using System.Threading.Tasks;

namespace Caefte
{
    public sealed class ExampleController
    {
        [Get("/api/async_rng", ApiName = "SlowerRng")]
        public async Task<int> AsyncRng()
        {
            await Task.Delay(500);
            return Rng();
        }

        [Get("/api/rng")]
        public int Rng()
        {
            int result = new Random().Next();
            Console.WriteLine($"Random! {result}");
            return result;
        }

        [Get("/api/files")]
        public string[] GetFiles() => Directory.GetFiles("/tmp");

        [Get("/api/files3")]
        public string[][][] GetFiles3() => new[] {
            new [] {
                Directory.GetFiles("/tmp")
            }
        };
    }
}
