using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using System.Net;
using UnityEngine.Experimental.Networking;
using System.Runtime.Serialization.Formatters.Binary;

#if UNITY_EDITOR
using UnityEditor;
#endif


public class PatchMgr : Mgr<PatchMgr>
{
    class PatchFile
    {
        const string FileName = ".patch";

        uint version;
        uint crc;
        Dictionary<string, string> fileNameHashs = new Dictionary<string, string>();

        void Serialize()
        {
            string path = PatchMgr.BundleCachePath + "/" + FileName;

            try
            {
                using (FileStream fs = File.Open(path, FileMode.OpenOrCreate, FileAccess.Write))
                {
                    var binaryFormatter = new BinaryFormatter();
                    binaryFormatter.Serialize(fs, version);
                    binaryFormatter.Serialize(fs, crc);
                    binaryFormatter.Serialize(fs, fileNameHashs);
                }
            }
            catch (System.Exception ex)
            {
            }
        }

        public void Deserialize()
        {
            string path = PatchMgr.BundleCachePath + "/" + FileName;

            try
            {
                using (FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read))
                {
                    var binaryFormatter = new BinaryFormatter();
                    version = (uint)binaryFormatter.Deserialize(fs);
                    crc = (uint)binaryFormatter.Deserialize(fs);
                    fileNameHashs = (Dictionary<string, string>)binaryFormatter.Deserialize(fs);
                }
            }
            catch (System.Exception ex)
            {
            }
        }

        public void SaveFileNameHash(string fileName, Hash128 hash)
        {
            fileNameHashs[fileName] = hash.ToString();
            Serialize();

            Debug.Log("PATCH save: " + fileName + ", hash: " + hash);
        }

        public bool IsVersionCached(string fileName, Hash128 fileHash)
        {
            string hash;
            if (fileNameHashs.TryGetValue(fileName, out hash))
            {
                if (hash == fileHash.ToString())
                    return true;
            }

            return false;
        }
    }

    public class FileInfo
    {
        public string fileName;
        public ulong bytesDownloaded;
        public ulong totalByteSize;
        public string error;
        public bool isDone;
    }

    public class PatchVersionRequest : CustomYieldInstruction
    {
        public string error { get; private set; }

        UnityWebRequest web;
        string url;
        string version;

        public PatchVersionRequest()
        {
            string platformName = PlatformUtil.GetPlatformName();

#if UNITY_EDITOR
            INIParser ini = new INIParser();
            ini.Open(Path.Combine(PatchMgr.EditorBundlePath, "BundleTool.ini"));
            version = ini.ReadValue("BundleTool", platformName, "");
            ini.Close();

            // Set base downloading url.
            string relativePath = "file:///" + PatchMgr.EditorBundlePath.Replace("\\", "/");
            url = relativePath + "/" + version + "/";
#else
            DataMgr.Instance.OnPostPatch();
            url = DataMgr.Instance.ConfigINI.ReadValue("PATCH", "URL", "http://192.168.1.104:7120");
            string request = url + "/patch.php?p=" + platformName;

            web = UnityWebRequest.Get(request);
            web.Send();

            Debug.Log("Try request url: " + request);
#endif
        }

        public override bool keepWaiting
        {
            get
            {
                if (web != null)
                {
                    if (web.isDone)
                    {
                        error = web.error;
                        version = web.downloadHandler.text;
                        url = url + ResourceMgr.BundlePath + "/" + version + "/";

                        web.Dispose();
                        web = null;

                        PatchMgr.Instance.PatchURL = url;
                        PatchMgr.Instance.ManifestVersion = version;
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    PatchMgr.Instance.PatchURL = url;
                    PatchMgr.Instance.ManifestVersion = version;
                    return false;
                }
            }
        }
    }

    public class FileInfosRequest : CustomYieldInstruction
    {
        List<FileInfo> m_Files = new List<FileInfo>();
        public List<FileInfo> files { get { return m_Files; } }

        public FileInfosRequest()
        {
            List<string> externalBundles = new List<string>();

            HashSet<string> localBundles = new HashSet<string>();
            string[] bundles = ResourceMgr.Instance.LocalManifest.GetAllAssetBundles();
            for (int i = 0; i < bundles.Length; i++)
            {
                localBundles.Add(bundles[i]);
            }

            string[] rmtBundles = PatchMgr.Instance.m_RemoteManifest.GetAllAssetBundles();
            foreach (string rmtBundle in rmtBundles)
            {
                Hash128 rmtHash = PatchMgr.Instance.m_RemoteManifest.GetAssetBundleHash(rmtBundle);
                string url = PatchMgr.Instance.PatchURL + rmtBundle;

                if (!localBundles.Contains(rmtBundle))
                {
                    // New asset.
                    externalBundles.Add(rmtBundle);
                    PatchMgr.Instance.m_ExternalAssetBundles.Add(rmtBundle);

                    //Debug.Log("Patch new asset:" + rmtBundle + ", gId:" + rmtHash);
                }
                else
                {
                    Hash128 localHash = ResourceMgr.Instance.LocalManifest.GetAssetBundleHash(rmtBundle);
                    if (rmtHash != localHash)
                    {
                        // Update asset.
                        externalBundles.Add(rmtBundle);
                        PatchMgr.Instance.m_ExternalAssetBundles.Add(rmtBundle);

                        //Debug.Log("Patch update asset:" + rmtBundle + ", g:" + rmtHash + ", l:" + localHash);
                    }
                }
            }

            ulong totalByteSize = 0;
            foreach (string rmtBundle in externalBundles)
            {
                Hash128 rmtHash = PatchMgr.Instance.m_RemoteManifest.GetAssetBundleHash(rmtBundle);
                string url = PatchMgr.Instance.PatchURL + rmtBundle;

                if (!PatchMgr.Instance.m_PatchFile.IsVersionCached(rmtBundle, rmtHash))
                {
                    ulong byteSize;
                    //Get size of the asset
                    WebRequest req = HttpWebRequest.Create(PatchMgr.Instance.PatchURL + rmtBundle);
                    req.Method = "HEAD";
                    using (System.Net.WebResponse resp = req.GetResponse())
                    {
                        byteSize = (ulong)resp.ContentLength;
                        totalByteSize += byteSize;
                    }

                    m_Files.Add(new FileInfo() { fileName = rmtBundle, bytesDownloaded = 0, totalByteSize = byteSize });
                    Debug.Log("PATCH pending: " + rmtBundle + ", new: " + rmtHash);
                }
            }
        }

        public override bool keepWaiting
        {
            get { return false; }
        }
    }

    public class PatchingRequest : CustomYieldInstruction
    {
        List<FileInfo> m_Files = new List<FileInfo>();
        public List<FileInfo> files { get { return m_Files; } }

        Dictionary<FileInfo, TransferRequest> downloads = new Dictionary<FileInfo, TransferRequest>();
        Dictionary<string, FileInfo> urls = new Dictionary<string, FileInfo>();

        public bool isError
        {
            get
            {
                for (int i = 0; i < files.Count; i++)
                {
                    if (!string.IsNullOrEmpty(files[i].error))
                        return true;
                }

                return false;
            }
        }

        public ulong count
        {
            get
            {
                ulong cnt = 0;
                for (int i = 0; i < files.Count; i++)
                {
                    if (!files[i].isDone || !string.IsNullOrEmpty(files[i].error))
                        cnt++;
                }

                return cnt;
            }
        }

        //ulong m_BytesDownloaded;
        public ulong bytesDownloaded
        {
            get
            {
                ulong bytesDownloading = 0;
                foreach (var keyValue in downloads)
                {
                    bytesDownloading += keyValue.Key.bytesDownloaded;
                }

                return bytesDownloading;
            }
        }

        public ulong totalByteSize { get; private set; }

        public PatchingRequest(List<FileInfo> files)
        {
            m_Files = files;

            for (int i = 0; i < files.Count; i++)
            {
                string url = PatchMgr.Instance.PatchURL + files[i].fileName;

                TransferRequest download = FileTransferMgr.Instance.DownloadAsync(url, CacheDownloadedFile);
                downloads.Add(files[i], download);
                urls.Add(url, files[i]);

                totalByteSize += files[i].totalByteSize;
            }
        }

        void CacheDownloadedFile(UnityWebRequest www)
        {
            FileInfo file;
            if (urls.TryGetValue(www.url, out file))
            {
                file.isDone = true;
                file.error = www.error;

                if (string.IsNullOrEmpty(www.error))
                {
                    string fileName = www.url.Substring(PatchMgr.Instance.PatchURL.Length);
                    System.IO.FileInfo info = new System.IO.FileInfo(PatchMgr.BundleCachePath + "/" + fileName);
                    if (!Directory.Exists(info.DirectoryName))
                    {
                        Directory.CreateDirectory(info.DirectoryName);
                    }
                    File.WriteAllBytes(info.FullName, www.downloadHandler.data);

                    Hash128 hash = PatchMgr.Instance.m_RemoteManifest.GetAssetBundleHash(fileName);
                    PatchMgr.Instance.m_PatchFile.SaveFileNameHash(fileName, hash);

                    Debug.Log("saved patch file:" + PatchMgr.BundleCachePath + "/" + fileName);
                }
            }
        }

        public override bool keepWaiting
        {
            get
            {
                bool finish = true;
                foreach (var keyValue in downloads)
                {
                    FileInfo file = keyValue.Key;
                    TransferRequest download = keyValue.Value;

                    file.bytesDownloaded = download.downloadedBytes;

                    if (download.keepWaiting)
                    {
                        finish = false;
                    }
                    else
                    {
                        file.isDone = true;
                    }
                }

                return !finish;
            }
        }
    }

    AssetBundleManifest m_RemoteManifest;

    public string PatchURL { get; private set; }
    public string ManifestVersion { get; private set; }

    public string[] Variants { get; set; }

    static bool m_FallbackMode = false;
    public static bool FallbackMode
    {
        get
        {
            return m_FallbackMode;
        }
        set
        {
            if (m_FallbackMode != value)
            {
                m_FallbackMode = value;
                //Debug.Log("Patch fall back mode : " + m_FallbackMode);
            }
        }
    }

    public bool SkipPatching = false;

    PatchFile m_PatchFile = new PatchFile();
    HashSet<string> m_ExternalAssetBundles = new HashSet<string>();

    public AssetBundleManifest ManifestObject
    {
        get
        {
            AssetBundleManifest manifest = m_RemoteManifest;
            if (m_RemoteManifest == null && FallbackMode)
                return ResourceMgr.Instance.LocalManifest;

            return m_RemoteManifest;
        }
    }

    public static string BundleCachePath
    {
        get
        {
            return Application.temporaryCachePath + ResourceMgr.BundlePath;
        }
    }

#if UNITY_EDITOR
    const string kEditorBundlePath = "EditorBundlePath";

    public static string EditorBundlePath
    {
        get
        {
            return EditorPrefs.GetString(kEditorBundlePath, "." + ResourceMgr.BundlePath);
        }
        set
        {
            EditorPrefs.SetString(kEditorBundlePath, value);
        }
    }
#endif

    void Start()
    {
#if !UNITY_EDITOR
        SkipPatching = false;
#endif
    }

    public static void CleanBundleCachePath()
    {
        while (Directory.Exists(BundleCachePath))
            Directory.Delete(BundleCachePath, true);
    }

    public PatchVersionRequest RequestVersionAsync()
    {
        PatchVersionRequest request = new PatchVersionRequest();
        return request;
    }

    public TransferRequest RequestRemoteManifestAsync()
    {
        // Initialize AssetBundleManifest which loads the AssetBundleManifest object.
        TransferRequest file = FileTransferMgr.Instance.DownloadAsync(PatchURL + "/" + ManifestVersion, (www) =>
        {
            if (!www.isError)
            {
                AssetBundle assetbundle = AssetBundle.LoadFromMemory(www.downloadHandler.data);
                m_RemoteManifest = assetbundle.LoadAsset<AssetBundleManifest>(ResourceMgr.ManifestName);
                assetbundle.Unload(false);
            }
        });
        return file;
    }

    public FileInfosRequest RequestFileInfosAsync()
    {
        m_PatchFile.Deserialize();

        FileInfosRequest request = new FileInfosRequest();
        return request;
    }

    public bool IsExternalAssetBundle(string bundleName)
    {
        if (FallbackMode)
            return false;

        return m_ExternalAssetBundles.Contains(bundleName);
    }

    public PatchingRequest PatchingAsync(List<FileInfo> list)
    {
        //FileInfoList ret = new FileInfoList();
        PatchingRequest request = new PatchingRequest(list);
        return request;
    }

    // Remaps the asset bundle name to the best fitting asset bundle variant.
    public string RemapVariantName(string assetBundleName)
    {
        string[] bundlesWithVariant = ManifestObject.GetAllAssetBundlesWithVariant();

        // If the asset bundle doesn't have variant, simply return.
        if (System.Array.IndexOf(bundlesWithVariant, assetBundleName) < 0)
            return assetBundleName;

        string[] split = assetBundleName.Split('.');

        int bestFit = int.MaxValue;
        int bestFitIndex = -1;
        // Loop all the assetBundles with variant to find the best fit variant assetBundle.
        for (int i = 0; i < bundlesWithVariant.Length; i++)
        {
            string[] curSplit = bundlesWithVariant[i].Split('.');
            if (curSplit[0] != split[0])
                continue;

            int found = System.Array.IndexOf(Variants, curSplit[1]);
            if (found != -1 && found < bestFit)
            {
                bestFit = found;
                bestFitIndex = i;
            }
        }

        if (bestFitIndex != -1)
            return bundlesWithVariant[bestFitIndex];
        else
            return assetBundleName;
    }
}
