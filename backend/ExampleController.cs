using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public Tree GetFilesRecursive() => ToTree(new DirectoryInfo(Path.GetTempPath()));

        private Tree ToTree(DirectoryInfo di) =>
            new Tree(
                di.GetDirectories().Select(ToTree),
                di.GetFiles().Select(f => f.Name)
            );

        public class Tree
        {
            public Tree(IEnumerable<Tree> directories, IEnumerable<string> files)
            {
                Directories = directories.ToArray();
                Files = files.ToArray();
            }

            public Tree[] Directories { get; }
            public string[] Files { get; }
        }
    }
}
