using System;
using System.IO;
using System.Reflection;
using Loupedeck;

namespace DevOSRing.Core.Hosting;

/// <summary>
/// Embedded-resource lookup helper shared by all DevOSRing plugins. Each plugin
/// calls <see cref="Init"/> from its <c>Plugin</c> constructor with its own assembly.
/// </summary>
public static class PluginResources
{
    private static Assembly? _assembly;

    public static void Init(Assembly assembly)
    {
        if (assembly is null) throw new ArgumentNullException(nameof(assembly));
        _assembly = assembly;
    }

    public static string[] GetFilesInFolder(string folderName) =>
        Require().GetFilesInFolder(folderName);

    public static string FindFile(string fileName) =>
        Require().FindFileOrThrow(fileName);

    public static string[] FindFiles(string regexPattern) =>
        Require().FindFiles(regexPattern);

    public static Stream GetStream(string resourceName) =>
        Require().GetStream(Require().FindFileOrThrow(resourceName));

    public static string ReadTextFile(string resourceName) =>
        Require().ReadTextFile(Require().FindFileOrThrow(resourceName));

    public static byte[] ReadBinaryFile(string resourceName) =>
        Require().ReadBinaryFile(Require().FindFileOrThrow(resourceName));

    public static BitmapImage ReadImage(string resourceName) =>
        Require().ReadImage(Require().FindFileOrThrow(resourceName));

    private static Assembly Require() =>
        _assembly ?? throw new InvalidOperationException(
            $"{nameof(PluginResources)} not initialised. Call {nameof(Init)}() from your plugin constructor.");
}
