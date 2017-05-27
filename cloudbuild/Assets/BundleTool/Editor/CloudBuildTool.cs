using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using MiniJSON;
using System.IO;

#if !UNITY_CLOUD_BUILD
namespace UnityEngine.CloudBuild
{
    public class BuildManifestObject
    {
        Dictionary<string, object> m_Dict = null;

        public BuildManifestObject(Dictionary<string, object> dict)
        {
            m_Dict = dict;
        }

        public string GetValue(string key, string defaultValue)
        {
            object value;
            if (m_Dict.TryGetValue(key, out value))
            {
                return value as string;
            }

            return defaultValue;
        }
    }
}
#endif

public class CloudBuildTool : MonoBehaviour
{
    static Dictionary<string, string> Bundle2Files;

#if !UNITY_CLOUD_BUILD
    [MenuItem("BuildTool/Simulate CloudBuild")]
    public static void SimulateCloudBuild()
    {
        var json = (TextAsset)Resources.Load("UnityCloudBuildManifest.json");
        if (json != null)
        {
            var manifestDict = Json.Deserialize(json.text) as Dictionary<string, object>;
            UnityEngine.CloudBuild.BuildManifestObject manifest = new UnityEngine.CloudBuild.BuildManifestObject(manifestDict);
            CloudBuildTool.PreExport(manifest);
            CloudBuildTool.PostExport();
        }
    }
#endif

    public static void PreExport(UnityEngine.CloudBuild.BuildManifestObject manifest)
    {
        Debug.Log("==================== PreExport ====================");

        string buildNumber = manifest.GetValue("buildNumber", "unknown");
        string buildStartTime = manifest.GetValue("buildStartTime", "5/22/2017 2:57:03 AM");

        PlatformUtil.ApplicationVersion = String.Format("0.1.0.{0}", buildNumber);
        PlatformUtil.StreamingBundle = buildNumber;

#if LOAD_RESOURCES
        Debug.Log("BUILD ASSET RESOURCES...");

        string streamingBundlePath = Application.streamingAssetsPath + ResourceMgr.BundlePath;
        while (Directory.Exists(streamingBundlePath))
            FileUtil.DeleteFileOrDirectory(streamingBundlePath);
#else
        Debug.Log("BUILD ASSET ASSETBUNDLES...");

        BundleTool bundleTool = BundleTool.Initialize();
        Bundle2Files = bundleTool.BuildAssetBundlesName();

        string assetsPath = Application.dataPath.Substring(0, Application.dataPath.IndexOf("Assets"));
        foreach (var keyValue in Bundle2Files)
        {
            string metaPath = assetsPath + keyValue.Value + ".meta";
            string meta = File.ReadAllText(metaPath);
            Debug.Log("[" + keyValue.Key + "]\n" + meta);
        }

        bundleTool.BuildAssetBundles(buildNumber, true);

    #if UNITY_CLOUD_BUILD
        FileUtil.MoveFileOrDirectory("Assets/Resources", "Assets/__Resources__");
    #endif
#endif

        Debug.Log("ApplicationVersion: " + PlatformUtil.ApplicationVersion);
        Debug.Log("StreamingBundle: " + PlatformUtil.StreamingBundle);
    }

    public static void PostExport()
    {
        Debug.Log("==================== PostExport ====================");

        string platformName = PlatformUtil.GetPlatformName();

#if !LOAD_RESOURCES
        string manifestPath = PlatformUtil.GetRuntimeStreamingAssetsPath(Application.platform) + ResourceMgr.BundlePath + "/" + platformName + "/manifest";
        using (FileStream fs = File.OpenRead(manifestPath))
        {
            using (var fr = new StreamReader(fs))
            {
                Debug.Log("BUNDLE VERSION: " + fr.ReadLine());

                string line;
                while ((line = fr.ReadLine()) != null)
                {
                    Debug.Log(line);
                }
            }
        }

        try
        {
            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo.FileName = "rcp";
            proc.StartInfo.UseShellExecute = true;
            bool ret = proc.Start();

            Debug.Log("SHELL~~~ OK: " + ret);
        }
        catch (Exception e)
        {
            Debug.Log("SHELL~~~ FAILED...");
        }
#endif
    }
}

