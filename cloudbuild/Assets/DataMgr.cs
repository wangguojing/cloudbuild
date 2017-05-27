using System;
using UnityEngine;

public class DataMgr : Mgr<DataMgr>
{
    protected override void OnDestroy()
    {
        ConfigINI.Close();
        ConfigINI = null;

        base.OnDestroy();
    }

    public override void OnPostPatch()
    {
        TextAsset config = ResourceMgr.Instance.LoadResource<TextAsset>("Config", ".txt");
        ResourceMgr.Instance.UnloadAsset("Config", ".txt");

        ConfigINI.OpenFromString(config.text);
    }

    public INIParser ConfigINI = new INIParser();

    public string SessionKey { get; set; }
    //public GetMyselfInfo_Resp MySelfInfo = null;

    // User Id & Password.
    public string UserId { get; set; }
    public string Password { get; set; }

    // Keep last upload image information for testing.
    public string LastUploadImageGUID { get; set; }
    public Texture2D LastUploadImage { get; set; }

}