using System;
using System.Collections.Generic;
using System.Reflection;
using Mindmagma.Curses;

// A simple test to explore the available attributes and constants in the NCurses API
class Program
{
    static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("Exploring NCurses API...");
            
            // Attributes
            Console.WriteLine("\nAttributes in CursesAttribute:");
            foreach (var field in typeof(CursesAttribute).GetFields())
            {
                Console.WriteLine($"  {field.Name} = {field.GetValue(null)}");
            }
            
            // Colors
            Console.WriteLine("\nColors in CursesColor:");
            foreach (var field in typeof(CursesColor).GetFields())
            {
                Console.WriteLine($"  {field.Name} = {field.GetValue(null)}");
            }
            
            // Keys
            Console.WriteLine("\nSample keys in CursesKey:");
            var keyFields = typeof(CursesKey).GetFields();
            for (int i = 0; i < Math.Min(10, keyFields.Length); i++)
            {
                var field = keyFields[i];
                Console.WriteLine($"  {field.Name} = {field.GetValue(null)}");
            }
            
            // Line drawing chars
            Console.WriteLine("\nLine drawing characters in CursesLineAcs:");
            foreach (var field in typeof(CursesLineAcs).GetFields())
            {
                Console.WriteLine($"  {field.Name} = {field.GetValue(null)}");
            }
            
            // NCurses methods (just a sample)
            Console.WriteLine("\nSample methods in NCurses:");
            var methods = typeof(NCurses).GetMethods();
            HashSet<string> uniqueNames = new HashSet<string>();
            
            foreach (var method in methods)
            {
                if (uniqueNames.Add(method.Name) && !method.Name.StartsWith("get_") && !method.Name.StartsWith("set_"))
                {
                    Console.WriteLine($"  {method.Name}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}