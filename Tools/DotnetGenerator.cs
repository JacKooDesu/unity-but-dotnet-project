using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

public class DotnetGenerator
{
    internal class AsmdefInfo
    {
        string _guid;
        Json _detail;
        public string Name => _detail.name;
        public string GUID => _guid;
        public string Dir { get; private set; }
        public string[] References => _detail.references ?? Array.Empty<string>();
        public bool NoEngineReferences => _detail.noEngineReferences;
        public bool AllowUnsafeCode => _detail.allowUnsafeCode;

        internal class Json
        {
            public string name;
            public string rootNamespace;
            public string[] references;
            public bool noEngineReferences;
            public bool allowUnsafeCode;
        }

        public AsmdefInfo(string asmdefPath, string metaPath)
        {
            _detail = ParseJson(asmdefPath);
            _guid = GetMetaGuid(metaPath);
            Dir = Path.GetDirectoryName(asmdefPath);
        }

        static string GetMetaGuid(string path)
        {
            string line = string.Empty;

            using (var reader = new StreamReader(path))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Trim().StartsWith("guid: "))
                        return line.Trim().Substring(6);
                }
            }
            return line;
        }

        static Json ParseJson(string jsonPath)
        {
            var content = File.ReadAllText(jsonPath);
            return JsonUtility.FromJson<AsmdefInfo.Json>(content);
        }
    }

    internal class ScopeAsmdefCollection
    {
        public List<AsmdefInfo> AsmdefList { get; private set; }
        public Dictionary<string, AsmdefInfo> GuidDict { get; private set; }
        public Dictionary<string, AsmdefInfo> NameDict { get; private set; }

        public ScopeAsmdefCollection(string rootDir)
        {
            var files = Directory
                .GetFiles(rootDir, "*.asmdef*", SearchOption.AllDirectories)
                .GroupBy(file => file.EndsWith(".asmdef"))
                .ToDictionary(group => group.Key, group => group.ToList());
            var metaHashSet = new HashSet<string>(files[false]);

            AsmdefList = files[true]
                .Where(file => metaHashSet.Contains(file + ".meta"))
                .Select(file => new AsmdefInfo(file, file + ".meta"))
                .ToList();

            GuidDict = AsmdefList.ToDictionary(asmdef => asmdef.GUID);
            NameDict = AsmdefList.ToDictionary(asmdef => asmdef.Name);
        }
    }

    static string EditorExePath = EditorApplication.applicationPath;
    static string EditorPath = Path.GetDirectoryName(EditorExePath);
    static string ProjectPath = Application.dataPath.Replace("/Assets", "");
    static string ExportPath = Path.Combine(ProjectPath, "exported");

    [MenuItem("UExtensions/Generate Dotnet Project")]
    public static void Run()
    {
        Directory.CreateDirectory(ExportPath);

        CopyDLLs();
        CollectAsmdef();
    }

    static void CopyDLLs()
    {
        // var editorDllsPath = Path.Combine(editorPath, "Data", "Managed");
        var editorDllsPath = Path.Combine(EditorPath, "Data", "Managed", "UnityEngine");

        var packageDllsPath = Path.Combine(ProjectPath, "Library", "ScriptAssemblies");

        var exportLibraryPath = Path.Combine(ExportPath, "Library");
        var exportEditorDllsPath = Path.Combine(exportLibraryPath, "UnityEngine");
        var exportPackageDllsPath = Path.Combine(exportLibraryPath, "Packages");

        Directory.CreateDirectory(exportLibraryPath);
        Directory.CreateDirectory(exportEditorDllsPath);
        Directory.CreateDirectory(exportPackageDllsPath);

        Func<FileInfo, bool> skipFunc = file =>
            file.Extension == ".pdb" || file.Name.StartsWith("Assembly-CSharp");
        CopyDirectory(editorDllsPath, exportEditorDllsPath, true, skipFunc);
        CopyDirectory(packageDllsPath, exportPackageDllsPath, true, skipFunc);
    }

    static void CollectAsmdef()
    {
        var packageDir = Path.Combine(ProjectPath, "Library", "PackageCache");
        if (!Directory.Exists(packageDir))
            return;

        var assetDir = Path.Combine(ProjectPath, "Assets");
        if (!Directory.Exists(assetDir))
            return;

        var packageAsmdefCollection = new ScopeAsmdefCollection(packageDir);
        var projectAsmdefCollection = new ScopeAsmdefCollection(assetDir);

        var editorCompiledAssemply = CompilationPipeline.GetAssemblies(AssembliesType.Editor)
            .ToDictionary(x => x.name);

        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "    ",
            NewLineOnAttributes = false,
            OmitXmlDeclaration = true
        };
        var sb = new StringBuilder();
        foreach (var asmdef in projectAsmdefCollection.AsmdefList)
        {
            using (var sw = File.CreateText(Path.Combine(ExportPath, asmdef.Name + ".csproj")))
            using (var writer = XmlWriter.Create(sw, settings))
            {
                // <Project Sdk="Microsoft.NET.Sdk">
                writer.WriteStartElement("Project");
                writer.WriteAttributeString("Sdk", "Microsoft.NET.Sdk");

                // PropertyGroup
                writer.WriteStartElement("PropertyGroup");
                {
                    writer.WriteElementString("TargetFramework", "netstandard2.0");
                    writer.WriteElementString("LangVersion", "latest");
                    writer.WriteElementString("ImplicitUsings", "disable");
                    writer.WriteElementString("nullable", "enable");
                    writer.WriteElementString("EnableDefaultCompileItems", "false");
                    writer.WriteElementString("AllowUnsafeBlocks", asmdef.AllowUnsafeCode ? "true" : "false");

                    // define constants
                    if (editorCompiledAssemply.TryGetValue(asmdef.Name, out var ca))
                        writer.WriteElementString("DefineConstants", string.Join(';', ca.defines));
                }
                writer.WriteEndElement();

                // ItemGroup
                writer.WriteStartElement("ItemGroup");
                {
                    writer.WriteStartElement("Compile");
                    var path = Path.GetRelativePath(assetDir, asmdef.Dir);
                    writer.WriteAttributeString("Include", "ScriptProject\\" + path + "\\**\\*.cs");
                    writer.WriteEndElement();

                    // reference dlls
                    foreach (var reference in asmdef.References)
                    {
                        AsmdefInfo asmdefInfo;
                        if (
                            packageAsmdefCollection.GuidDict.TryGetValue(reference, out asmdefInfo) ||
                            packageAsmdefCollection.NameDict.TryGetValue(reference, out asmdefInfo)
                        )
                        {
                            var refDllPath = "Library\\Packages\\" + asmdefInfo.Name + ".dll";
                            writer.WriteStartElement("Reference");
                            {
                                writer.WriteAttributeString("Include", asmdefInfo.Name);
                                writer.WriteElementString("HintPath", refDllPath);
                                writer.WriteElementString("Private", "False");
                            }
                            writer.WriteEndElement();
                        }
                        else if (
                            projectAsmdefCollection.GuidDict.TryGetValue(reference, out asmdefInfo) ||
                            projectAsmdefCollection.NameDict.TryGetValue(reference, out asmdefInfo)
                        )
                        {
                            var refCsprojPath = asmdefInfo.Name + ".csproj";
                            writer.WriteStartElement("ProjectReference");
                            writer.WriteAttributeString("Include", refCsprojPath);
                            writer.WriteEndElement();
                        }
                    }

                    if (!asmdef.NoEngineReferences)
                    {
                        writer.WriteStartElement("Reference");
                        writer.WriteAttributeString("Include", "Library\\UnityEngine\\*.dll");
                        writer.WriteEndElement();
                    }
                }
                writer.WriteEndElement();

                // </Project>
                writer.WriteEndElement();
            }
            sb.Clear();
        }

        using (var sw = File.CreateText(Path.Combine(ExportPath, "ScriptProject.slnx")))
        using (var writer = XmlWriter.Create(sw, settings))
        {
            writer.WriteStartElement("Solution");
            foreach (var asmdef in projectAsmdefCollection.AsmdefList)
            {
                writer.WriteStartElement("Project");
                writer.WriteAttributeString("Path", asmdef.Name + ".csproj");
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
    }

    static void CopyDirectory(string sourceDir, string destinationDir, bool recursive, Func<FileInfo, bool> skipFunc)
    {
        // Get information about the source directory
        var dir = new DirectoryInfo(sourceDir);

        // Check if the source directory exists
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        // Cache directories before we start copying
        DirectoryInfo[] dirs = dir.GetDirectories();

        // Create the destination directory
        Directory.CreateDirectory(destinationDir);

        // Get the files in the source directory and copy to the destination directory
        foreach (FileInfo file in dir.GetFiles())
        {
            if (skipFunc(file))
                continue;

            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath);
        }

        // If recursive and copying subdirectories, recursively call this method
        if (recursive)
        {
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir, true, skipFunc);
            }
        }
    }
}
