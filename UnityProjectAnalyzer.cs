namespace UnityProjectAnalyzer;

class UnityProjectAnalyzer
{
    private readonly string _projectPath;
    private readonly string _outputPath;

    public UnityProjectAnalyzer(string projectPath, string outputPath)
    {
        _projectPath = projectPath;
        _outputPath = outputPath;
    }

    public void Analyze()
    {
        // Parse scene hierarchies
        var sceneParser = new SceneHierarchyParser(_projectPath, _outputPath);
        sceneParser.ParseAllScenes();

        // Find unused scripts
        var scriptAnalyzer = new ScriptUsageAnalyzer(_projectPath, _outputPath);
        scriptAnalyzer.FindUnusedScripts();
    }
}
