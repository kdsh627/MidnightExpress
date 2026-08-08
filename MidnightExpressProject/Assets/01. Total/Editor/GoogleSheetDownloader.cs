using System;
using System.IO;
using System.Threading.Tasks;
using NPOI.XSSF.UserModel;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public sealed class GoogleSheetDownloader : EditorWindow
{
    private const string DefaultSaveFolder = "Assets/01. Total/Data";
    private const string DefaultSaveFileName = "DialogueData.xlsx";
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private string sheetId = string.Empty;
    private string saveFolderPath = DefaultSaveFolder;
    private string saveFileName = DefaultSaveFileName;
    private bool isDownloading;

    private string PrefKeyId => Application.productName + "_DialogueSheetId";
    private string PrefKeyPath => Application.productName + "_DialogueSheetPath";
    private string PrefKeyFile => Application.productName + "_DialogueSheetFile";

    [MenuItem("Tools/Dialogue/Download Google Sheet")]
    public static void ShowWindow()
    {
        GetWindow<GoogleSheetDownloader>("Dialogue Sheet");
    }

    private void OnEnable()
    {
        sheetId = EditorPrefs.GetString(PrefKeyId, string.Empty);
        saveFolderPath = EditorPrefs.GetString(PrefKeyPath, DefaultSaveFolder);
        saveFileName = EditorPrefs.GetString(PrefKeyFile, DefaultSaveFileName);
    }

    private void OnDisable()
    {
        SaveSettings();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Google Sheet Downloader", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "The sheet must be accessible without an interactive Google login. "
            + "A successful download is imported into the dialogue ScriptableObject automatically.",
            MessageType.Info);

        sheetId = EditorGUILayout.TextField("Sheet ID or URL", sheetId);

        EditorGUILayout.BeginHorizontal();
        saveFolderPath = EditorGUILayout.TextField("Save Folder", saveFolderPath);
        if (GUILayout.Button("Select", GUILayout.Width(70f)))
        {
            SelectSaveFolder();
        }

        EditorGUILayout.EndHorizontal();
        saveFileName = EditorGUILayout.TextField("File Name", saveFileName);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(
                   isDownloading
                   || string.IsNullOrWhiteSpace(sheetId)
                   || string.IsNullOrWhiteSpace(saveFolderPath)
                   || string.IsNullOrWhiteSpace(saveFileName)))
        {
            if (GUILayout.Button(isDownloading ? "Downloading..." : "Download & Import", GUILayout.Height(30f)))
            {
                SaveSettings();
                DownloadSheet();
            }
        }
    }

    private void SelectSaveFolder()
    {
        string initialFolder;
        try
        {
            initialFolder = GetFullAssetFolderPath(saveFolderPath);
        }
        catch
        {
            initialFolder = Application.dataPath;
        }

        string selected = EditorUtility.OpenFolderPanel(
            "Select a folder inside this project's Assets directory",
            initialFolder,
            string.Empty);
        if (string.IsNullOrEmpty(selected))
        {
            return;
        }

        string fullSelected = Path.GetFullPath(selected).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string fullAssets = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!fullSelected.Equals(fullAssets, StringComparison.OrdinalIgnoreCase)
            && !fullSelected.StartsWith(fullAssets + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog(
                "Invalid save folder",
                "Choose a folder inside this project's Assets directory so Unity can import the workbook.",
                "OK");
            return;
        }

        string relative = fullSelected.Substring(fullAssets.Length)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Replace('\\', '/');
        saveFolderPath = string.IsNullOrEmpty(relative) ? "Assets" : "Assets/" + relative;
        SaveSettings();
    }

    private async void DownloadSheet()
    {
        isDownloading = true;
        Repaint();

        try
        {
            string normalizedSheetId = ExtractAndValidateSheetId(sheetId);
            string normalizedFileName = NormalizeFileName(saveFileName);
            string normalizedFolder = NormalizeAssetFolder(saveFolderPath);
            string assetPath = normalizedFolder.TrimEnd('/') + "/" + normalizedFileName;
            string fullPath = GetFullAssetPath(assetPath);
            string url = $"https://docs.google.com/spreadsheets/d/{normalizedSheetId}/export?format=xlsx";

            byte[] workbookBytes;
            string contentType;
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result != UnityWebRequest.Result.Success
                    || request.responseCode < 200
                    || request.responseCode >= 300)
                {
                    throw new InvalidOperationException(
                        $"Google Sheet download failed (HTTP {request.responseCode}): {request.error}");
                }

                contentType = request.GetResponseHeader("Content-Type");
                workbookBytes = request.downloadHandler?.data;
            }

            ValidateResponse(contentType, workbookBytes);
            WriteFileAtomically(fullPath, workbookBytes);

            saveFolderPath = normalizedFolder;
            saveFileName = normalizedFileName;
            sheetId = normalizedSheetId;
            SaveSettings();

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"Downloaded and imported Google Sheet: {assetPath}");
        }
        catch (Exception exception)
        {
            Debug.LogError($"Google Sheet download/import failed.\n{exception}");
            EditorUtility.DisplayDialog("Google Sheet download failed", exception.Message, "OK");
        }
        finally
        {
            isDownloading = false;
            if (this != null)
            {
                Repaint();
            }
        }
    }

    private static string ExtractAndValidateSheetId(string value)
    {
        string candidate = value?.Trim();
        if (string.IsNullOrEmpty(candidate))
        {
            throw new ArgumentException("Google Sheet ID is empty.");
        }

        const string marker = "/spreadsheets/d/";
        int markerIndex = candidate.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex >= 0)
        {
            int start = markerIndex + marker.Length;
            int end = candidate.IndexOfAny(new[] { '/', '?', '#' }, start);
            candidate = end < 0 ? candidate.Substring(start) : candidate.Substring(start, end - start);
        }

        foreach (char character in candidate)
        {
            if (!char.IsLetterOrDigit(character) && character != '-' && character != '_')
            {
                throw new ArgumentException("Google Sheet ID contains invalid characters.");
            }
        }

        if (candidate.Length == 0)
        {
            throw new ArgumentException("Google Sheet ID is empty.");
        }

        return candidate;
    }

    private static string NormalizeFileName(string value)
    {
        string candidate = value?.Trim();
        if (string.IsNullOrEmpty(candidate))
        {
            throw new ArgumentException("Save file name is empty.");
        }

        if (!string.Equals(candidate, Path.GetFileName(candidate), StringComparison.Ordinal))
        {
            throw new ArgumentException("Save file name cannot include a directory.");
        }

        if (!candidate.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            candidate += ".xlsx";
        }

        if (candidate.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("Save file name contains invalid characters.");
        }

        return candidate;
    }

    private static string NormalizeAssetFolder(string value)
    {
        string candidate = value?.Trim().Replace('\\', '/').TrimEnd('/');
        if (string.IsNullOrEmpty(candidate)
            || (!candidate.Equals("Assets", StringComparison.OrdinalIgnoreCase)
                && !candidate.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Save folder must be inside this project's Assets directory.");
        }

        if (candidate.Contains(".."))
        {
            throw new ArgumentException("Save folder cannot contain parent-directory segments.");
        }

        return candidate;
    }

    private static string GetFullAssetFolderPath(string assetFolder)
    {
        string normalized = NormalizeAssetFolder(assetFolder);
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Could not resolve the Unity project root.");
        return Path.GetFullPath(Path.Combine(projectRoot, normalized));
    }

    private static string GetFullAssetPath(string assetPath)
    {
        string normalized = assetPath.Replace('\\', '/');
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Could not resolve the Unity project root.");
        string fullPath = Path.GetFullPath(Path.Combine(projectRoot, normalized));
        string fullAssets = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (!fullPath.StartsWith(fullAssets + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Resolved save path is outside this project's Assets directory.");
        }

        return fullPath;
    }

    private static void ValidateResponse(string contentType, byte[] data)
    {
        string mediaType = contentType?.Split(';')[0].Trim();
        if (!string.IsNullOrEmpty(mediaType)
            && !mediaType.Equals(XlsxContentType, StringComparison.OrdinalIgnoreCase)
            && !mediaType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)
            && !mediaType.Equals("application/zip", StringComparison.OrdinalIgnoreCase)
            && !mediaType.Equals("application/x-zip-compressed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Unexpected response Content-Type '{contentType}'. The sheet may require a Google login.");
        }

        if (data == null
            || data.Length < 4
            || data[0] != (byte)'P'
            || data[1] != (byte)'K'
            || data[2] != 3
            || data[3] != 4)
        {
            throw new InvalidDataException(
                "The response is not an XLSX ZIP file. The sheet may be private or the export URL returned an HTML page.");
        }

        try
        {
            using (var stream = new MemoryStream(data, false))
            {
                var workbook = new XSSFWorkbook(stream);
                if (workbook.NumberOfSheets == 0)
                {
                    throw new InvalidDataException("The downloaded workbook contains no sheets.");
                }
            }
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidDataException("The downloaded XLSX workbook could not be opened.", exception);
        }
    }

    private static void WriteFileAtomically(string destinationPath, byte[] data)
    {
        string directory = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException("Could not resolve the workbook directory.");
        Directory.CreateDirectory(directory);

        string temporaryPath = destinationPath + "." + Guid.NewGuid().ToString("N") + ".download";
        try
        {
            File.WriteAllBytes(temporaryPath, data);
            if (File.Exists(destinationPath))
            {
                File.Replace(temporaryPath, destinationPath, null);
            }
            else
            {
                File.Move(temporaryPath, destinationPath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private void SaveSettings()
    {
        EditorPrefs.SetString(PrefKeyId, sheetId ?? string.Empty);
        EditorPrefs.SetString(PrefKeyPath, saveFolderPath ?? DefaultSaveFolder);
        EditorPrefs.SetString(PrefKeyFile, saveFileName ?? DefaultSaveFileName);
    }
}
