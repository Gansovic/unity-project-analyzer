using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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

        // Find all C# scripts and build GUID to path mapping
        var allScripts = new Dictionary<string, ScriptInfo>();
        var guidToScriptPath = new Dictionary<string, string>();
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
                    guidToScriptPath[guid] = scriptFile;
                }
            }
        }

        Console.WriteLine($"  Found {allScripts.Count} scripts");

        // Extract script references with field information from scenes
        var scriptReferences = new List<ScriptReference>();
        var sceneFiles = Directory.GetFiles(assetsPath, "*.unity", SearchOption.AllDirectories);

        foreach (var sceneFile in sceneFiles)
        {
            var references = ExtractScriptReferencesFromScene(sceneFile);
            scriptReferences.AddRange(references);
        }

        Console.WriteLine($"  Found {scriptReferences.Count} script references in scenes");

        // Validate script usage with field validation
        var usedGuids = new HashSet<string>();

        foreach (var reference in scriptReferences)
        {
            // Add owner script as used (it's referenced in scene)
            usedGuids.Add(reference.OwnerScriptGuid);

            // For target scripts, validate that the field actually exists
            if (!string.IsNullOrEmpty(reference.TargetScriptGuid) &&
                !string.IsNullOrEmpty(reference.FieldName))
            {
                // Get owner script file
                if (guidToScriptPath.TryGetValue(reference.OwnerScriptGuid, out var ownerScriptPath))
                {
                    // Parse owner script to get field names
                    var fieldNames = GetFieldNamesFromScript(ownerScriptPath);

                    // Check if the field exists
                    if (fieldNames.Contains(reference.FieldName))
                    {
                        // Field exists, mark target script as used
                        usedGuids.Add(reference.TargetScriptGuid);
                    }
                    // If field doesn't exist, target script is NOT marked as used (stale reference)
                }
            }
        }

        Console.WriteLine($"  Found {usedGuids.Count} unique scripts actually used (after field validation)");

        // Find unused scripts
        var unusedScripts = allScripts.Values
            .Where(script => !usedGuids.Contains(script.Guid))
            .OrderBy(script => script.Path.Count(c => c == Path.DirectorySeparatorChar))
            .ThenBy(script => script.Path)
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

    private List<ScriptReference> ExtractScriptReferencesFromScene(string sceneFilePath)
    {
        var references = new List<ScriptReference>();
        var content = File.ReadAllText(sceneFilePath);

        // Split into MonoBehaviour sections
        var monoBehaviourPattern = new Regex(
            @"--- !u!114 &\d+\s+MonoBehaviour:.*?(?=(?:--- !u!|\z))",
            RegexOptions.Singleline);

        var monoBehaviours = monoBehaviourPattern.Matches(content);

        foreach (Match mb in monoBehaviours)
        {
            var mbContent = mb.Value;

            // Extract owner script GUID (the MonoBehaviour class itself)
            var ownerScriptMatch = Regex.Match(mbContent,
                @"m_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([a-f0-9]+),\s*type:\s*3\}");

            if (!ownerScriptMatch.Success)
                continue;

            var ownerScriptGuid = ownerScriptMatch.Groups[1].Value;

            // Add reference for the owner script itself (without field name)
            references.Add(new ScriptReference
            {
                OwnerScriptGuid = ownerScriptGuid,
                FieldName = "",
                TargetScriptGuid = ""
            });

            // Extract serialized fields that reference other scripts
            // Fields appear after m_EditorClassIdentifier and reference scripts via fileID and guid
            var fieldPattern = new Regex(
                @"^\s+(\w+):\s*\{(?:fileID:\s*11500000,\s*)?guid:\s*([a-f0-9]+)",
                RegexOptions.Multiline);

            var fieldMatches = fieldPattern.Matches(mbContent);

            foreach (Match fieldMatch in fieldMatches)
            {
                var fieldName = fieldMatch.Groups[1].Value;
                var targetGuid = fieldMatch.Groups[2].Value;

                // Skip if field name is a Unity internal field
                if (fieldName.StartsWith("m_"))
                    continue;

                references.Add(new ScriptReference
                {
                    OwnerScriptGuid = ownerScriptGuid,
                    FieldName = fieldName,
                    TargetScriptGuid = targetGuid
                });
            }
        }

        return references;
    }

    private List<string> GetFieldNamesFromScript(string scriptPath)
    {
        try
        {
            var code = File.ReadAllText(scriptPath);
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();

            // Find the class declaration (should be only one public class per file)
            var classDeclaration = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault();

            if (classDeclaration == null)
                return new List<string>();

            // Get all field declarations
            var fields = classDeclaration.DescendantNodes()
                .OfType<FieldDeclarationSyntax>();

            // Extract field names
            var fieldNames = new List<string>();
            foreach (var field in fields)
            {
                foreach (var variable in field.Declaration.Variables)
                {
                    fieldNames.Add(variable.Identifier.Text);
                }
            }

            return fieldNames;
        }
        catch
        {
            return new List<string>();
        }
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

class ScriptReference
{
    public string OwnerScriptGuid { get; set; } = "";
    public string FieldName { get; set; } = "";
    public string TargetScriptGuid { get; set; } = "";
}
