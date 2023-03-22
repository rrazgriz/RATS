// RATS - Raz's Animator Tweaks'n Stuff
// Original AnimatorExtensions by Dj Lukis.LT, under MIT License

// Copyright (c) 2023 Razgriz
// SPDX-License-Identifier: MIT

#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
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
            if(size < 1024)
                return string.Format("{0} B", Math.Round(size, 0));
            else if(size < 1048576)
                return string.Format("{0} KB", Math.Round(size / 1024, 0));
            else if(size < 1048576 * 10)
                return string.Format("{0} MB", Math.Round(size / 1048576, 2));
            else if(size < 1048576 * 100)
                return string.Format("{0} MB", Math.Round(size / 1048576, 1));
            else
                return string.Format("{0} MB", Math.Round(size / 1048576, 0));
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
                // Don't try to label built-in items, and respect configuration
                if(filterItem == null || (!RATS.Prefs.ProjectWindowExtensions && !RATS.Prefs.ProjectWindowFilesize))
                    return;

                bool listMode = (bool)ObjectListAreaLocalGroupListModeField.GetValue(__instance);
                bool isMainRepresentation = (bool)FilterResultIsMainRepresentationField.GetValue(filterItem);
                bool isFolder = (bool)FilterResultIsFolderField.GetValue(filterItem);
                int instanceID = (int)FilterResultInstanceIDField.GetValue(filterItem);

                // Don't try to label if it's not list mode, is a subasset or a folder, or doesn't have a valid instance ID
                if(!listMode || !isMainRepresentation || isFolder || instanceID <= 0)
                    return;

                string name = (string)FilterResultNameField.GetValue(filterItem);
                string guid = (string)FilterResultGuidProperty.GetValue(filterItem);

                string path = Path.GetFullPath(AssetDatabase.GetAssetPath(instanceID));
                string extension = Path.GetExtension(path);
                // Handle double extensions, like .tar.gz 
                string fileNameWithoutFirstExtension = Path.GetFileNameWithoutExtension(path);
                if(fileNameWithoutFirstExtension.Contains("."))
                    extension = Path.GetExtension(fileNameWithoutFirstExtension) + extension;

                extensionLabelStyle.normal.textColor = RATS.Prefs.ProjectWindowLabelTextColor;

                string labelText = "";
                
                if(RATS.Prefs.ProjectWindowExtensions)
                    labelText += extension + "  ";
                
                if(RATS.Prefs.ProjectWindowFilesize)
                    labelText = labelText + FormatSizeBytes(new System.IO.FileInfo(path).Length);
                extensionLabelStyle.alignment = RATS.Prefs.ProjectWindowLabelAlignment;

                float offsetX = position.x + EditorStyles.foldout.margin.left + EditorStyles.foldout.padding.left;
                offsetX += EditorStyles.label.CalcSize(new GUIContent(name)).x + 16 + 6;
                Rect labelRect = position;
                labelRect.x += offsetX;
                labelRect.width -= offsetX;

                GUI.Label(labelRect, labelText, extensionLabelStyle);
            }
        }
    }
}
#endif