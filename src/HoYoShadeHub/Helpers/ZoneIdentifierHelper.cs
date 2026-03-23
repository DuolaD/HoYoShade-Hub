using System;
using System.IO;
using System.Runtime.InteropServices;

namespace HoYoShadeHub.Helpers;

public static class ZoneIdentifierHelper
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteFile(string lpFileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFileAttributes(string lpFileName);

    private const uint INVALID_FILE_ATTRIBUTES = 0xFFFFFFFF;

    /// <summary>
    /// Checks if the specified file has a Zone.Identifier alternate data stream.
    /// </summary>
    public static bool HasZoneIdentifier(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        string streamPath = filePath + ":Zone.Identifier";
        return GetFileAttributes(streamPath) != INVALID_FILE_ATTRIBUTES;
    }

    /// <summary>
    /// Deletes the Zone.Identifier alternate data stream from the specified file.
    /// </summary>
    public static bool RemoveZoneIdentifier(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        string streamPath = filePath + ":Zone.Identifier";
        if (GetFileAttributes(streamPath) != INVALID_FILE_ATTRIBUTES)
        {
            return DeleteFile(streamPath);
        }
        return false;
    }

    /// <summary>
    /// Recursively removes the Zone.Identifier stream from all files in the given directory.
    /// </summary>
    public static void ClearDirectoryZoneIdentifiers(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return;

        try
        {
            var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                try
                {
                    RemoveZoneIdentifier(file);
                }
                catch (Exception)
                {
                    // Ignore individual file errors
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error clearing Zone.Identifiers: {ex.Message}");
        }
    }
}
