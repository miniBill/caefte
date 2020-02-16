using System;

namespace Caefte
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public sealed class GetAttribute : Attribute
    {
        public GetAttribute(string path) => Path = path;

        public string Path { get; }

        public string ApiName { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public sealed class PostAttribute : Attribute
    {
        public PostAttribute(string path) => Path = path;

        public string Path { get; }

        public string ApiName { get; set; }
    }
}
