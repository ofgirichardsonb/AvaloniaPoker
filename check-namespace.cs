using System;
using System.Reflection;
using System.IO;

class Program
{
    static void Main()
    {
        string dllPath = "/home/runner/.nuget/packages/dotnet-curses/1.0.3/lib/netstandard2.0/dotnet-curses.dll";
        
        try
        {
            Assembly assembly = Assembly.LoadFile(dllPath);
            Console.WriteLine($"Assembly: {assembly.FullName}");
            Console.WriteLine("\nTypes:");
            
            foreach (Type type in assembly.GetExportedTypes())
            {
                Console.WriteLine($"- {type.FullName}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}