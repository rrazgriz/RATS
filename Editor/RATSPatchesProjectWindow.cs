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
using UnityEditor.Animations;

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
        
        [MenuItem("Assets/Cleanup Controller")]
        private static void CleanupController()
        {
            var controller = Selection.activeObject as AnimatorController;

            if (controller == null) return;

            if (!EditorUtility.DisplayDialog("Cleanup Controller",
                    "This operation will remove all sub-assets not referenced in this Controller. This might remove assets that are still used externally. Make sure you have a backup of the Controller.",
                    "Proceed", "Cancel")) return;

            ScanController(controller);
        }
            
        [MenuItem("Assets/Cleanup Controller", true)]
        private static bool ValidateCleanupController()
        {
            return Selection.objects.Any(x => x is AnimatorController) && Selection.objects.Length > 0;
        }
        
        // Code in the region below has been adapted from Dreadrith's Controller Cleaner, which has the following license
        /*
MIT License
Copyright (c) 2024 Dreadrith

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
         */
        #region Dreadrith Controller Cleaner
        static void ScanController(AnimatorController target)
        {
            var controller = target;
            Type[] targetTypes = new [] {
                typeof(AnimatorState), 
                typeof(AnimatorStateTransition),
                typeof(AnimatorStateMachine),
                typeof(AnimatorTransition),
                typeof(AnimatorTransitionBase),
                typeof(StateMachineBehaviour),
                typeof(BlendTree),
            };

            var allObjects = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller)).Where(x => targetTypes.Contains(x.GetType())).ToArray();
            var usedObjects = new List<UnityEngine.Object>();
            for (int i = 0; i < controller.layers.Length; i++)
                ScanMachine(controller.layers[i].stateMachine, true, usedObjects);
            
            var obsoleteObjects = allObjects.Except(usedObjects).ToArray();
            bool shouldDestroy = obsoleteObjects.Length > 0;
            if (shouldDestroy)
            {
                int destroyCount = 0;
                foreach (var o in obsoleteObjects)
                {
                    if (!o) continue;
                    Debug.Log($"Removed {o} from {controller}");
                    destroyCount++;
                    AssetDatabase.RemoveObjectFromAsset(o);
                    UnityEngine.Object.DestroyImmediate(o);
                }
                Debug.Log($"Removed {destroyCount} assets from {controller}");
            }

            foreach (var l in controller.layers)
                RemoveMissingTransitions(l.stateMachine);

            if (shouldDestroy) ScanController(target);
        }

        static void RemoveMissingTransitions(AnimatorStateMachine m)
        {
            m.entryTransitions = m.entryTransitions.Where(o => o).ToArray();
            m.anyStateTransitions = m.anyStateTransitions.Where(o => o).ToArray();
            foreach (var cs in m.states)
            {
                cs.state.transitions = cs.state.transitions.Where(o => o).ToArray();
                EditorUtility.SetDirty(cs.state);
            }
            foreach (var cssm in m.stateMachines)
            {
                m.SetStateMachineTransitions(cssm.stateMachine, m.GetStateMachineTransitions(cssm.stateMachine).Where(o => o).ToArray());
                RemoveMissingTransitions(cssm.stateMachine);
            }
            EditorUtility.SetDirty(m);
        }

        static void ScanMachine(AnimatorStateMachine machine, bool isRootMachine, List<UnityEngine.Object> usedObjects)
        {
            usedObjects.Add(machine);
            foreach (var b in machine.behaviours)
                usedObjects.Add(b);
            foreach (var cs in machine.states)
            {
                var s = cs.state;
                usedObjects.Add(s);
                AddTree(s.motion as BlendTree, usedObjects);
                foreach (var t in s.transitions)
                {
                    if (!t) continue;
                    if (t.destinationState || t.destinationStateMachine || t.isExit)
                        usedObjects.Add(t);
                }
                foreach (var b in s.behaviours)
                    usedObjects.Add(b);
            }

            foreach (var t in machine.entryTransitions)
            {
                if (!t) continue;
                if (t.destinationState || t.destinationStateMachine || t.isExit)
                    usedObjects.Add(t);
            }

            if (isRootMachine)
            {
                foreach (var t in machine.anyStateTransitions)
                {
                    if (!t) continue;
                    if (t.destinationState || t.destinationStateMachine || t.isExit)
                        usedObjects.Add(t);
                }
            }

            for (int i = 0; i < machine.stateMachines.Length; i++)
                ScanMachine(machine.stateMachines[i].stateMachine, false, usedObjects);
            
            FinalScanMachine(machine, usedObjects);
            for (int i = 0; i < machine.stateMachines.Length; i++)
                FinalScanMachine(machine.stateMachines[i].stateMachine, usedObjects);
            
        }

        static void FinalScanMachine(AnimatorStateMachine machine, List<UnityEngine.Object> usedObjects)
        {
            foreach (var cssm in machine.stateMachines)
            {
                if (!cssm.stateMachine) continue;
                foreach (var t in machine.GetStateMachineTransitions(cssm.stateMachine))
                {
                    if (!t) continue;
                    if (t.destinationState || t.destinationStateMachine || t.isExit)
                        usedObjects.Add(t);
                }
                FinalScanMachine(cssm.stateMachine, usedObjects);
            }
        }

        
        static void AddTree(BlendTree t, List<UnityEngine.Object> usedObjects)
        {
            if (!t) return;
            usedObjects.Add(t);
            foreach (var cm in t.children)
                AddTree(cm.motion as BlendTree, usedObjects);
        }
        
        #endregion //DREADRITH CONTROLLER CLEANER
    }
}
#endif