using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace TransactTcp.Tests.Resources
{
    public static class Utils
    {
        public static string LoadResourceAsText(string resourceName)
        {
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(string.Concat("TransactTcp.Tests.Resources.", resourceName));
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public static byte[] LoadResourceAsByteArray(string resourceName)
        {
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(string.Concat("TransactTcp.Tests.Resources.", resourceName));
            var buffer = new byte[stream.Length];
            stream.Read(buffer, 0, buffer.Length);
            return buffer;
        }
    }
}
