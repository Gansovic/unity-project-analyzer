namespace UnityProjectAnalyzer;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine("Usage: ./tool.exe <unity_project_path> <output_folder_path>");
            return;
        }

        string projectPath = args[0];
        string outputPath = args[1];

        if (!Directory.Exists(projectPath))
        {
            Console.WriteLine($"Error: Project path '{projectPath}' does not exist.");
            return;
        }

        Directory.CreateDirectory(outputPath);

        var analyzer = new UnityProjectAnalyzer(projectPath, outputPath);
        analyzer.Analyze();

        Console.WriteLine("Analysis completed successfully.");
    }
}
