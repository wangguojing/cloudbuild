using UnityEngine;
using System.Collections;
using System;

#if UNITY_EDITOR	
using UnityEditor;
#endif

public class PlatformUtil : ScriptableObject
{
    public enum Platform
    {
        Android,
        IPhone,
        WebPlayer,
        Windows,
        Windows_x64,
        MacOSX,
        MacOSX_x64,
        MacOSX_Universal,

        MAX,
    };

    [SerializeField]
    private static string m_ApplicationVersion = "0.0.0.0";
    public static string ApplicationVersion
    {
        get { return m_ApplicationVersion; }
        set
        {
#if UNITY_EDITOR
            PlayerSettings.bundleVersion = value;
#endif
            m_ApplicationVersion = value;
        }
    }

    [SerializeField]
    public static string StreamingBundle = "0";

    static public string Platform2Extension(Platform platform)
    {
        switch (platform)
        {
            case Platform.Android:
                return "apk";
            case Platform.IPhone:
                return "";
            case Platform.WebPlayer:
                return "";
            case Platform.Windows:
            case Platform.Windows_x64:
                return "exe";
            case Platform.MacOSX:
            case Platform.MacOSX_x64:
            case Platform.MacOSX_Universal:
                return "";

            default:
                return "";
        }
    }

#if UNITY_EDITOR
    static public BuildTarget Platform2BuildTarget(Platform platform)
    {
        switch (platform)
        {
            case Platform.Android:
                return BuildTarget.Android;
            case Platform.IPhone:
                return BuildTarget.iOS;
            case Platform.WebPlayer:
                return BuildTarget.WebPlayer;
            case Platform.Windows:
                return BuildTarget.StandaloneWindows;
            case Platform.Windows_x64:
                return BuildTarget.StandaloneWindows64;
            case Platform.MacOSX:
                return BuildTarget.StandaloneOSXIntel;
            case Platform.MacOSX_x64:
                return BuildTarget.StandaloneOSXIntel64;
            case Platform.MacOSX_Universal:
                return BuildTarget.StandaloneOSXUniversal;

            default:
                return BuildTarget.iOS;
        }
    }

    static public Platform BuildTarget2Platform(BuildTarget target)
    {
        switch (target)
        {
            case BuildTarget.Android:
                return Platform.Android;
            case BuildTarget.iOS:
                return Platform.IPhone;
            case BuildTarget.WebPlayer:
                return Platform.WebPlayer;
            case BuildTarget.StandaloneWindows:
                return Platform.Windows;
            case BuildTarget.StandaloneWindows64:
                return Platform.Windows_x64;
            case BuildTarget.StandaloneOSXIntel:
                return Platform.MacOSX;
            case BuildTarget.StandaloneOSXIntel64:
                return Platform.MacOSX_x64;
            case BuildTarget.StandaloneOSXUniversal:
                return Platform.MacOSX_Universal;

            default:
                return Platform.IPhone;
        }
    }
#endif

    static public string GetPlatformName()
    {
#if UNITY_EDITOR
        return BuildTarget2Name(EditorUserBuildSettings.activeBuildTarget);
#else
        return RuntimePlatform2Name(Application.platform);
#endif
    }

    const string AndroidName = "Android";
    const string iOSName = "iOS";
    const string WebPlayerName = "WebPlayer";
    const string WindowsName = "Windows";
    const string OSXName = "OSX";

    static public string Platform2Name(Platform platform)
    {
        switch (platform)
        {
            case Platform.Android:
                return AndroidName;
            case Platform.IPhone:
                return iOSName;
            case Platform.WebPlayer:
                return WebPlayerName;
            case Platform.Windows:
            case Platform.Windows_x64:
                return WindowsName;
            case Platform.MacOSX:
            case Platform.MacOSX_x64:
            case Platform.MacOSX_Universal:
                return OSXName;

            default:
                return null;
        }
    }

#if UNITY_EDITOR
    static public string BuildTarget2Name(BuildTarget target)
    {
        switch (target)
        {
            case BuildTarget.Android:
                return AndroidName;
            case BuildTarget.iOS:
                return iOSName;
            case BuildTarget.WebPlayer:
                return WebPlayerName;
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
                return WindowsName;
            case BuildTarget.StandaloneOSXIntel:
            case BuildTarget.StandaloneOSXIntel64:
            case BuildTarget.StandaloneOSXUniversal:
                return OSXName;

            default:
                return null;
        }
    }
#endif

    static public string RuntimePlatform2Name(RuntimePlatform platform)
    {
        switch (platform)
        {
            case RuntimePlatform.Android:
                return AndroidName;
            case RuntimePlatform.IPhonePlayer:
                return iOSName;
            case RuntimePlatform.WindowsWebPlayer:
            case RuntimePlatform.OSXWebPlayer:
            case RuntimePlatform.WebGLPlayer:
                return WebPlayerName;
            case RuntimePlatform.WindowsEditor:
            case RuntimePlatform.WindowsPlayer:
                return WindowsName;
            case RuntimePlatform.OSXEditor:
            case RuntimePlatform.OSXPlayer:
                return OSXName;

            default:
                return null;
        }
    }

    static public string GetRuntimeStreamingAssetsPath(RuntimePlatform platform)
    {
        switch (platform)
        {
            case RuntimePlatform.OSXEditor:
            case RuntimePlatform.OSXPlayer:
            case RuntimePlatform.WindowsEditor:
            case RuntimePlatform.WindowsPlayer:
                {
                    return Application.dataPath + "/StreamingAssets";
                }

            case RuntimePlatform.Android:
                {
                    return Application.dataPath + "!assets";
                }

            case RuntimePlatform.IPhonePlayer:
                {
                    return Application.dataPath + "/Raw";
                }

            default:
                throw new System.Exception("Unknown platform!");
        }
    }
}