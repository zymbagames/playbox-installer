#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor.PackageManager;
using UnityEngine.Networking;


public class PackageInstaller
{
    private static string ManifestPath => Path.Combine(Application.dataPath, "../Packages/manifest.json");
    
    private static string facebook_url = "https://lookaside.facebook.com/developers/resources/?id=FacebookSDK-current.zip";
    
    private static string file_directory => Path.Combine(Application.dataPath, "../DownloadFiles/FacebookSDK.zip");
    
    [MenuItem("Playbox/Download Facebook")]
    public static async void DownloadFacebook()
    {
        await DownloadFileAsync(facebook_url,file_directory);
    }
    
    [MenuItem("Playbox/Install Playbox Dependencies")]
    public static void InstallPlayboxDependencies()
    {
        AddPackagesToManifest();
    }
    
    public static async Task<bool> DownloadFileAsync(string url, string outputPath)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.downloadHandler = new DownloadHandlerFile(outputPath);

            var operation = request.SendWebRequest();

            // Ждём завершения загрузки в асинхронном виде
            while (!operation.isDone)
                await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Ошибка загрузки {url}: {request.error}");
                return false;
            }

            Debug.Log($"Файл загружен: {outputPath}");
            return true;
        }
    }
    
    private static void AddPackagesToManifest()
    {
        if (!File.Exists(ManifestPath))
        {
            Debug.LogError("manifest.json not found!");
            return;
        }
        
        var manifestJson = JObject.Parse(File.ReadAllText(ManifestPath));

        var dependencies = (JObject)manifestJson["dependencies"];
        
        var packagesToAdd = new Dictionary<string, string>
        {
            { "com.appsflyer.unity","https://github.com/AppsFlyerSDK/appsflyer-unity-plugin.git#upm" },
            { "com.devtodev.sdk.analytics","https://github.com/devtodev-analytics/package_Analytics.git" },
            { "com.devtodev.sdk.analytics.google","https://github.com/devtodev-analytics/package_Google.git" },
            { "com.google.external-dependency-manager","1.2.186" },
            { "com.applovin.mediation.ads","8.3.1" },
            { "com.google.ads.mobile","10.3.0" }
        };

        foreach (var item in packagesToAdd)
        {
            if (dependencies != null && dependencies[item.Key] == null)
            {
                dependencies[item.Key] = item.Value;
            }
            else
            {
                if (dependencies != null) dependencies[item.Key] = item.Value;
                Debug.Log($"Package {item.Key} already exists!");
            }
        }

        if (manifestJson["scopedRegistries"] == null)
        {
            manifestJson["scopedRegistries"] = new JArray();
        }
        
        var registries = (JArray)manifestJson["scopedRegistries"];

        if (!hasRegistry("applovin", registries))
        {
            registries = AddToRegistry(registries,
                "AppLovin MAX Unity",
                "https://unity.packages.applovin.com/",
                new JArray("com.applovin.mediation.ads",
                    "com.applovin.mediation.adapters",
                    "com.applovin.mediation.dsp"));
        }
        
        if (!hasRegistry("openupm", registries))
        {
            registries = AddToRegistry(registries,
                "package.openupm.com",
                "https://package.openupm.com",
                new JArray("com.google.external-dependency-manager"));
        }


        manifestJson["scopedRegistries"] = registries;
        manifestJson["dependencies"] = dependencies;
        
        File.WriteAllText(ManifestPath, manifestJson.ToString(Newtonsoft.Json.Formatting.Indented));

        AssetDatabase.Refresh();
    }

    static JArray AddToRegistry(JArray registries,string name, string url, JArray scopes)
    {
        var newResigtry = new JObject
        {
            ["name"] = name,
            ["url"] = url,
            ["scopes"] = scopes
        };
        
        registries.Add(newResigtry);

        return registries;
    }

    static bool hasRegistry(string registryName, JArray array)
    {
        if (array == null)
            return false;

        foreach (var item in array)
        {
            if (item["url"] != null && item["url"].ToString().Contains(registryName))
            {
                return true;
            }
        }
        
        return false;
    }
}

#endif