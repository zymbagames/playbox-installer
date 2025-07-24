#if UNITY_EDITOR
using System;
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
    private static string firebase_url = "https://firebase.google.com/download/unity?hl=ru";
    
    [MenuItem("PlayboxInstaller/Download Facebook")]
    public static async void DownloadFacebook()
    {
        await DownloadFileAsync(facebook_url,Path.Combine(Application.dataPath,"../DownloadFiles/FacebookSDK.zip"));
    }
    
    [MenuItem("PlayboxInstaller/Fix Facebook Error")]
    public static  void FixFacebookError()
    {
        AssetDatabase.Refresh();
        AssetDatabase.ImportAsset("Assets", ImportAssetOptions.ImportRecursive);
        AssetDatabase.Refresh();
        
        Client.Resolve();

        EditorUtility.DisplayDialog("Reimport", "Reimport completed.", "OK");
    }
    
    [MenuItem("PlayboxInstaller/Download Firebase")]
    public static async void DownloadFirebase()
    {
        await DownloadFileAsync(firebase_url,Path.Combine(Application.dataPath,"../DownloadFiles/Firebase.zip"));
    }
    
    [MenuItem("PlayboxInstaller/Install Playbox Dependencies")]
    public static void InstallPlayboxDependencies()
    {
        AddPackagesToManifest();
    }
    
    public static async Task<bool> DownloadFileAsync(string url, string outputPath)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("User-Agent", "Mozilla/5.0 (UnityDownloader)");
            
            request.downloadHandler = new DownloadHandlerFile(outputPath);

            var operation = request.SendWebRequest();
            
            while (!operation.isDone)
            {
                EditorUtility.DisplayProgressBar(
                    "Download file",
                    $"Download {Path.GetFileName(outputPath)}",
                    request.downloadProgress
                );
                await Task.Yield();
            }
            
            EditorUtility.ClearProgressBar();
            
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"File loading error {url}: {request.error}");
                return false;
            }

            Debug.Log($"File loaded : {outputPath}");
            return true;
        }
    }

    [MenuItem("PlayboxInstaller/Install PlayboxSDK")]
    public static void InstallPlayboxSDK()
    {
        if (!IsFirebaseAvailable())
        {
            Debug.Log("Firebase is not installed");
            Debug.Log("Please install the: Firebase.Analytics, Firebase.Crashlytics");
            return;
        }
        
        if (!IsFacebookAvailable())
        {
            Debug.Log("Facebook is not installed");
            Debug.Log("Please install the: FaceboockSDK");
            return;
        }

        var request = Client.Add("https://github.com/dreamsim-dev/PlayboxSdk.git#main");
        
        EditorApplication.update += Update;

        void Update()
        {
            if (request.IsCompleted)
            {
                if (request.Status == StatusCode.Success)
                {
                    Debug.Log("Playbox SDK Installed");
                }else if (request.Status == StatusCode.Failure)
                {
                    Debug.Log("Playbox SDK Installation failed");
                }
                
                EditorApplication.update -= Update;
            }
        }
    }

    [MenuItem("PlayboxInstaller/Upgrade PlayboxSDK")]
    public static void PlayboxUpgrade()
    {
        var request = Client.Add("https://github.com/dreamsim-dev/PlayboxSdk.git#main");
        
        EditorApplication.update += Update;

        void Update()
        {
            if (request.IsCompleted)
            {
                if (request.Status == StatusCode.Success)
                {
                    Debug.Log("Playbox SDK Upgraded");
                }
                EditorApplication.update -= Update;
            }
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
            { "com.google.ads.mobile","10.3.0" },
            { "com.unity.ads.ios-support", "1.0.0" }
            //{ "playbox", "https://github.com/dreamsim-dev/PlayboxSdk.git#main" }
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
    
    public static bool IsFirebaseAvailable()
    {
        try
        {
            var type = Type.GetType("Firebase.FirebaseApp, Firebase.App, Firebase.Crashlytics.Crashlytics, Firebase.Analytics.FirebaseAnalytics");
            return type != null;
        }
        catch
        {
            return false;
        }
    }
    
    public static bool IsFacebookAvailable()
    {
        try
        {
            var type = Type.GetType("Facebook.Unity.FB, Facebook.Unity");
            return type != null;
        }
        catch
        {
            return false;
        }
    }
}

#endif