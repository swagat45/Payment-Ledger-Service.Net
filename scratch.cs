using System;
using StackExchange.Redis;

class Program {
    static void Main() {
        foreach (var m in typeof(IDatabase).GetMethods()) {
            if (m.Name == "StringSet" || m.Name == "StringSetAsync") {
                Console.Write(m.Name + "(");
                var p = m.GetParameters();
                for (int i=0; i<p.Length; i++) {
                    Console.Write(p[i].ParameterType.Name + " " + p[i].Name);
                    if (i < p.Length - 1) Console.Write(", ");
                }
                Console.WriteLine(")");
            }
        }
    }
}
