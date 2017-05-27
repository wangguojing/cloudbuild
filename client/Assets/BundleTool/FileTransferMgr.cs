using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Experimental.Networking;

#if UNITY_EDITOR	
using UnityEditor;
#endif

public class TransferRequest : CustomYieldInstruction
{
    public string url { get; private set; }
    public event FileTransferMgr.DownloadHandler onDownload;

    public string error { get; private set; }
    public ulong downloadedBytes { get; private set; }
    public float downloadProgress { get; private set; }
    //public bool isDone { get { return www == null; } }
    public override bool keepWaiting { get { return www != null; } }

    UnityWebRequest www;

    public TransferRequest(string url)
    {
        this.url = url;
        this.www = UnityWebRequest.Get(url);
    }

    public void Send()
    {
        www.Send();
    }

    public void Update()
    {
        if (!keepWaiting)
            return;

        error = www.error;
        downloadedBytes = www.downloadedBytes;
        downloadProgress = www.downloadProgress;

        if (www.isDone)
        {
            if (onDownload != null)
            {
                onDownload(www);
            }

            Dispose();
        }
    }

    void Dispose()
    {
        www.Dispose();
        www = null;
    }
}

// Class takes care of loading assetBundle and its dependencies automatically, loading variants automatically.
public class FileTransferMgr : Mgr<FileTransferMgr>
{
    public delegate void DownloadHandler(UnityWebRequest www);

    public int MaxProgressLimit = 1;

    Dictionary<string, TransferRequest> m_DownloadingWWWs = new Dictionary<string, TransferRequest>();

    Queue<TransferRequest> m_PendingDownloads = new Queue<TransferRequest>();
    List<TransferRequest> m_InProgressDownloads = new List<TransferRequest>();

    void Update()
    {
        // Remove the finished WWWs.
        for (int i = m_InProgressDownloads.Count - 1; i >= 0; i--)
        {
            TransferRequest download = m_InProgressDownloads[i];
            download.Update();

            if (!download.keepWaiting)
            {
                m_InProgressDownloads.RemoveAt(i);
                m_DownloadingWWWs.Remove(download.url);
            }
        }

        while (m_PendingDownloads.Count > 0 && m_InProgressDownloads.Count < MaxProgressLimit)
        {
            TransferRequest download = m_PendingDownloads.Dequeue();
            download.Send();

            m_InProgressDownloads.Add(download);
        }
    }

    public TransferRequest DownloadAsync(string url, DownloadHandler callback)
    {
        TransferRequest download = null;
        if (m_DownloadingWWWs.TryGetValue(url, out download))
        {
            download.onDownload += callback;
            return download;
        }

        download = new TransferRequest(url);
        download.onDownload += callback;

        m_DownloadingWWWs.Add(url, download);
        m_PendingDownloads.Enqueue(download);
        return download;
    }

}