# Unity Project Analyzer

A command-line tool that analyzes Unity projects without requiring the Unity Editor. It parses Unity scene files and identifies unused MonoBehaviour scripts.

## Features

- **Scene Hierarchy Analysis**: Extracts and displays the complete GameObject hierarchy from Unity scene files
- **Unused Script Detection**: Identifies C# MonoBehaviour scripts that are not referenced in any scene
- **Standalone Operation**: Works without Unity Editor or Unity API dependencies

## Requirements

- .NET 9.0 SDK or later
- NuGet package: YamlDotNet

## Building

```bash
dotnet build
```

## Usage

```bash
dotnet run <unity_project_path> <output_folder_path>
```

### Example

```bash
dotnet run /path/to/UnityProject ./output
```

## Output

The tool generates the following files in the output folder:

### 1. Scene Hierarchy Files (`<SceneName>.unity.dump`)

Each Unity scene generates a corresponding `.dump` file showing the GameObject hierarchy:

```
Main Camera
Directional Light
Parent
--Child1
----ChildNested
--Child2
```

- Root objects have no indentation
- Each nesting level adds `--` prefix
- Preserves the original GameObject names from the scene

### 2. Unused Scripts Report (`UnusedScripts.csv`)

Lists all MonoBehaviour scripts that are not referenced in any scene:

```csv
Relative Path,GUID
Assets/Scripts/UnusedScript.cs,0111ada5c04694881b4ea1c5adfed99f
```

## Technical Details

### Architecture

The analyzer consists of three main components:

1. **SceneHierarchyParser**: Parses Unity scene files (YAML format) using YamlDotNet
2. **ScriptUsageAnalyzer**: Tracks script references across scenes and identifies unused scripts
3. **UnityProjectAnalyzer**: Orchestrates the analysis workflow

### Unity Scene File Format

Unity stores scenes in a multi-document YAML format with custom tags:
- Each document represents a Unity object (GameObject, Transform, Component, etc.)
- Documents are identified by their structure (presence of "GameObject", "Transform", etc.)
- GameObjects link to Transforms via fileID references
- Transform components maintain parent-child relationships through `m_Children` and `m_Father` fields

### Script Detection

Scripts are identified by:
1. Scanning all `.cs` files in the Assets folder
2. Reading corresponding `.meta` files to extract script GUIDs
3. Parsing scene files for `m_Script` references with matching GUIDs
4. Scripts without any scene references are marked as unused

## Design Decisions

### Why YamlDotNet?

Unity's scene files use standard YAML with custom tags. YamlDotNet's `RepresentationModel` provides:
- Structured access to YAML documents
- Type-safe navigation of YAML nodes
- Proper handling of Unity's multi-document format

### Structure-Based Detection

Rather than hardcoding Unity's internal type IDs, the parser identifies objects by their structure:
- Documents containing "GameObject" key → GameObject
- Documents containing "Transform" key → Transform
- Documents containing "SceneRoots" key → Scene root references

This approach is more maintainable and works across different Unity versions.

## Limitations

- Prefab instances are not fully analyzed (as specified in requirements)
- Only detects scripts referenced in scene files (not in prefabs or ScriptableObjects)
- Assumes all scripts inherit from MonoBehaviour

## License

MIT
