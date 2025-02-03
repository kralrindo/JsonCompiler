using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

class Program
{
    static readonly string[] SortPriorities = { "_shadow_", "_prepass_", "_vsm_", "_tightshadow_", "_colpass_" };
    static StreamWriter? logWriter;

    // The set to store database of filenames.
    static HashSet<string> skipList = new HashSet<string>();

    static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Log("Please provide the folder path as a command-line argument.");
            return;
        }

        string folderPath = args[0];
        string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt");

        try
        {
            // Load the database
            string skipListFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "R5Reloaded_Asset_Database.db");

            LoadSkipList(skipListFilePath);

            // Initialize log file
            logWriter = new StreamWriter(logFilePath, append: false);
            logWriter.WriteLine($"Json Compiler Starting...");

            // Set up logging to both console and file
            var multiWriter = new MultiTextWriter(Console.Out, logWriter);
            Console.SetOut(multiWriter);

            // Process folders and files
            List<JObject> stltObjects = new List<JObject>(); // Stlayoout files (.stlt)
            List<JObject> stgsObjects = new List<JObject>(); // Settings files  (.stgs)
            List<JObject> dtblObjects = new List<JObject>(); // Datatable files (.csv)
            List<JObject> shdsObjects = new List<JObject>(); // Shaderset files (.msw)
            List<JObject> txanObjects = new List<JObject>(); // TXAN files      (.txan)
            List<JObject> txtrObjects = new List<JObject>(); // Texture files   (.dds)
            List<JObject> matlObjects = new List<JObject>(); // Material files  (.json)
            List<JObject> rrigObjects = new List<JObject>(); // Animation rigs  (.rrig)
            List<JObject> rmdlObjects = new List<JObject>(); // 3D model files  (.rmdl)

            ProcessFolder(folderPath, folderPath, stltObjects, stgsObjects, dtblObjects, shdsObjects, txanObjects, txtrObjects, matlObjects, rrigObjects, rmdlObjects);

            // Sort and write output JSON
            SortMatlObjects(matlObjects);

            List<JObject> allObjects = new List<JObject>();
            allObjects.AddRange(stltObjects);
            allObjects.AddRange(stgsObjects);
            allObjects.AddRange(dtblObjects);
            allObjects.AddRange(shdsObjects);
            allObjects.AddRange(txanObjects);
            allObjects.AddRange(txtrObjects);
            allObjects.AddRange(matlObjects);
            allObjects.AddRange(rrigObjects);
            allObjects.AddRange(rmdlObjects);

            JObject outputJson = new JObject
            {
                { "version", 8 },
                { "keepDevOnly", true },
                { "name", "output" },
                { "streamFileMandatory", "paks/Win64/output.starpak" },
                { "streamFileOptional", "paks/Win64/output.opt.starpak" },
                { "assetsDir", "./assets/" },
                { "outputDir", "./build/" },
                { "compressLevel", 19 },
                { "compressWorkers", 16 },
                { "files", JArray.FromObject(allObjects) }
            };

            string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string outputPath = Path.Combine(exeDirectory, "output.json");

            File.WriteAllText(outputPath, JsonConvert.SerializeObject(outputJson, Formatting.Indented));
            Log($"Asset path successfully converted into a JSON");
            Log($"Output Location: {outputPath}");
        }
        catch (Exception ex)
        {
            Log($"Error: {ex.Message}");
        }
        finally
        {
            // Close log file
            logWriter?.WriteLine($"Conversion ended at: {DateTime.Now}");
            logWriter?.Close();
        }
    }

    static void LoadSkipList(string skipListFilePath)
    {
        try
        {
            if (File.Exists(skipListFilePath))
            {
                var lines = File.ReadAllLines(skipListFilePath);
                foreach (var line in lines)
                {
                    // Trim whitespace and add to skip list if not empty
                    var trimmedLine = line.Trim();
                    if (!string.IsNullOrEmpty(trimmedLine))
                    {
                        skipList.Add(trimmedLine);
                    }
                }
                Log($"Found R5Reloaded Database!: {skipListFilePath}");
            }
            else
            {
                Log($"R5Reloaded Database not found: {skipListFilePath}");
            }
        }
        catch (Exception ex)
        {
            Log($"Error loading R5Reloaded Database: {ex.Message}");
        }
    }

    static void ProcessFolder(string folderPath, string rootFolder, List<JObject> stltObjects, List<JObject> stgsObjects, List<JObject> dtblObjects, List<JObject> shdsObjects, List<JObject> txanObjects, List<JObject> txtrObjects, List<JObject> matlObjects, List<JObject> rrigObjects, List<JObject> rmdlObjects)
    {
        if (!Directory.Exists(folderPath))
        {
            Log($"Directory not found: {folderPath}");
            return;
        }

        foreach (var file in Directory.GetFiles(folderPath))
        {
            string fileName = Path.GetFileName(file).Replace("\\", "/");
            string stgspath = folderPath.Replace("\\", "/");

            // Check if the file is in the database
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file).ToLowerInvariant().Replace("\\", "/"); // To make it case-insensitive and without extension
            if (skipList.Contains(fileNameWithoutExtension, StringComparer.OrdinalIgnoreCase))
            {
                Log($"Found an asset that was already in R5Reloaded: {fileNameWithoutExtension}, skipping!"); // Log the skipped file name
                continue; // Skip this file
            }
            else if (skipList.Contains(fileName, StringComparer.OrdinalIgnoreCase))
            {
                Log($"Found an asset that was already in R5Reloaded: {fileName}, skipping!"); // Log the skipped file name
                continue; // Skip this file
            }
            else if (file.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                if (folderPath.Contains("datatable", StringComparison.OrdinalIgnoreCase))
                    ProcessDtblFile(file, rootFolder, dtblObjects);
            }
            else if (file.EndsWith(".txan", StringComparison.OrdinalIgnoreCase))
            {
                ProcessAssetFile(file, rootFolder, txanObjects, "txan", "texture_anim");
            }
            else if (file.EndsWith(".msw", StringComparison.OrdinalIgnoreCase))
            {
                if (folderPath.Contains("shaderset", StringComparison.OrdinalIgnoreCase))
                    ProcessAssetFile(file, rootFolder, shdsObjects, "shds", "shaderset");
                else if (folderPath.Contains("shader", StringComparison.OrdinalIgnoreCase))
                    ProcessAssetFile(file, rootFolder, shdsObjects, "shdr", "shader");
                else
                    Console.Error.WriteLine($"Asset {file} is in unsupported folder!");
            }
            else if (file.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
            {
                ProcessAssetFile(file, rootFolder, txtrObjects, "txtr", "texture");
            }
            else if (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                if (folderPath.Contains("settings_layout", StringComparison.OrdinalIgnoreCase))
                    ProcessStltFile(file, rootFolder, stltObjects);
                if (stgspath.Contains("settings/", StringComparison.OrdinalIgnoreCase))
                    ProcessStgsFile(file, rootFolder, stgsObjects); // temporarily disabled
                else
                    ProcessJsonFile(file, rootFolder, matlObjects, "matl");
            }
            else if (file.EndsWith(".rrig", StringComparison.OrdinalIgnoreCase))
            {
                ProcessRrigFile(file, rootFolder, rrigObjects);
            }
            else if (file.EndsWith(".rmdl", StringComparison.OrdinalIgnoreCase))
            {
                ProcessRmdlFile(file, rootFolder, rmdlObjects);
            }
        }

        foreach (var subFolder in Directory.GetDirectories(folderPath))
        {
            ProcessFolder(subFolder, rootFolder, stltObjects, stgsObjects, dtblObjects, shdsObjects, txanObjects, txtrObjects, matlObjects, rrigObjects, rmdlObjects);
        }
    }

    static void Log(string message)
    {
        Console.WriteLine(message); // This will now write to both Console and log file
    }


    static void ProcessStltFile(string filePath, string rootFolder, List<JObject> stltObjects)
    {
        try
        {
            string relativePath = Path.GetRelativePath(rootFolder, filePath) // Remove stlt folder from the path, rsx cant export stlt yet but i'm guessing this is what it's gonna use
                          .Replace("\\", "/")
                          .Replace("stlt/", "");
            string modifiedPath = Path.ChangeExtension(relativePath, ".rpak");

            JObject newJsonObject = new JObject
            {
                { "_type", "stlt" },
                { "_path", modifiedPath }
            };

            stltObjects.Add(newJsonObject);
            Log($"Processed a settings layout asset! Path: {modifiedPath}");
        }
        catch (Exception ex)
        {
            Log("Error processing stlt: " + ex.Message);
        }
    }

    static void ProcessStgsFile(string filePath, string rootFolder, List<JObject> dtblObjects)
    {
        try
        {
            string relativePath = Path.GetRelativePath(rootFolder, filePath) // Remove stgs folder from the path, rsx exports settings folder as "stgs" but game uses "settings"
                          .Replace("\\", "/")
                          .Replace("stgs/", "");
            string modifiedPath = Path.ChangeExtension(relativePath, ".rpak");

            JObject newJsonObject = new JObject
            {
                { "_type", "stgs" },
                { "_path", modifiedPath }
            };

            dtblObjects.Add(newJsonObject);
            Log($"Processed a setting asset! Path: {modifiedPath}");
        }
        catch (Exception ex)
        {
            Log("Error processing stgs: " + ex.Message);
        }
    }

    static void ProcessDtblFile(string filePath, string rootFolder, List<JObject> dtblObjects)
    {
        try
        {
            string relativePath = Path.GetRelativePath(rootFolder, filePath) // Remove dtbl folder from the path, rsx exports datatable folder as "dtbl" but game uses "datatable"
                          .Replace("\\", "/")
                          .Replace("dtbl/", "");
            string modifiedPath = Path.ChangeExtension(relativePath, ".rpak");

            JObject newJsonObject = new JObject
	        {
	            { "_type", "dtbl" },
	            { "_path", modifiedPath }
	        };

            dtblObjects.Add(newJsonObject);
            Log($"Processed a datatable! Path: {modifiedPath}");
        }
        catch (Exception ex)
        {
            Log("Error processing datatable: " + ex.Message);
        }
    }

    static void ProcessJsonFile(string filePath, string rootFolder, List<JObject> matlObjects, string assetType)
    {
        try
        {
            // Check if the file path contains the word "material" (case-insensitive)
            if (!filePath.Contains("material", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string relativePath = Path.GetRelativePath(rootFolder, filePath).Replace("\\", "/");
            string modifiedPath = Path.ChangeExtension(relativePath, ".rpak");
            string databasepath = Path.ChangeExtension(Path.GetRelativePath(rootFolder, filePath), ".rpak");

            if (skipList.Contains(databasepath, StringComparer.OrdinalIgnoreCase))
            {
                Log($"Found an asset that was already in R5Reloaded: {databasepath}, skipping!");
                return; // Skip this file
            }

            JObject newJsonObject = new JObject
            {
                { "_type", assetType },
                { "_path", modifiedPath }
            };

            matlObjects.Add(newJsonObject);
            Log($"Processed a material JSON! Path: {modifiedPath}");
        }
        catch (Exception ex)
        {
            Log("Error processing material JSON file: " + ex.Message);
        }
    }

    static void ProcessAssetFile(string filePath, string rootFolder, List<JObject> assetObjects, string assetType, string pathPrefix)
    {
        try
        {
            string relativePath = Path.GetRelativePath(rootFolder, filePath).Replace("\\", "/");
            string modifiedPath = $"{pathPrefix}/{Path.GetFileNameWithoutExtension(relativePath)}.rpak";
                
            string guid = Path.GetFileNameWithoutExtension(filePath);
            JObject newJsonObject = new JObject
            {
                { "_type", assetType },
                { "_path", modifiedPath },
                { "$guid", guid }
            };

            assetObjects.Add(newJsonObject);
            Log($"Processed an asset with the asset type {assetType}. Path: {modifiedPath}");
        }
        catch (Exception ex)
        {
            Log($"Error processing {assetType} file: " + ex.Message);
        }
    }

    static void SortMatlObjects(List<JObject> matlObjects)
    {
        matlObjects.Sort((x, y) =>
        {
            string pathX = x["_path"]?.ToString() ?? "";
            string pathY = y["_path"]?.ToString() ?? "";

            int indexX = GetPriorityIndex(pathX);
            int indexY = GetPriorityIndex(pathY);

            int comparison = indexX.CompareTo(indexY);
            return comparison == 0 ? string.Compare(pathX, pathY, StringComparison.OrdinalIgnoreCase) : comparison;
        });
    }

    static void ProcessRrigFile(string filePath, string rootFolder, List<JObject> rrigObjects)
    {
        try
        {
            string relativePath = Path.GetRelativePath(rootFolder, filePath).Replace("\\", "/");

            JObject newJsonObject = new JObject
            {
                { "_type", "arig" },
                { "_path", relativePath }
            };

            Log($"Processed an animation RIG! Path: {relativePath}");


            string rsonPath = Path.ChangeExtension(filePath, ".rson");
            if (File.Exists(rsonPath))
            {
                Log($"Found RSON configuration for the animation RIG: {rsonPath}");
                var rsonData = File.ReadAllLines(rsonPath);
                List<string> sequences = ExtractSection(rsonData, "seqs");

                if (sequences.Count > 0)
                {
                    newJsonObject.Add("$sequences", JArray.FromObject(sequences));
                    Log($"Extracted animation sequences: {string.Join(", ", sequences)}");
                }
            }

            rrigObjects.Add(newJsonObject);
        }
        catch (Exception ex)
        {
            Log("Error processing RRIG file: " + ex.Message);
        }
    }

    static void ProcessRmdlFile(string filePath, string rootFolder, List<JObject> rmdlObjects)
    {
        try
        {
            string relativePath = Path.GetRelativePath(rootFolder, filePath).Replace("\\", "/");
            string databasepath = Path.GetRelativePath(rootFolder, filePath);

            if (skipList.Contains(databasepath, StringComparer.OrdinalIgnoreCase))
            {
                Log($"Found an asset that was already in R5Reloaded: {databasepath}, skipping!");
                return; // Skip this file
            }
            JObject newJsonObject = new JObject
        {
            { "_type", "mdl_" },
            { "_path", relativePath }
        };

            Log($"Processed a RMDL! Path: {relativePath}");

            string rsonPath = Path.ChangeExtension(filePath, ".rson");
            if (File.Exists(rsonPath))
            {
                Log($"Found RSON configuration for the RMDL: {relativePath}");
                var rsonData = File.ReadAllLines(rsonPath);
                List<string> animrigs = ExtractSection(rsonData, "rigs");
                List<string> sequences = ExtractSection(rsonData, "seqs");

                if (animrigs.Count > 0)
                {
                    newJsonObject.Add("$animrigs", JArray.FromObject(animrigs));
                    Log($"Extracted animation RIGs: {string.Join(", ", animrigs)}");
                }

                if (sequences.Count > 0)
                {
                    newJsonObject.Add("$sequences", JArray.FromObject(sequences));
                    Log($"Extracted animation sequences: {string.Join(", ", sequences)}");
                }

                if (sequences.Count == 0 && animrigs.Count == 0)
                {
                    Log($"RSON file is empty: {rsonPath}.");
                }
            }

            rmdlObjects.Add(newJsonObject);
        }
        catch (Exception ex)
        {
            Log("Error processing RMDL file: " + ex.Message);
        }
    }

    static List<string> ExtractSection(string[] rsonData, string sectionName)
    {
        List<string> items = new List<string>();
        bool inSection = false;

        foreach (var line in rsonData)
        {
            if (line.Trim().StartsWith($"{sectionName}:", StringComparison.OrdinalIgnoreCase))
            {
                inSection = true;
                continue;
            }

            if (inSection)
            {
                if (line.Trim() == "]")
                {
                    break;
                }

                string item = line.Trim().TrimStart('[').TrimEnd(',', ']', ' ');
                if (!string.IsNullOrEmpty(item))
                {
                    items.Add(item.Replace("\\", "/"));
                }
            }
        }

        return items;
    }

    static int GetPriorityIndex(string path)
    {
        for (int i = 0; i < SortPriorities.Length; i++)
        {
            if (path.IndexOf(SortPriorities[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return i;
            }
        }
        return SortPriorities.Length;
    }
}

// Custom TextWriter that writes to both the console and the log file
public class MultiTextWriter : TextWriter
{
    private readonly TextWriter _consoleWriter;
    private readonly TextWriter _fileWriter;

    public MultiTextWriter(TextWriter consoleWriter, TextWriter fileWriter)
    {
        _consoleWriter = consoleWriter;
        _fileWriter = fileWriter;
    }

    public override void WriteLine(string? value)  // Match the base method's nullability
    {
        _consoleWriter.WriteLine(value); // Write to console
        _fileWriter.WriteLine(value);    // Write to log file
    }

    public override Encoding Encoding => _consoleWriter.Encoding;
}