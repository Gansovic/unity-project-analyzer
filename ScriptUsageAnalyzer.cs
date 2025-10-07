using System.Text.RegularExpressions;

namespace UnityProjectAnalyzer;

class ScriptUsageAnalyzer
{
    private readonly string _projectPath;
    private readonly string _outputPath;

    public ScriptUsageAnalyzer(string projectPath, string outputPath)
    {
        _projectPath = projectPath;
        _outputPath = outputPath;
    }

    public void FindUnusedScripts()
    {
        Console.WriteLine("Finding unused scripts...");

        string assetsPath = Path.Combine(_projectPath, "Assets");
        if (!Directory.Exists(assetsPath))
        {
            Console.WriteLine($"Warning: Assets folder not found at '{assetsPath}'");
            return;
        }

        // Find all C# scripts
        var allScripts = new Dictionary<string, ScriptInfo>();
        var scriptFiles = Directory.GetFiles(assetsPath, "*.cs", SearchOption.AllDirectories);

        foreach (var scriptFile in scriptFiles)
        {
            var metaFile = scriptFile + ".meta";
            if (File.Exists(metaFile))
            {
                var guid = ExtractGuidFromMeta(metaFile);
                if (!string.IsNullOrEmpty(guid))
                {
                    var relativePath = GetRelativePath(scriptFile, _projectPath);
                    allScripts[guid] = new ScriptInfo
                    {
                        Guid = guid,
                        Path = relativePath
                    };
                }
            }
        }

        Console.WriteLine($"  Found {allScripts.Count} scripts");

        // Find all used script GUIDs from scene files
        var usedGuids = new HashSet<string>();
        var sceneFiles = Directory.GetFiles(assetsPath, "*.unity", SearchOption.AllDirectories);

        foreach (var sceneFile in sceneFiles)
        {
            var guids = ExtractScriptGuidsFromScene(sceneFile);
            foreach (var guid in guids)
            {
                usedGuids.Add(guid);
            }
        }

        Console.WriteLine($"  Found {usedGuids.Count} unique scripts used in scenes");

        // Find unused scripts
        var unusedScripts = allScripts.Values
            .Where(script => !usedGuids.Contains(script.Guid))
            .OrderBy(script => script.Path)
            .ToList();

        Console.WriteLine($"  Found {unusedScripts.Count} unused scripts");

        // Write output
        var outputFile = Path.Combine(_outputPath, "UnusedScripts.csv");
        using (var writer = new StreamWriter(outputFile))
        {
            writer.WriteLine("Relative Path,GUID");
            foreach (var script in unusedScripts)
            {
                writer.WriteLine($"{script.Path},{script.Guid}");
            }
        }

        Console.WriteLine($"  Written to: UnusedScripts.csv");
    }

    private string ExtractGuidFromMeta(string metaFilePath)
    {
        var guidPattern = new Regex(@"^guid:\s*([a-f0-9]+)", RegexOptions.Multiline);
        var content = File.ReadAllText(metaFilePath);
        var match = guidPattern.Match(content);

        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        return "";
    }

    private HashSet<string> ExtractScriptGuidsFromScene(string sceneFilePath)
    {
        var guids = new HashSet<string>();
        var scriptPattern = new Regex(@"m_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([a-f0-9]+),\s*type:\s*3\}");

        var content = File.ReadAllText(sceneFilePath);
        var matches = scriptPattern.Matches(content);

        foreach (Match match in matches)
        {
            guids.Add(match.Groups[1].Value);
        }

        return guids;
    }

    private string GetRelativePath(string fullPath, string basePath)
    {
        var baseUri = new Uri(basePath + Path.DirectorySeparatorChar);
        var fullUri = new Uri(fullPath);
        var relativeUri = baseUri.MakeRelativeUri(fullUri);
        return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
    }
}

class ScriptInfo
{
    public string Guid { get; set; } = "";
    public string Path { get; set; } = "";
}
