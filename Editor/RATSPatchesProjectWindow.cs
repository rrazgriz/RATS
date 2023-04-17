// RATS - Raz's Animator Tweaks'n Stuff
// Original AnimatorExtensions by Dj Lukis.LT, under MIT License

// Copyright (c) 2023 Razgriz
// SPDX-License-Identifier: MIT

#if UNITY_EDITOR && RATS_HARMONY
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using HarmonyLib;

namespace Razgriz.RATS
{
    public partial class RATS
    {
        private static readonly Type ObjectListAreaLocalGroupType = AccessTools.TypeByName("UnityEditor.ObjectListArea+LocalGroup");
        private static readonly FieldInfo ObjectListAreaLocalGroupListModeField = AccessTools.Field(ObjectListAreaLocalGroupType, "m_ListMode"); // bool

        private static readonly Type FilterResultType = AccessTools.TypeByName("UnityEditor.FilteredHierarchy+FilterResult");
        private static readonly FieldInfo FilterResultNameField = AccessTools.Field(FilterResultType, "name"); // string
        private static readonly FieldInfo FilterResultInstanceIDField = AccessTools.Field(FilterResultType, "instanceID"); // int
        private static readonly PropertyInfo FilterResultGuidProperty = AccessTools.Property(FilterResultType, "guid"); // string
        private static readonly FieldInfo FilterResultIsMainRepresentationField = AccessTools.Field(FilterResultType, "isMainRepresentation"); // bool
        private static readonly FieldInfo FilterResultIsFolderField = AccessTools.Field(FilterResultType, "isFolder"); // bool

        public static string FormatSizeBytes(float size)
        {
            int sizeLog1024 = Mathf.FloorToInt(Mathf.Log(size, 1024)); // Intervals of 1024 bytes

            switch (sizeLog1024)
            {
                case 0:  return $"{Math.Round(size, 0)} B";
                case 1:  return $"{Math.Round(size / 1024, 0)} KB";
                case 2:  return $"{Math.Round(size / 1048576, 2)} MB";
                case 3:  return $"{Math.Round(size / 1048576, 1)} MB";
                default: return $"{Math.Round(size / 1048576, 0)} MB";
            }
        }

        // TODO: figure out why this doesn't show for newly created assets until project reload
        // Add extension/filesize to project window
        [HarmonyPatch]
        class PatchProjectWindowDrawItem
        {
            static GUIStyle extensionLabelStyle = new GUIStyle(EditorStyles.miniLabel);

            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => AccessTools.Method(ObjectListAreaLocalGroupType, "DrawItem");

            [HarmonyPostfix]
            static void Postfix(object __instance, Rect position, object filterItem)
            {
                // Don't try to label built-in items
                if(filterItem == null)
                    return;

                bool listMode = (bool)ObjectListAreaLocalGroupListModeField.GetValue(__instance);
                bool isMainRepresentation = (bool)FilterResultIsMainRepresentationField.GetValue(filterItem);
                bool isFolder = (bool)FilterResultIsFolderField.GetValue(filterItem);
                int instanceID = (int)FilterResultInstanceIDField.GetValue(filterItem);

                // Don't try to label if it's not list mode, is a subasset, or we're configured not to label
                if (!listMode 
                    || !isMainRepresentation 
                    || (isFolder && !RATS.Prefs.ProjectWindowFolderChildren) 
                    || (!isFolder && !RATS.Prefs.ProjectWindowExtensions && !RATS.Prefs.ProjectWindowFilesize)
                    )
                    return;

                string name = (string)FilterResultNameField.GetValue(filterItem);
                string guid = (string)FilterResultGuidProperty.GetValue(filterItem);

                // This prevents nullrefs when trying to list scene objects
                if(guid == null)
                    return;

                string labelText = ProjectItemCache.GetLabel(instanceID, guid, isFolder);

                extensionLabelStyle.normal.textColor = RATS.Prefs.ProjectWindowLabelTextColor;
                extensionLabelStyle.alignment = RATS.Prefs.ProjectWindowLabelAlignment;

                float offsetX = position.x + EditorStyles.foldout.margin.left + EditorStyles.foldout.padding.left;
                offsetX += EditorStyles.label.CalcSize(new GUIContent(name)).x + 16 + 6;
                Rect labelRect = position;
                labelRect.x += offsetX;
                labelRect.width -= offsetX;

                GUI.Label(labelRect, labelText, extensionLabelStyle);
            }
        }

        // Cache item data, so we don't have to recalculate on each draw
        class ProjectItemCache : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
            {
                importedAssets.Do(assetPath => fileCache.Remove(AssetDatabase.AssetPathToGUID(assetPath)));
                // Not sure how to elegantly determine folders that need updated, and updating folders seems fast, so just clear the entire cache
                folderCache.Clear();
            }

            static Dictionary<string, FileData> fileCache = new Dictionary<string, FileData>();
            static Dictionary<string, int> folderCache = new Dictionary<string, int>();

            internal struct FileData
            {
                public string FullPath;
                public string Extension;
                public long FileSize;
                public string FileSizeLabel;
            }

            public static string GetLabel(int instanceID, string guid, bool isFolder)
            {
                if(isFolder)
                {
                    if(!folderCache.ContainsKey(guid))
                        FetchAndCacheFolderInfo(instanceID, guid);

                    return folderCache[guid].ToString();
                }

                if(!fileCache.ContainsKey(guid))
                    FetchAndCacheFileInfo(instanceID, guid);

                string labelText = "";

                if(RATS.Prefs.ProjectWindowExtensions)
                    labelText += fileCache[guid].Extension;
                
                if(RATS.Prefs.ProjectWindowFilesize)
                {
                    if(labelText.Length > 0) labelText += "  ";
                    labelText += fileCache[guid].FileSizeLabel;
                }

                return labelText;
            }

            public static void FetchAndCacheFolderInfo(int instanceID, string guid)
            {
                folderCache[guid] = Directory.EnumerateFiles(AssetDatabase.GetAssetPath(instanceID), "*.meta", SearchOption.AllDirectories).Count();
            }

            public static void FetchAndCacheFileInfo(int instanceID, string guid)
            {
                string assetPath = AssetDatabase.GetAssetPath(instanceID);
                if(String.IsNullOrEmpty(assetPath))
                {
                    fileCache[guid] = new FileData {FullPath = "", FileSize = 0, Extension = "", FileSizeLabel = ""};
                }
                else
                {
                    string itemPath = Path.GetFullPath(assetPath);
                    string itemExtension = Path.GetExtension(itemPath);
                    long fileSizeBytes = new System.IO.FileInfo(itemPath).Length;
                    string itemFileSizeFormatted = FormatSizeBytes(fileSizeBytes);

                    fileCache[guid] = new FileData {FullPath = itemPath, FileSize = fileSizeBytes, Extension = itemExtension, FileSizeLabel = itemFileSizeFormatted};
                }
            }
        }
    }
}
#endif