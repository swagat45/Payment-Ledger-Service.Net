using System;
using System.Reflection;

class Program
{
    static void Main()
    {
        var path = @"C:\Users\LENOVO\.nuget\packages\microsoft.openapi\2.3.0\lib\net8.0\Microsoft.OpenApi.dll";
        var asm = Assembly.LoadFrom(path);
        foreach (var type in asm.GetTypes())
        {
            if (type.Name.Contains("Security"))
                Console.WriteLine(type.FullName);
        }
    }
}
