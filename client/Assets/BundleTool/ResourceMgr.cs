using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR	
using UnityEditor;
#endif

// Loaded assetBundle contains the references count which can be used to unload dependent assetBundles automatically.
public class LoadedAssetBundle
{
    public AssetBundle assetBundle { get; private set; }
    int referencedCount;

    public LoadedAssetBundle(AssetBundle assetBundle)
    {
        this.assetBundle = assetBundle;
        referencedCount = 1;
    }

    public int IncRef()
    {
        return ++referencedCount;
    }

    public int DecRef()
    {
        return --referencedCount;
    }
}

public class ResourceMgr : Mgr<ResourceMgr>
{
    Dictionary<string, LoadedAssetBundle> m_LoadedAssetBundles = new Dictionary<string, LoadedAssetBundle>();

#if UNITY_EDITOR
    static int m_SimulateLoadBundleInEditor = -1;
    const string kSimulateLoadBundle = "SimulateLoadBundle";

    public static bool SimulateLoadBundle
    {
        get
        {
            if (m_SimulateLoadBundleInEditor == -1)
                m_SimulateLoadBundleInEditor = EditorPrefs.GetBool(kSimulateLoadBundle, true) ? 1 : 0;

            return m_SimulateLoadBundleInEditor != 0;
        }
        set
        {
            int newValue = value ? 1 : 0;
            if (newValue != m_SimulateLoadBundleInEditor)
            {
                m_SimulateLoadBundleInEditor = newValue;
                EditorPrefs.SetBool(kSimulateLoadBundle, value);
            }
        }
    }
#else
#if LOAD_RESOURCES
    public const bool SimulateLoadBundle = false;
#else
    public const bool SimulateLoadBundle = true;
#endif
#endif

    public const string BundlePath = "/AssetBundles";
    public const string BundleSuffix = ".bundle";
    public const string ResourcePrefix = "assets/resources/";
    public const string ScenePrefix = "assets/scenes/";
    public const string ManifestName = "AssetBundleManifest";

    int loadedSceneFrameCount = -1;
    List<string> loadedSceneBundleNames = new List<string>();

    public static string LocalCachePath { get; private set; }
    public AssetBundleManifest LocalManifest { get; private set; }

    public static bool IsExternalScene(string levelName)
    {
        if (levelName.ToLower().IndexOf("starter") != -1 ||
            levelName.ToLower().IndexOf("patch") != -1)
        {
            return false;
        }

        return true;
    }


    protected override void Awake()
    {
#if UNITY_EDITOR || LOAD_RESOURCES
        if (!ResourceMgr.SimulateLoadBundle)
        {
        }
        else
#endif
        {
            string platformName = PlatformUtil.GetPlatformName();
            LocalCachePath = PlatformUtil.GetRuntimeStreamingAssetsPath(Application.platform) + ResourceMgr.BundlePath + "/" + platformName;

            // Load default.
            string manifest = LocalCachePath + "/" + platformName;
            AssetBundle assetbundle = AssetBundle.LoadFromFile(manifest);
            LocalManifest = assetbundle.LoadAsset<AssetBundleManifest>(ResourceMgr.ManifestName);
            assetbundle.Unload(false);
        }
        base.Awake();
    }

    void Update()
    {
        if (loadedSceneBundleNames.Count > 0 && Time.frameCount != loadedSceneFrameCount)
        {
            for (int i = 0; i < loadedSceneBundleNames.Count; i++)
            {
                UnloadAsset(loadedSceneBundleNames[i]);
            }
            loadedSceneBundleNames.Clear();
        }
    }

    public void LoadScene(string sceneName, bool isAdditive, bool unload = true)
    {
        if (!IsExternalScene(sceneName))
        {
            SceneManager.LoadScene(sceneName, isAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single);
        }
        else
        {
#if UNITY_EDITOR || LOAD_RESOURCES
            if (!SimulateLoadBundle)
            {
                SceneManager.LoadScene(sceneName, isAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single);
            }
            else
#endif
            {
                string bundleName = (ScenePrefix + sceneName + ".unity" + BundleSuffix).ToLower();
                LoadAssetBundleWithDependencies(bundleName);
                LoadedAssetBundle bundle = GetLoadedAssetBundle(bundleName);
                if (bundle == null)
                {
                    return;
                }

                SceneManager.LoadScene(sceneName, isAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single);

                if (unload)
                {
                    loadedSceneFrameCount = Time.frameCount;
                    loadedSceneBundleNames.Add(bundleName);
                }
            }
        }
    }

    public string Resource2BundleName(string path, string suffix)
    {
        string bundleName = ResourcePrefix + path + suffix + BundleSuffix;
        return bundleName.ToLower();
    }

    public Object LoadResource(string path, string suffix)
    {
        return LoadResource<Object>(path, suffix);
    }

    public T LoadResource<T>(string path, string suffix) where T : Object
    {
#if UNITY_EDITOR || LOAD_RESOURCES
        if (!SimulateLoadBundle)
        {
            return Resources.Load<T>(path);
        }
        else
#endif
        {
            string bundleName = Resource2BundleName(path, suffix);
            LoadAssetBundleWithDependencies(bundleName);
            LoadedAssetBundle bundle = GetLoadedAssetBundle(bundleName);
            if (bundle == null)
            {
                return null;
            }

            string assetName = path.Substring(path.LastIndexOf("/") + 1);
            return bundle.assetBundle.LoadAsset<T>(assetName);
        }
    }

    public GameObject InstantiatePrefab(string path, bool unload = true)
    {
        GameObject obj = null;

        GameObject prefab = ResourceMgr.Instance.LoadResource<GameObject>(path, ".prefab");
        if (prefab != null)
        {
            obj = GameObject.Instantiate(prefab);
            if (unload)
            {
                UnloadAsset(path, ".prefab");
            }
        }

        return obj;
    }

    // Get loaded AssetBundle, only return vaild object when all the dependencies are downloaded successfully.
    public LoadedAssetBundle GetLoadedAssetBundle(string assetBundleName)
    {
        LoadedAssetBundle bundle;
        if (!m_LoadedAssetBundles.TryGetValue(assetBundleName, out bundle))
        {
            return null;
        }

        // No dependencies are recorded, only the bundle itself is required.
        string[] dependencies = PatchMgr.Instance.ManifestObject.GetAllDependencies(assetBundleName);

        // Make sure all dependencies are loaded
        foreach (var dependency in dependencies)
        {
            // Wait all the dependent assetBundles being loaded.
            LoadedAssetBundle dependentBundle;
            if (!m_LoadedAssetBundles.TryGetValue(dependency, out dependentBundle))
            {
                return null;
            }
        }

        return bundle;
    }

    public void UnloadAsset(string path, string suffix)
    {
        string bundleName = Resource2BundleName(path, suffix);
        UnloadAsset(bundleName);
    }

    public void UnloadAsset(string assetBundleName)
    {
#if UNITY_EDITOR || LOAD_RESOURCES
        // If we're in Editor simulation mode, ignore it.
        if (!ResourceMgr.SimulateLoadBundle)
            return;
        else
#endif
        {
            UnloadAssetBundleWithDependencies(assetBundleName);
        }
    }

    public void UnloadAllAssets()
    {
#if UNITY_EDITOR || LOAD_RESOURCES
        // If we're in Editor simulation mode, ignore it.
        if (!ResourceMgr.SimulateLoadBundle)
            return;
        else
#endif
        {
            foreach (var keyValue in m_LoadedAssetBundles)
            {
                keyValue.Value.assetBundle.Unload(false);
            }
            m_LoadedAssetBundles.Clear();
        }
    }

    // Load AssetBundle and its dependencies.
    void LoadAssetBundleWithDependencies(string assetBundleName)
    {
#if UNITY_EDITOR || LOAD_RESOURCES
        // If we're in Editor simulation mode, we don't have to really load the assetBundle and its dependencies.
        if (!ResourceMgr.SimulateLoadBundle)
            return;
#endif

        assetBundleName = PatchMgr.Instance.RemapVariantName(assetBundleName);
        LoadAssetBundleInternal(assetBundleName);

        // Load dependencies.
        LoadDependencies(assetBundleName);
    }

    // Unload assetbundle and its dependencies.
    void UnloadAssetBundleWithDependencies(string assetBundleName)
    {
#if UNITY_EDITOR || LOAD_RESOURCES
        // If we're in Editor simulation mode, ignore it.
        if (!ResourceMgr.SimulateLoadBundle)
            return;
        else
#endif
        {
            //Debug.Log(m_LoadedAssetBundles.Count + " assetbundle(s) in memory before unloading " + assetBundleName);

            UnloadAssetBundleInternal(assetBundleName);
            UnloadDependencies(assetBundleName);

            //Debug.Log(m_LoadedAssetBundles.Count + " assetbundle(s) in memory after unloading " + assetBundleName);
        }
    }

    // Where we get all the dependencies and load them all.
    void LoadDependencies(string assetBundleName)
    {
        // Get dependecies from the AssetBundleManifest object..
        string[] dependencies = PatchMgr.Instance.ManifestObject.GetAllDependencies(assetBundleName);
        if (dependencies.Length == 0)
            return;

        for (int i = 0; i < dependencies.Length; i++)
        {
            //if (!string.IsNullOrEmpty(dependencies[i]))
            {
                dependencies[i] = PatchMgr.Instance.RemapVariantName(dependencies[i]);
            }
        }

        // Record and load all dependencies.
        for (int i = 0; i < dependencies.Length; i++)
        {
            //if (!string.IsNullOrEmpty(dependencies[i]))
            {
                LoadAssetBundleInternal(dependencies[i]);
            }
        }
    }

    void UnloadDependencies(string assetBundleName)
    {
        string[] dependencies = PatchMgr.Instance.ManifestObject.GetAllDependencies(assetBundleName);

        // Loop dependencies.
        foreach (var dependency in dependencies)
        {
            UnloadAssetBundleInternal(dependency);
        }
    }

    // Where we actuall call WWW to download the assetBundle.
    LoadedAssetBundle LoadAssetBundleInternal(string assetBundleName)
    {
        LoadedAssetBundle bundle;
        if(m_LoadedAssetBundles.TryGetValue(assetBundleName, out bundle))
        {
            // Already loaded.
            bundle.IncRef();
            return bundle;
        }

        string path;
        if (PatchMgr.Instance.IsExternalAssetBundle(assetBundleName))
        {
            path = PatchMgr.BundleCachePath + "/" + assetBundleName;
        }
        else
        {
            path = LocalCachePath + "/" + assetBundleName;
        }

        bundle = new LoadedAssetBundle(AssetBundle.LoadFromFile(path));
        m_LoadedAssetBundles.Add(assetBundleName, bundle);
        return bundle;
    }

    void UnloadAssetBundleInternal(string assetBundleName)
    {
        LoadedAssetBundle bundle;
        if (!m_LoadedAssetBundles.TryGetValue(assetBundleName, out bundle))
        {
            return;
        }

        if (bundle.DecRef() == 0)
        {
            bundle.assetBundle.Unload(false);
            m_LoadedAssetBundles.Remove(assetBundleName);
            //Debug.Log("AssetBundle " + assetBundleName + " has been unloaded successfully");
        }
    }
}
