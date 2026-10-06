namespace CliTool;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: cli-tool <command>");
            return 1;
        }

        var command = args[0];
        switch (command)
        {
            case "greet":
                Console.WriteLine("Hello, World!");
                return 0;
            case "version":
                Console.WriteLine("1.0.0");
                return 0;
            default:
                Console.WriteLine($"Unknown command: {command}");
                return 1;
        }
    }
}
