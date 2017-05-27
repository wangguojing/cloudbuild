using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using MiniJSON;

public class ArtTool : MonoBehaviour
{
    [MenuItem("ArtTool/Clear Unused Properties")]
    public static void ClearUnusedProperties(params string[] searchPaths)
    {
        string[] matGuids = AssetDatabase.FindAssets("t:Material", searchPaths);
        SerializedObject matInfo = null;
        SerializedProperty propArr = null;
        SerializedProperty prop = null;
        Material mat = null;
        foreach (string guid in matGuids)
        {
            mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            matInfo = new SerializedObject(mat);
            propArr = matInfo.FindProperty("m_SavedProperties");
            propArr.Next(true);
            do
            {
                if (!propArr.isArray) continue;
                for (int i = propArr.arraySize - 1; i >= 0; --i)
                {
                    prop = propArr.GetArrayElementAtIndex(i).FindPropertyRelative("first").FindPropertyRelative("name");

                    if (!mat.HasProperty(prop.stringValue))
                    {
                        propArr.DeleteArrayElementAtIndex(i);
                    }
                }
            } while (propArr.Next(false));
            matInfo.ApplyModifiedProperties();
            Resources.UnloadAsset(mat);
        }
        AssetDatabase.SaveAssets();
    }
}

