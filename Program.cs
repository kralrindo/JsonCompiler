using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

class Program
{
    static readonly string[] SortPriorities = { "shadow", "prepass", "vsm", "tightshadow", "colpass" };
    static StreamWriter? logWriter;

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
            // Initialize log file
            logWriter = new StreamWriter(logFilePath, append: false);
            logWriter.WriteLine($"Json Compiler Starting...");

            // Set up logging to both console and file
            var multiWriter = new MultiTextWriter(Console.Out, logWriter);
            Console.SetOut(multiWriter);

            // Process folders and files
            List<JObject> stgsObjects = new List<JObject>(); // Settings files (.stgs)
            List<JObject> dtblObjects = new List<JObject>(); // Datatable files (.csv)
            List<JObject> shdsObjects = new List<JObject>(); // Shaderset files (.msw)
            List<JObject> txtrObjects = new List<JObject>(); // Texture files   (.dds)
            List<JObject> matlObjects = new List<JObject>(); // Material files (.json)
            List<JObject> rrigObjects = new List<JObject>(); // Animation rigs (.rrig)
            List<JObject> rmdlObjects = new List<JObject>(); // 3D model files (.rmdl)

            ProcessFolder(folderPath, folderPath, stgsObjects, dtblObjects, shdsObjects, txtrObjects, matlObjects, rrigObjects, rmdlObjects);

            // Sort and write output JSON
            SortMatlObjects(matlObjects);

            List<JObject> allObjects = new List<JObject>();
            allObjects.AddRange(stgsObjects);
            allObjects.AddRange(dtblObjects);
            allObjects.AddRange(shdsObjects);
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

    static void ProcessFolder(string folderPath, string rootFolder, List<JObject> stgsObjects, List<JObject> dtblObjects, List<JObject> shdsObjects, List<JObject> txtrObjects, List<JObject> matlObjects, List<JObject> rrigObjects, List<JObject> rmdlObjects)
    {
        if (!Directory.Exists(folderPath))
        {
            Log($"Directory not found: {folderPath}");
            return;
        }

        foreach (var file in Directory.GetFiles(folderPath))
        {
            if (file.EndsWith(".set", StringComparison.OrdinalIgnoreCase))
            {
                //ProcessStgsFile(file, rootFolder, stgsObjects); //disable temporary, repak doesn't support it yet
            }
            else if (file.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                ProcessDtblFile(file, rootFolder, dtblObjects);
            }
            else if (file.EndsWith(".msw", StringComparison.OrdinalIgnoreCase))
            {
                ProcessAssetFile(file, rootFolder, shdsObjects, "shds", "shaderset");
            }
            else if (file.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
            {
                ProcessAssetFile(file, rootFolder, txtrObjects, "txtr", "texture");
            }
            else if (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
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
            ProcessFolder(subFolder, rootFolder, stgsObjects, dtblObjects, shdsObjects, txtrObjects, matlObjects, rrigObjects, rmdlObjects);
        }
    }

    static void Log(string message)
    {
        Console.WriteLine(message); // This will now write to both Console and log file
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

    static void ProcessJsonFile(string filePath, string rootFolder, List<JObject> matlObjects, string assetType)
    {
        try
        {
            string relativePath = Path.GetRelativePath(rootFolder, filePath).Replace("\\", "/");
            string modifiedPath = Path.ChangeExtension(relativePath, ".rpak");

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
            Log($"Processed a texture with the asset type {assetType}. Path: {modifiedPath}");
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
