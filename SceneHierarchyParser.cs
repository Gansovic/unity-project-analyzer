using YamlDotNet.RepresentationModel;

namespace UnityProjectAnalyzer;

class SceneHierarchyParser
{
    private readonly string _projectPath;
    private readonly string _outputPath;

    public SceneHierarchyParser(string projectPath, string outputPath)
    {
        _projectPath = projectPath;
        _outputPath = outputPath;
    }

    public void ParseAllScenes()
    {
        string assetsPath = Path.Combine(_projectPath, "Assets");
        if (!Directory.Exists(assetsPath))
        {
            Console.WriteLine($"Warning: Assets folder not found at '{assetsPath}'");
            return;
        }

        var sceneFiles = Directory.GetFiles(assetsPath, "*.unity", SearchOption.AllDirectories);

        foreach (var sceneFile in sceneFiles)
        {
            ParseScene(sceneFile);
        }
    }

    private void ParseScene(string sceneFilePath)
    {
        var fileName = Path.GetFileName(sceneFilePath);
        var outputFileName = $"{fileName}.dump";
        var outputFilePath = Path.Combine(_outputPath, outputFileName);

        Console.WriteLine($"Parsing scene: {fileName}");

        var gameObjects = new Dictionary<string, GameObject>();
        var transforms = new Dictionary<string, Transform>();
        var rootTransformIds = new List<string>();

        using (var reader = new StreamReader(sceneFilePath))
        {
            var yaml = new YamlStream();
            yaml.Load(reader);

            foreach (var document in yaml.Documents)
            {
                if (!(document.RootNode is YamlMappingNode root))
                    continue;

                var anchor = document.RootNode.Anchor.Value;
                var fileId = string.IsNullOrEmpty(anchor) ? "" : anchor;

                // Identify by structure, not by type ID
                if (root.Children.ContainsKey(new YamlScalarNode("GameObject")))
                {
                    var gameObject = ParseGameObject(fileId, root);
                    if (gameObject != null)
                        gameObjects[fileId] = gameObject;
                }
                else if (root.Children.ContainsKey(new YamlScalarNode("Transform")))
                {
                    var transform = ParseTransform(fileId, root);
                    if (transform != null)
                        transforms[fileId] = transform;
                }
                else if (root.Children.ContainsKey(new YamlScalarNode("SceneRoots")))
                {
                    rootTransformIds = ParseSceneRoots(root);
                }
            }
        }

        // Build the hierarchy
        var hierarchy = new List<string>();

        foreach (var rootTransformId in rootTransformIds)
        {
            BuildHierarchy(rootTransformId, transforms, gameObjects, 0, hierarchy);
        }

        // Write output
        File.WriteAllLines(outputFilePath, hierarchy);
        Console.WriteLine($"  Written to: {outputFileName}");
    }

    private GameObject? ParseGameObject(string fileId, YamlMappingNode root)
    {
        var gameObjectKey = new YamlScalarNode("GameObject");
        if (!root.Children.ContainsKey(gameObjectKey))
            return null;

        var goNode = root.Children[gameObjectKey] as YamlMappingNode;
        if (goNode == null)
            return null;

        var nameKey = new YamlScalarNode("m_Name");
        if (!goNode.Children.ContainsKey(nameKey))
            return null;

        var name = ((YamlScalarNode)goNode.Children[nameKey]).Value ?? "";

        return new GameObject { FileId = fileId, Name = name };
    }

    private Transform? ParseTransform(string fileId, YamlMappingNode root)
    {
        var transformKey = new YamlScalarNode("Transform");
        if (!root.Children.ContainsKey(transformKey))
            return null;

        var transformNode = root.Children[transformKey] as YamlMappingNode;
        if (transformNode == null)
            return null;

        var transform = new Transform { FileId = fileId };

        // Get GameObject reference
        var gameObjectKey = new YamlScalarNode("m_GameObject");
        if (transformNode.Children.ContainsKey(gameObjectKey))
        {
            var goNode = transformNode.Children[gameObjectKey] as YamlMappingNode;
            if (goNode != null)
            {
                var fileIdKey = new YamlScalarNode("fileID");
                if (goNode.Children.ContainsKey(fileIdKey))
                {
                    transform.GameObjectFileId = ((YamlScalarNode)goNode.Children[fileIdKey]).Value ?? "";
                }
            }
        }

        // Get parent reference
        var fatherKey = new YamlScalarNode("m_Father");
        if (transformNode.Children.ContainsKey(fatherKey))
        {
            var fatherNode = transformNode.Children[fatherKey] as YamlMappingNode;
            if (fatherNode != null)
            {
                var fileIdKey = new YamlScalarNode("fileID");
                if (fatherNode.Children.ContainsKey(fileIdKey))
                {
                    var fatherId = ((YamlScalarNode)fatherNode.Children[fileIdKey]).Value ?? "";
                    if (fatherId != "0")
                    {
                        transform.ParentFileId = fatherId;
                    }
                }
            }
        }

        // Get children
        var childrenKey = new YamlScalarNode("m_Children");
        if (transformNode.Children.ContainsKey(childrenKey))
        {
            var childrenNode = transformNode.Children[childrenKey] as YamlSequenceNode;
            if (childrenNode != null)
            {
                foreach (var childItem in childrenNode.Children)
                {
                    if (childItem is YamlMappingNode childMapping)
                    {
                        var fileIdKey = new YamlScalarNode("fileID");
                        if (childMapping.Children.ContainsKey(fileIdKey))
                        {
                            var childId = ((YamlScalarNode)childMapping.Children[fileIdKey]).Value ?? "";
                            transform.ChildrenFileIds.Add(childId);
                        }
                    }
                }
            }
        }

        return transform;
    }

    private List<string> ParseSceneRoots(YamlMappingNode root)
    {
        var rootIds = new List<string>();

        var sceneRootsKey = new YamlScalarNode("SceneRoots");
        if (!root.Children.ContainsKey(sceneRootsKey))
            return rootIds;

        var sceneRootsNode = root.Children[sceneRootsKey] as YamlMappingNode;
        if (sceneRootsNode == null)
            return rootIds;

        var rootsKey = new YamlScalarNode("m_Roots");
        if (!sceneRootsNode.Children.ContainsKey(rootsKey))
            return rootIds;

        var rootsSequence = sceneRootsNode.Children[rootsKey] as YamlSequenceNode;
        if (rootsSequence == null)
            return rootIds;

        foreach (var rootItem in rootsSequence.Children)
        {
            if (rootItem is YamlMappingNode rootMapping)
            {
                var fileIdKey = new YamlScalarNode("fileID");
                if (rootMapping.Children.ContainsKey(fileIdKey))
                {
                    var rootFileId = ((YamlScalarNode)rootMapping.Children[fileIdKey]).Value ?? "";
                    rootIds.Add(rootFileId);
                }
            }
        }

        return rootIds;
    }

    private void BuildHierarchy(string transformId, Dictionary<string, Transform> transforms,
                                 Dictionary<string, GameObject> gameObjects, int depth,
                                 List<string> hierarchy)
    {
        if (!transforms.TryGetValue(transformId, out var transform))
            return;

        if (!gameObjects.TryGetValue(transform.GameObjectFileId, out var gameObject))
            return;

        // Build indentation
        string prefix = depth == 0 ? "" : new string('-', depth * 2);
        string line = $"{prefix}{gameObject.Name}";
        hierarchy.Add(line);

        // Recursively process children
        foreach (var childId in transform.ChildrenFileIds)
        {
            BuildHierarchy(childId, transforms, gameObjects, depth + 1, hierarchy);
        }
    }
}

class GameObject
{
    public string FileId { get; set; } = "";
    public string Name { get; set; } = "";
}

class Transform
{
    public string FileId { get; set; } = "";
    public string GameObjectFileId { get; set; } = "";
    public string? ParentFileId { get; set; }
    public List<string> ChildrenFileIds { get; set; } = new();
}
