
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

public class BundleTool : EditorWindow
{
    delegate void OnFileIterated(System.IO.FileInfo info);

    enum OutputType
    {
        None,
        Scenes,
        Bundles,
    }

    bool isBuilding = false;
    bool syncToClient = true;

    bool disableWriteTypeTree = true;
    bool ignoreTypeTreeChanges = false;
    bool appendHashToAssetBundleName = false;

    enum Compression
    {
        None,
        LZMA,
        LZ4,
    }

    Compression compressionMode = Compression.LZMA;

    PlatformUtil.Platform platform = PlatformUtil.Platform.IPhone;
    OutputType outputType = OutputType.Bundles;
    Vector2 scrollPosition;

    const string SimulateLoadBundleMenu = "BuildTool/Simulate LoadBundle";

#if UNITY_EDITOR
    [MenuItem(SimulateLoadBundleMenu)]
    public static void ToggleSimulatePatching()
    {
        ResourceMgr.SimulateLoadBundle = !ResourceMgr.SimulateLoadBundle;
    }
#endif

    [MenuItem(SimulateLoadBundleMenu, true)]
    public static bool ToggleSimulatePatchingValidate()
    {
        Menu.SetChecked(SimulateLoadBundleMenu, ResourceMgr.SimulateLoadBundle);
        return true;
    }

    [MenuItem("BuildTool/Clean Bundle Cache Path")]
    public static void CleanBundleCachePath()
    {
        PatchMgr.CleanBundleCachePath();
    }

    [MenuItem("BuildTool/Clean Editor Bundle Path")]
    public static void CleanEditorBundlePath()
    {
        string[] allDirs = Directory.GetDirectories(PatchMgr.EditorBundlePath);
        HashSet<string> delDirs = new HashSet<string>();

        INIParser ini = new INIParser();
        ini.Open(Path.Combine(PatchMgr.EditorBundlePath, "BundleTool.ini"));

        for (int i = 0; i < (int)PlatformUtil.Platform.MAX; i++)
        {
            string name = PlatformUtil.Platform2Name((PlatformUtil.Platform)i);
            if (name != null)
            {
                string version = ini.ReadValue("BundleTool", name, "");
                if (!string.IsNullOrEmpty(version))
                {
                    foreach (string dir in allDirs)
                    {
                        if (dir.IndexOf(name) != -1 && dir.IndexOf(version) == -1)
                        {
                            delDirs.Add(dir);
                        }
                    }
                }
            }
        }

        foreach (string dir in delDirs)
        {
            while (Directory.Exists(dir))
                FileUtil.DeleteFileOrDirectory(dir);
        }

        ini.Close();
    }

    [MenuItem("BuildTool/Caching.CleanCache")]
    public static void CleanCache()
    {
        Caching.CleanCache();
    }

    [MenuItem("BuildTool/Build Bundle")]
    public static BundleTool Initialize()
    {
        //Rect rect = new Rect(0, 0, 400, 800);
        BundleTool wnd = (BundleTool)EditorWindow.GetWindow(typeof(BundleTool), true);
        wnd.platform = PlatformUtil.BuildTarget2Platform(EditorUserBuildSettings.activeBuildTarget);
        wnd.Show();
        return wnd;
    }

    void Update()
    {
        if (isBuilding)
        {
            isBuilding = false;
            string buildStartTime = DateTime.Now.ToString("yyMMdd_HHmmss");
            BuildAssetBundlesName();
            BuildAssetBundles(buildStartTime, syncToClient);
        }
    }

    void OnGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Box("Output Path");
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Browser..."))
        {
            string path = EditorUtility.SaveFolderPanel("Select bundle output path.", PatchMgr.EditorBundlePath, "");
            if (!string.IsNullOrEmpty(path))
            {
                PatchMgr.EditorBundlePath = path;
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Label(PatchMgr.EditorBundlePath);
        GUILayout.Box("Config");

        GUILayout.BeginHorizontal();
        GUILayout.Label("Sync to client:");
        GUILayout.FlexibleSpace();
        syncToClient = GUILayout.Toggle(syncToClient, "");
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Platform:");
        GUILayout.FlexibleSpace();
        platform = (PlatformUtil.Platform)(EditorGUILayout.EnumPopup(platform));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical();
        GUILayout.Label("Option:");
        GUILayout.EndVertical();

        GUILayout.FlexibleSpace();

        GUILayout.BeginVertical();
        disableWriteTypeTree = GUILayout.Toggle(disableWriteTypeTree, "DisableWriteTypeTree");
        ignoreTypeTreeChanges = GUILayout.Toggle(ignoreTypeTreeChanges, "IgnoreTypeTreeChanges");
        //appendHashToAssetBundleName = GUILayout.Toggle(appendHashToAssetBundleName, "AppendHashToBundleName");
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        if (GUILayout.Button("BUILD ASSETBUNDLES"))
        {
            isBuilding = true;
        }

        GUILayout.Box("Output");
        GUILayout.BeginHorizontal();
        GUILayout.Label("Type:");
        GUILayout.FlexibleSpace();
        outputType = (OutputType)EditorGUILayout.EnumPopup(outputType);
        GUILayout.EndHorizontal();

        if (outputType != OutputType.None)
        {
            EditorGUIUtility.SetIconSize(new Vector2(15, 15));

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            if (outputType == OutputType.Scenes)
            {

                EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
                foreach (EditorBuildSettingsScene scn in scenes)
                {
                    GUILayout.BeginHorizontal();
                    Texture ico = AssetDatabase.GetCachedIcon(scn.path);
                    GUILayout.Label(new GUIContent(scn.path, ico));
                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                var names = AssetDatabase.GetAllAssetBundleNames();
                foreach (var name in names)
                {
                    GUILayout.BeginHorizontal();
                    Texture ico = AssetDatabase.GetCachedIcon(name);
                    GUILayout.Label(new GUIContent(name, ico));
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
        }
    }

    BuildAssetBundleOptions Configure2BundleOptions()
    {
        BuildAssetBundleOptions ret = BuildAssetBundleOptions.None;

        if (disableWriteTypeTree)
            ret |= BuildAssetBundleOptions.DisableWriteTypeTree;

        if (ignoreTypeTreeChanges)
            ret |= BuildAssetBundleOptions.IgnoreTypeTreeChanges;

        if (appendHashToAssetBundleName)
            ret |= BuildAssetBundleOptions.AppendHashToAssetBundleName;

        if (compressionMode == Compression.None)
            ret |= BuildAssetBundleOptions.UncompressedAssetBundle;
        else if (compressionMode == Compression.LZ4)
            ret |= BuildAssetBundleOptions.ChunkBasedCompression;

        return ret;
    }

    public Dictionary<string, string> BuildAssetBundlesName()
    {
        string[] bundles = AssetDatabase.GetAllAssetBundleNames();
        foreach (string bundle in bundles)
        {
            AssetDatabase.RemoveAssetBundleName(bundle, true);
        }

        Dictionary<string, string> bundle2Files = new Dictionary<string, string>();

        BuildAllScenesName(ref bundle2Files);
        BuildAllResourcesName(ref bundle2Files);
        AssetDatabase.Refresh();

        return bundle2Files;
    }

    public void BuildAssetBundles(string manifestPrefix, bool syncToClient)
    {
        BuildTarget tar = PlatformUtil.Platform2BuildTarget(platform);

        string platformName = PlatformUtil.Platform2Name(platform);
        string manifestName = manifestPrefix + "_" + platformName;
        //Debug.Log("buildStartTime: " + buildStartTime + " -> " + manifestName);

        //Use DeterministicAssetBundle flag to avoid cross-guid of rebuilding 
        BuildAssetBundleOptions opt = BuildAssetBundleOptions.DeterministicAssetBundle;
        opt |= Configure2BundleOptions();

        string outputPath = PatchMgr.EditorBundlePath + "/" + manifestName;
        while (Directory.Exists(outputPath))
            FileUtil.DeleteFileOrDirectory(outputPath);

        Directory.CreateDirectory(outputPath);

        //Dictionary<string, string> bundle2Files;
        //BuildAssetBundlesName(out bundle2Files);

        BuildPipeline.BuildAssetBundles(outputPath, opt, tar);

        // Load manifest.
        AssetBundle assetbundle = AssetBundle.LoadFromFile(outputPath + "/" + manifestName);
        AssetBundleManifest manifest = assetbundle.LoadAsset<AssetBundleManifest>(ResourceMgr.ManifestName);
        assetbundle.Unload(false);

        using (FileStream fs = File.OpenWrite(outputPath + "/manifest"))
        {
            using (var fw = new StreamWriter(fs))
            {
                fw.WriteLine(manifestName);

                string[] bundles = manifest.GetAllAssetBundles();
                foreach (string bundle in bundles)
                {
                    Hash128 hash = manifest.GetAssetBundleHash(bundle);

                    string sha = "";
                    byte[] bytes = File.ReadAllBytes(outputPath + "/" + bundle);
                    using (SHA1 sha1 = SHA1.Create())
                    {
                        //Encoding enc = Encoding.UTF8;

                        StringBuilder sb = new StringBuilder();
                        Byte[] result = sha1.ComputeHash(bytes);

                        foreach (Byte b in result)
                            sb.Append(b.ToString("x2"));

                        sha = sb.ToString();
                    }

                    fw.WriteLine(bundle + ", hash: " + hash + ", sha: " + sha);
                }
            }
        }

        if (syncToClient)
        {
            string streamingBundlePath = Application.streamingAssetsPath + ResourceMgr.BundlePath;
            string streamingBundlePlatformPath = streamingBundlePath + "/" + platformName;

            while (Directory.Exists(streamingBundlePlatformPath))
                FileUtil.DeleteFileOrDirectory(streamingBundlePlatformPath);

            if (!Directory.Exists(streamingBundlePath))
                Directory.CreateDirectory(streamingBundlePath);

            FileUtil.CopyFileOrDirectory(outputPath, streamingBundlePlatformPath);

            string manifestPath = streamingBundlePlatformPath + "/" + manifestName;
            FileUtil.MoveFileOrDirectory(manifestPath, streamingBundlePlatformPath + "/" + platformName);

            DirectoryInfo directoryInfo = new DirectoryInfo(streamingBundlePlatformPath);
            FileInfo[] files = directoryInfo.GetFiles("*.manifest", SearchOption.AllDirectories);
            foreach (FileInfo file in files)
            {
                file.Attributes = FileAttributes.Normal;
                File.Delete(file.FullName);
            }
        }

        INIParser ini = new INIParser();
        string iniPath = Path.Combine(PatchMgr.EditorBundlePath, "BundleTool.ini");
        ini.Open(iniPath);
        ini.WriteValue("BundleTool", platformName, manifestName);
        ini.Close();

        AssetDatabase.Refresh();
    }

    void BuildAllScenesName(ref Dictionary<string, string> bundle2Files)
    {
        foreach (EditorBuildSettingsScene scn in EditorBuildSettings.scenes)
        {
            if (!ResourceMgr.IsExternalScene(scn.path))
                continue;

            if (!scn.enabled)
                continue;

            //if (!FilterDependencies(scn.path))
            //    continue;

            //AssetImporter importer = AssetImporter.GetAtPath(scn.path);
            //importer.assetBundleName = scn.path + ResourceMgr.BundleSuffix;

            string[] deps = AssetDatabase.GetDependencies(scn.path, true);
            foreach (string dep in deps)
            {
                if (!FilterDependencies(dep, scn.path))
                    continue;

                AssetImporter importer = AssetImporter.GetAtPath(dep);
                importer.assetBundleName = dep + ResourceMgr.BundleSuffix;

                bundle2Files.Add(importer.assetBundleName, dep);
            }

            /*
            string[] levels = new string[1] { scn.path };
            string localPath = dstPath + "/" + scn.path.ToLower() + ResourceMgr.BundleSuffix;

            FileInfo file = new FileInfo(localPath);
            while (!Directory.Exists(file.DirectoryName))
                Directory.CreateDirectory(file.DirectoryName);

            BuildPipeline.BuildPlayer(levels, localPath, tar, BuildOptions.UncompressedAssetBundle | BuildOptions.BuildAdditionalStreamedScenes);
            */
        }
    }

    void BuildAllResourcesName(ref Dictionary<string, string> bundle2Files)
    {
        List<string> files = DirectoryFilter(new System.IO.DirectoryInfo("Assets/Resources"));
        foreach (var file in files)
        {
            //if (!FilterDependencies(file))
            //    continue;

            //AssetImporter importer = AssetImporter.GetAtPath(file);
            //importer.assetBundleName = file;

            string[] deps = AssetDatabase.GetDependencies(file, true);
            foreach (var dep in deps)
            {
                if (!FilterDependencies(dep, file))
                    continue;

                AssetImporter importer = AssetImporter.GetAtPath(dep);
                importer.assetBundleName = dep + ResourceMgr.BundleSuffix;

                bundle2Files.Add(importer.assetBundleName, dep);
            }
        }

        //BuildPipeline.BuildAssetBundles(dstPath, opt, tar);
    }

    void IteratePath(DirectoryInfo path, string[] excludeDirs, OnFileIterated iter)
    {
        try
        {
            foreach (FileInfo info in path.GetFiles("*.*"))
                iter(info);
        }
        catch
        {
        }

        DirectoryInfo[] dics = path.GetDirectories();
        foreach (DirectoryInfo info in dics)
        {
            bool ignore = false;
            foreach (string dir in excludeDirs)
            {
                if (-1 != info.Name.IndexOf(dir))
                {
                    ignore = true;
                    break;
                }
            }

            if(!ignore)
                IteratePath(info, excludeDirs, iter);
        }
    }

    string[] m_ExcludeDirs = { ".svn" };
    string[] m_ExcludeExts = { ".cs", ".js", ".meta", ".dll", "LightingData.asset" };

    List<string> DirectoryFilter(DirectoryInfo path)
    {
        List<string> ret = new List<string>();
        int pathIdx = Application.dataPath.IndexOf("Assets");

        IteratePath(path, m_ExcludeDirs, (FileInfo info)=>
        {
            foreach (string ext in m_ExcludeExts)
            {
                if (info.Name.IndexOf(ext) != -1)
                    return;
            }

            string lPath = info.FullName.Replace("\\", "/");
            lPath = lPath.Substring(pathIdx);

            ret.Add(lPath);
        });

        return ret;
    }

    bool FilterDependencies(string file, string root)
    {
        FileInfo info = new FileInfo(file);

        if (info.Extension == ".shader")
            return true;

        foreach (string ext in m_ExcludeExts)
        {
            if (info.Name.IndexOf(ext) != -1)
                return false;
        }

        if (file == root)
        {
            return true;
        }
        else
        {
            // Limit size is 512 KB.
            if (info.Length < 512 * 1024)
                return false;

            return true;
        }
    }
}
