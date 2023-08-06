// RATS - Raz's Animator Tweaks'n Stuff
// Original AnimatorExtensions by Dj Lukis.LT, under MIT License

// Copyright (c) 2023 Razgriz
// SPDX-License-Identifier: MIT

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using ReorderableList = UnityEditorInternal.ReorderableList;
#if RATS_HARMONY
using HarmonyLib;
#endif

namespace Razgriz.RATS
{
    [InitializeOnLoad]
    public partial class RATS
    {
        private static int wait = 0;
        public static RATSPreferences Prefs = new RATSPreferences();

        static RATS()
        {
            // Debug.Log($"[RATS ]");
            RATSGUI.HandlePreferences();
            // Register our patch delegate
            EditorApplication.update -= DoPatches;
            EditorApplication.update += DoPatches;
            EditorApplication.update -= TextureWatchdog;
            EditorApplication.update += TextureWatchdog;
        }

#if RATS_HARMONY
        public static Harmony harmonyInstance = new Harmony("Razgriz.RATS");
#endif
        static void DoPatches()
        {
            // Wait a couple cycles to patch to let static initializers run
            wait++;
            if(wait > 2)
            {
                // Unregister our delegate so it doesn't run again
                EditorApplication.update -= DoPatches;

#if RATS_HARMONY
                harmonyInstance.PatchAll();
                Debug.Log($"[RATS v{RATS.Version}] Patches Applied!");
#else
                Debug.LogWarning($"[RATS v{RATS.Version}] 0Harmony.dll not found - Patches Skipped!");
#endif
                HandleTextures();
            }
        }

        // Try to keep textures around as much as possible
        private static void TextureWatchdog()
        {
            if(nodeBackgroundImage == (Texture2D) null)
            {
                HandleTextures();
            }
        }
        
        const string packageJsonGUID = "752640b8a8602f74e940d13a53c5fdae";  
        static string version;
        public static string Version
        {
            get
            {
                if(String.IsNullOrEmpty(version))
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(packageJsonGUID);
                    if(String.IsNullOrEmpty(assetPath))
                        version = "0.0.0";
                    else
                        version = JsonUtility.FromJson<RATSPackageProxy>(File.ReadAllText(Path.GetFullPath(assetPath))).version;
                }

                return version;
            }

            private set {}
        }
        public class RATSPackageProxy
        {
            public string version;
        }

#if RATS_HARMONY
        #region Helpers

        public static void RecursivelyDetermineStateMachineWDStatus(AnimatorStateMachine stateMachine, ref int wdOnStateCount, ref int wdOffStateCount, int recursionDepth = 0)
        {
            if(stateMachine == null || recursionDepth > 10)
                return;

            foreach(var childState in stateMachine.states)
            {
                if(childState.state.writeDefaultValues)
                    wdOnStateCount++;
                else
                    wdOffStateCount++;
            }

            foreach(var subStateMachine in stateMachine.stateMachines)
            {
                RecursivelyDetermineStateMachineWDStatus(subStateMachine.stateMachine, ref wdOnStateCount, ref wdOffStateCount, recursionDepth + 1);
            }
        }

        public static void RecursivelyDetermineControllerWDStatus(AnimatorController controller, ref int layerCountWDOn, ref int layerCountWDOff)
        {
            foreach(var layer in controller.layers)
            {
                int wdOnStateCount = 0;
                int wdOffStateCount = 0;
                RecursivelyDetermineStateMachineWDStatus(layer.stateMachine, ref wdOnStateCount, ref wdOffStateCount);

                if(wdOnStateCount == 0 && wdOffStateCount == 0)
                {
                    // We don't consider the WD status of empty layers
                    continue;
                }
                else if(wdOnStateCount > 0 && wdOffStateCount > 0)
                {
                    layerCountWDOn++;
                    layerCountWDOff++;
                }
                else
                {
                    if(wdOnStateCount > 0)
                        layerCountWDOn++;
                    else
                        layerCountWDOff++;
                }
            }
        }

        // Recursive helper functions to gather deeply-nested parameter references
        private static void GatherBtParams(BlendTree bt, ref Dictionary<string, AnimatorControllerParameter> srcParams, ref Dictionary<string, AnimatorControllerParameter> queuedParams)
        {
            if (srcParams.ContainsKey(bt.blendParameter))
                queuedParams[bt.blendParameter] = srcParams[bt.blendParameter];
            if (srcParams.ContainsKey(bt.blendParameterY))
                queuedParams[bt.blendParameterY] = srcParams[bt.blendParameterY];

            foreach (var cmotion in bt.children)
            {
                if (srcParams.ContainsKey(cmotion.directBlendParameter))
                    queuedParams[cmotion.directBlendParameter] = srcParams[cmotion.directBlendParameter];

                // Go deeper to nested BlendTrees
                var cbt = cmotion.motion as BlendTree;
                if (!(cbt is null))
                    GatherBtParams(cbt, ref srcParams, ref queuedParams);
            }
        }
        
        private static void GatherSmParams(AnimatorStateMachine sm, ref Dictionary<string, AnimatorControllerParameter> srcParams, ref Dictionary<string, AnimatorControllerParameter> queuedParams)
        {
            // Go over states to check controlling or BlendTree params
            foreach (var cstate in sm.states)
            {
                var s = cstate.state;
                if (s.mirrorParameterActive && srcParams.ContainsKey(s.mirrorParameter))
                    queuedParams[s.mirrorParameter] = srcParams[s.mirrorParameter];
                if (s.speedParameterActive && srcParams.ContainsKey(s.speedParameter))
                    queuedParams[s.speedParameter] = srcParams[s.speedParameter];
                if (s.timeParameterActive && srcParams.ContainsKey(s.timeParameter))
                    queuedParams[s.timeParameter] = srcParams[s.timeParameter];
                if (s.cycleOffsetParameterActive && srcParams.ContainsKey(s.cycleOffsetParameter))
                    queuedParams[s.cycleOffsetParameter] = srcParams[s.cycleOffsetParameter];

                var bt = s.motion as BlendTree;
                if (!(bt is null))
                    GatherBtParams(bt, ref srcParams, ref queuedParams);
            }

            // Go over all transitions
            var transitions = new List<AnimatorStateTransition>(sm.anyStateTransitions.Length);
            transitions.AddRange(sm.anyStateTransitions);
            foreach (var cstate in sm.states)
                transitions.AddRange(cstate.state.transitions);
            foreach (var transition in transitions)
            foreach (var cond in transition.conditions)
                if (srcParams.ContainsKey(cond.parameter))
                    queuedParams[cond.parameter] = srcParams[cond.parameter];

            // Go deeper to child sate machines
            foreach (var csm in sm.stateMachines)
                GatherSmParams(csm.stateMachine, ref srcParams, ref queuedParams);
        }

        // Layer Copy/Paste Functions
        private static void CopyLayer(object layerControllerView, ref AnimatorControllerLayer layerClipboard, ref AnimatorController controllerClipboard)
        {
            var rlist = (ReorderableList)LayerListField.GetValue(layerControllerView);
            var ctrl = Traverse.Create(layerControllerView).Field("m_Host").Property("animatorController").GetValue<AnimatorController>();
            layerClipboard = rlist.list[rlist.index] as AnimatorControllerLayer;
            controllerClipboard = ctrl;
            Unsupported.CopyStateMachineDataToPasteboard(layerClipboard.stateMachine, ctrl, rlist.index);
        }

        public static void PasteLayer(object layerControllerView, ref AnimatorControllerLayer layerClipboard, ref AnimatorController controllerClipboard)
        {
            if (layerClipboard == null)
                return;
            var rlist = (ReorderableList)LayerListField.GetValue(layerControllerView);
            var ctrl = Traverse.Create(layerControllerView).Field("m_Host").Property("animatorController").GetValue<AnimatorController>();

            // Will paste layer right below selected one
            int targetindex = rlist.index + 1;
            string newname = ctrl.MakeUniqueLayerName(layerClipboard.name);
            Undo.FlushUndoRecordObjects();

            // Use unity built-in function to clone state machine
            ctrl.AddLayer(newname);
            var layers = ctrl.layers;
            int pastedlayerindex = layers.Length - 1;
            var pastedlayer = layers[pastedlayerindex];
            Unsupported.PasteToStateMachineFromPasteboard(pastedlayer.stateMachine, ctrl, pastedlayerindex, Vector3.zero);

            // Promote from child to main
            var pastedsm = pastedlayer.stateMachine.stateMachines[0].stateMachine;
            pastedsm.name = newname;
            pastedlayer.stateMachine.stateMachines = new ChildAnimatorStateMachine[0];
            UnityEngine.Object.DestroyImmediate(pastedlayer.stateMachine, true);
            pastedlayer.stateMachine = pastedsm;
            PasteLayerProperties(pastedlayer, layerClipboard);

            // Move up to desired spot
            for (int i = layers.Length-1; i > targetindex; i--)
                layers[i] = layers[i - 1];
            layers[targetindex] = pastedlayer;
            ctrl.layers = layers;

            // Make layer unaffected by undo, forces user to delete manually but prevents dangling sub-assets
            Undo.ClearUndo(ctrl);

            // Pasting to different controller, sync parameters
            if (ctrl != controllerClipboard)
            {
                Undo.IncrementCurrentGroup();
                int curgroup = Undo.GetCurrentGroup();
                Undo.RecordObject(ctrl, "Sync pasted layer parameters");

                // cache names
                // TODO: do this before pasting to workaround default values not being copied
                var destparams = new Dictionary<string, AnimatorControllerParameter>(ctrl.parameters.Length);
                foreach (var param in ctrl.parameters)
                    destparams[param.name] = param;

                var srcparams = new Dictionary<string, AnimatorControllerParameter>(controllerClipboard.parameters.Length);
                foreach (var param in controllerClipboard.parameters)
                    srcparams[param.name] = param;

                var queuedparams = new Dictionary<string, AnimatorControllerParameter>(controllerClipboard.parameters.Length);

                // Recursively loop over all nested state machines
                GatherSmParams(pastedsm, ref srcparams, ref queuedparams);

                // Sync up whats missing
                foreach (var param in queuedparams.Values)
                {
                    string pname = param.name;
                    if (!destparams.ContainsKey(pname))
                    {
                        // Debug.Log("Transferring parameter "+pname); // TODO: count or concatenate names?
                        ctrl.AddParameter(param);
                        // note: queuedparams should not have duplicates so don't need to append to destparams
                    }
                }
                Undo.CollapseUndoOperations(curgroup);
            }

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Update list selection
            Traverse.Create(layerControllerView).Property("selectedLayerIndex").SetValue(targetindex);
        }

        public static void PasteLayerSettings(object layerControllerView, ref AnimatorControllerLayer layerClipboard)
        {
            var rlist = (ReorderableList)LayerListField.GetValue(layerControllerView);
            AnimatorController ctrl = Traverse.Create(layerControllerView).Field("m_Host").Property("animatorController").GetValue<AnimatorController>();

            var layers = ctrl.layers;
            var targetlayer = layers[rlist.index];
            PasteLayerProperties(targetlayer, layerClipboard);
            ctrl.layers = layers; // needed for edits to apply
        }

        public static void PasteLayerProperties(AnimatorControllerLayer dest, AnimatorControllerLayer src)
        {
            dest.avatarMask = src.avatarMask;
            dest.blendingMode = src.blendingMode;
            dest.defaultWeight = src.defaultWeight;
            dest.iKPass = src.iKPass;
            dest.syncedLayerAffectsTiming = src.syncedLayerAffectsTiming;
            dest.syncedLayerIndex = src.syncedLayerIndex;
        }

        #endregion Helpers

        #region ReflectionCache
        // Animator Window
        private static readonly Type AnimatorWindowType = AccessTools.TypeByName("UnityEditor.Graphs.AnimatorControllerTool");
        private static readonly MethodInfo AnimatorControllerGetter = AccessTools.PropertyGetter(AnimatorWindowType, "animatorController");

        private static readonly Type LayerControllerViewType = AccessTools.TypeByName("UnityEditor.Graphs.LayerControllerView");
        private static readonly FieldInfo LayerScrollField = AccessTools.Field(LayerControllerViewType, "m_LayerScroll");
        private static readonly FieldInfo LayerListField = AccessTools.Field(LayerControllerViewType, "m_LayerList");

        private static readonly Type IAnimatorControllerEditorType = AccessTools.TypeByName("UnityEditor.Graphs.IAnimatorControllerEditor");
        private static readonly FieldInfo IAnimatorControllerEditorField = AccessTools.Field(LayerControllerViewType, "m_Host");
        private static readonly Type AnimatorControllerViewType = AccessTools.TypeByName("UnityEditor.Graphs.AnimatorControllerTool");
        private static readonly FieldInfo AnimatorControllerField = AccessTools.Field(AnimatorControllerViewType, "m_AnimatorController");

        private static readonly Type RenameOverlayType = AccessTools.TypeByName("UnityEditor.RenameOverlay");
        private static readonly MethodInfo BeginRenameMethod = AccessTools.Method(RenameOverlayType, "BeginRename");

        private static readonly Type AnimatorWindowGraphGUIType = AccessTools.TypeByName("UnityEditor.Graphs.GraphGUI");
        private static readonly Type GraphStylesType = AccessTools.TypeByName("UnityEditor.Graphs.Styles");
        private static readonly Type ParameterControllerViewType = AccessTools.TypeByName("UnityEditor.Graphs.ParameterControllerView");
        private static readonly Type AnimatorTransitionInspectorBaseType = AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.AnimatorTransitionInspectorBase");

        private static readonly MethodInfo GetElementHeightMethod = AccessTools.Method(typeof(ReorderableList), "GetElementHeight", new Type[]{typeof(int)});
        private static readonly MethodInfo GetElementYOffsetMethod = AccessTools.Method(typeof(ReorderableList), "GetElementYOffset", new Type[]{typeof(int)});
        
        // Animation Window
        static readonly Type AnimationWindowHierarchyGUIType = AccessTools.TypeByName("UnityEditorInternal.AnimationWindowHierarchyGUI");
        static readonly Type AnimationWindowHierarchyNodeType = AccessTools.TypeByName("UnityEditorInternal.AnimationWindowHierarchyNode");
        static readonly Type AnimationWindowUtilityType = AccessTools.TypeByName("UnityEditorInternal.AnimationWindowUtility");

        static readonly Type AnimEditorType = AccessTools.TypeByName("UnityEditor.AnimEditor");

        static readonly FieldInfo NodeTypePropertyName = AnimationWindowHierarchyNodeType.GetField("propertyName", BindingFlags.Instance | BindingFlags.Public);
        static readonly FieldInfo NodeTypePath = AnimationWindowHierarchyNodeType.GetField("path", BindingFlags.Instance | BindingFlags.Public);
        static readonly FieldInfo NodeTypeAnimatableObjectType = AnimationWindowHierarchyNodeType.GetField("animatableObjectType", BindingFlags.Instance | BindingFlags.Public);
        static readonly FieldInfo NodeTypeIndent = AnimationWindowHierarchyNodeType.GetField("indent", BindingFlags.Instance | BindingFlags.Public);

        static readonly PropertyInfo NodeDisplayNameProp = AnimationWindowHierarchyNodeType.GetProperty("displayName", BindingFlags.Instance | BindingFlags.Public);
        #endregion ReflectionCache
#endif

        #region TextureHandling

        private static Texture2D nodeBackgroundImageMask;
        private static Color[] nodeBackgroundPixels;
        private static Color[] nodeBackgroundActivePixels;
        private static Color[] stateMachineBackgroundPixels;
        private static Color[] stateMachineBackgroundPixelsActive;

        private static Texture2D nodeBackgroundImage;
        private static Texture2D nodeBackgroundImageActive;
        private static Texture2D nodeBackgroundImageBlue;
        private static Texture2D nodeBackgroundImageBlueActive;
        private static Texture2D nodeBackgroundImageAqua;
        private static Texture2D nodeBackgroundImageAquaActive;
        private static Texture2D nodeBackgroundImageGreen;
        private static Texture2D nodeBackgroundImageGreenActive;
        private static Texture2D nodeBackgroundImageYellow;
        private static Texture2D nodeBackgroundImageYellowActive;
        private static Texture2D nodeBackgroundImageOrange;
        private static Texture2D nodeBackgroundImageOrangeActive;
        private static Texture2D nodeBackgroundImageRed;
        private static Texture2D nodeBackgroundImageRedActive;

        private static Texture2D stateMachineBackgroundImage;
        private static Texture2D stateMachineBackgroundImageActive;

        // TODO: This texture handling code feels pretty inefficient but it only runs when adjusting so I'm not too concerned
        static void HandleTextures()
        {
#if RATS_HARMONY
            RATS.AnimatorWindowState.handledNodeStyles.Clear();
#endif
            LoadGraphTextures();
            UpdateGraphTextures();
        }

        static void LoadGraphTextures()
        {
            byte[] nodeBackgroundBytes = System.IO.File.ReadAllBytes(Path.Combine(Directory.GetCurrentDirectory(), AssetDatabase.GUIDToAssetPath("780a9e3efb8a1ca42b44c98c5e972f2d")).Replace('/', Path.DirectorySeparatorChar));
            byte[] nodeBackgroundActiveBytes = System.IO.File.ReadAllBytes(Path.Combine(Directory.GetCurrentDirectory(), AssetDatabase.GUIDToAssetPath("4fb6ef4881973e24cbcf73cff14ae0c8")).Replace('/', Path.DirectorySeparatorChar));
            nodeBackgroundImageMask = LoadPNG(Path.Combine(Directory.GetCurrentDirectory(), AssetDatabase.GUIDToAssetPath("81dcb3a363364ea4f9a475b4cebb0eaf")).Replace('/', Path.DirectorySeparatorChar));
            
            stateMachineBackgroundImage = LoadPNG(Path.Combine(Directory.GetCurrentDirectory(), AssetDatabase.GUIDToAssetPath("160541e301c89e644a9c10fb82f74f88")).Replace('/', Path.DirectorySeparatorChar));
            stateMachineBackgroundPixels = stateMachineBackgroundImage.GetPixels();
            stateMachineBackgroundImageActive = LoadPNG(Path.Combine(Directory.GetCurrentDirectory(), AssetDatabase.GUIDToAssetPath("c430ad55db449494aa1caefe9dccdc2d")).Replace('/', Path.DirectorySeparatorChar));
            stateMachineBackgroundPixelsActive = stateMachineBackgroundImageActive.GetPixels();

            nodeBackgroundPixels = LoadPNG(nodeBackgroundBytes).GetPixels();
            nodeBackgroundActivePixels = LoadPNG(nodeBackgroundActiveBytes).GetPixels();

            nodeBackgroundImage = LoadPNG(nodeBackgroundBytes); 
            nodeBackgroundImageBlue = LoadPNG(nodeBackgroundBytes);
            nodeBackgroundImageAqua = LoadPNG(nodeBackgroundBytes);
            nodeBackgroundImageGreen = LoadPNG(nodeBackgroundBytes);
            nodeBackgroundImageYellow = LoadPNG(nodeBackgroundBytes);
            nodeBackgroundImageOrange = LoadPNG(nodeBackgroundBytes);
            nodeBackgroundImageRed = LoadPNG(nodeBackgroundBytes);

            nodeBackgroundImageActive = LoadPNG(nodeBackgroundActiveBytes);
            nodeBackgroundImageBlueActive = LoadPNG(nodeBackgroundActiveBytes);
            nodeBackgroundImageAquaActive = LoadPNG(nodeBackgroundActiveBytes);
            nodeBackgroundImageGreenActive = LoadPNG(nodeBackgroundActiveBytes);
            nodeBackgroundImageYellowActive = LoadPNG(nodeBackgroundActiveBytes);
            nodeBackgroundImageOrangeActive = LoadPNG(nodeBackgroundActiveBytes);
            nodeBackgroundImageRedActive = LoadPNG(nodeBackgroundActiveBytes);

            // These aren't really used as far as I can tell, so no user customization needed
            TintTexture2D(ref nodeBackgroundImageBlue, nodeBackgroundImageMask, new Color(27/255f, 27/255f, 150/255f, 1f));
            TintTexture2D(ref nodeBackgroundImageYellow, nodeBackgroundImageMask, new Color(204/255f, 165/255f, 39/255f, 1f));
        }

        public static void UpdateGraphTextures()
        {
            try
            {
                Texture2D glowState = new Texture2D(nodeBackgroundImageActive.width, nodeBackgroundImageActive.height);
                Texture2D glowStateMachine = new Texture2D(stateMachineBackgroundImageActive.width, stateMachineBackgroundImageActive.height);
                glowState.SetPixels(nodeBackgroundActivePixels);
                glowStateMachine.SetPixels(stateMachineBackgroundPixelsActive);
                TintTexture2D(ref glowState, RATS.Prefs.StateGlowColor);
                TintTexture2D(ref glowStateMachine, RATS.Prefs.StateGlowColor);
                Color[] glowData = glowState.GetPixels();
                Color[] glowStateMachineData = glowStateMachine.GetPixels();

                stateMachineBackgroundImage.SetPixels(stateMachineBackgroundPixels);

                nodeBackgroundImageBlue.SetPixels(nodeBackgroundPixels);
                nodeBackgroundImageYellow.SetPixels(nodeBackgroundPixels);
                nodeBackgroundImage.SetPixels(nodeBackgroundPixels);
                nodeBackgroundImageAqua.SetPixels(nodeBackgroundPixels);
                nodeBackgroundImageGreen.SetPixels(nodeBackgroundPixels);
                nodeBackgroundImageOrange.SetPixels(nodeBackgroundPixels);
                nodeBackgroundImageRed.SetPixels(nodeBackgroundPixels);

                stateMachineBackgroundImageActive.SetPixels(glowStateMachineData);

                nodeBackgroundImageActive.SetPixels(glowData);
                nodeBackgroundImageBlueActive.SetPixels(glowData);
                nodeBackgroundImageYellowActive.SetPixels(glowData);
                nodeBackgroundImageAquaActive.SetPixels(glowData);
                nodeBackgroundImageGreenActive.SetPixels(glowData);
                nodeBackgroundImageOrangeActive.SetPixels(glowData);
                nodeBackgroundImageRedActive.SetPixels(glowData);

                // Main color tint
                TintTexture2D(ref stateMachineBackgroundImage, RATS.Prefs.SubStateMachineColor);
                TintTexture2D(ref nodeBackgroundImage, RATS.Prefs.StateColorGray);
                TintTexture2D(ref nodeBackgroundImageAqua, RATS.Prefs.StateColorAqua);
                TintTexture2D(ref nodeBackgroundImageGreen, RATS.Prefs.StateColorGreen);
                TintTexture2D(ref nodeBackgroundImageOrange, RATS.Prefs.StateColorOrange);
                TintTexture2D(ref nodeBackgroundImageRed, RATS.Prefs.StateColorRed); 

                // Glowing edge for selected
                AddTexture2D(ref stateMachineBackgroundImageActive, stateMachineBackgroundImage);
                AddTexture2D(ref nodeBackgroundImageActive, nodeBackgroundImage);
                AddTexture2D(ref nodeBackgroundImageBlueActive, nodeBackgroundImageBlue);
                AddTexture2D(ref nodeBackgroundImageAquaActive, nodeBackgroundImageAqua);
                AddTexture2D(ref nodeBackgroundImageGreenActive, nodeBackgroundImageGreen);
                AddTexture2D(ref nodeBackgroundImageYellowActive, nodeBackgroundImageYellow);
                AddTexture2D(ref nodeBackgroundImageOrangeActive, nodeBackgroundImageOrange);
                AddTexture2D(ref nodeBackgroundImageRedActive, nodeBackgroundImageRed);
            }
            catch(MissingReferenceException e)
            {
                Debug.LogWarning("[RATS] Texture Update Exception Caught: " + e.ToString());
            }
        }

        private static byte[] GetFileBytes(string filePath)
        {
            byte[] fileData = System.IO.File.ReadAllBytes(filePath);
            return fileData;
        }

        public static Texture2D LoadPNGFromGUID(string guid)
        {
            return LoadPNG(Path.Combine(Directory.GetCurrentDirectory(), AssetDatabase.GUIDToAssetPath(guid)).Replace('/', Path.DirectorySeparatorChar));
        }

        public static Texture2D LoadPNG(string filePath)
        {
            Texture2D tex = null;
            byte[] fileData;

            if (System.IO.File.Exists(filePath))
            {
                fileData = System.IO.File.ReadAllBytes(filePath);
                tex = new Texture2D(2, 2);
                tex.LoadImage(fileData); //..this will auto-resize the texture dimensions.
            }
            return tex;
        }

        public static string FindFile(string name, string type=null)
        {
            string[] guids;
            if (type != null)
                guids = AssetDatabase.FindAssets(name + " t:" + type);
            else
                guids = AssetDatabase.FindAssets(name);
            if (guids.Length == 0)
                return null;
            return AssetDatabase.GUIDToAssetPath(guids[0]);
        }

        public static Texture2D LoadPNG(byte[] fileData)
        {
            Texture2D tex = null;
            tex = new Texture2D(2, 2);
            tex.LoadImage(fileData); //..this will auto-resize the texture dimensions.
            return tex;
        }

        private static void TintTexture2D(ref Texture2D texture, Color tint, bool recalculateMips = true, bool makeNoLongerReadable = false)
        {
            Color[] pixels = texture.GetPixels();
            Parallel.For(0, pixels.Length, (j, state) => { pixels[j] *= tint; });

            texture.SetPixels(pixels);
            texture.Apply(recalculateMips, makeNoLongerReadable);
        } 

        private static void TintTexture2D(ref Texture2D texture, Texture2D maskTexture, Color tint, bool recalculateMips = true, bool makeNoLongerReadable = false)
        {
            Color[] pixels = texture.GetPixels();
            Color[] mask = maskTexture.GetPixels();
            Parallel.For(0, pixels.Length, (j, state) => { pixels[j] *= Color.Lerp(Color.white, tint, mask[j].r); });

            texture.SetPixels(pixels);
            texture.Apply(recalculateMips, makeNoLongerReadable);
        }

        private static void AddTexture2D(ref Texture2D texture, Texture2D textureToAdd, bool recalculateMips = true, bool makeNoLongerReadable = false)
        {
            Color[] pixels = texture.GetPixels();
            Color[] pixelsToAdd = textureToAdd.GetPixels();
            Parallel.For(0, pixels.Length, (j, state) => { pixels[j] += (pixelsToAdd[j] * pixelsToAdd[j].a);});

            texture.SetPixels(pixels); 
            texture.Apply(recalculateMips, makeNoLongerReadable);
        }

        #endregion TextureHandling
    }
}
#endif
