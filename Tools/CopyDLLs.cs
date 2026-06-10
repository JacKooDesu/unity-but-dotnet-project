using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public class CopyDLLs
{
    public static void Run()
    {
        var editorExePath = EditorApplication.applicationPath;
        var editorPath = Path.GetDirectoryName(editorExePath);
        var editorDllsPath = Path.Combine(editorPath, "Data", "Managed");

        var projectPath = Application.dataPath.Replace("/Assets", "");
        var packageDllsPath = Path.Combine(projectPath, "Library", "ScriptAssemblies");

        var exportPath = Path.Combine(projectPath, "exported");
        var exportEditorDllsPath = Path.Combine(exportPath, "Unity", "Managed");
        var exportPackageDllsPath = Path.Combine(exportPath, "Packages");

        Directory.CreateDirectory(exportPath);
        Directory.CreateDirectory(exportEditorDllsPath);
        Directory.CreateDirectory(exportPackageDllsPath);

        CopyDirectory(editorDllsPath, exportEditorDllsPath, true);
        CopyDirectory(packageDllsPath, exportPackageDllsPath, true);
    }

    static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
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
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath);
        }

        // If recursive and copying subdirectories, recursively call this method
        if (recursive)
        {
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir, true);
            }
        }
    }
}
