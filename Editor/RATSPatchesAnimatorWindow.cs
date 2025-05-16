// RATS - Raz's Animator Tweaks'n Stuff
// Original AnimatorExtensions by Dj Lukis.LT, under MIT License

// Copyright (c) 2023 Razgriz
// SPDX-License-Identifier: MIT

#if UNITY_EDITOR && RATS_HARMONY
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using ReorderableList = UnityEditorInternal.ReorderableList;
using HarmonyLib;
#if VRC_SDK_VRCSDK3 && !UDON
using VRC.SDK3.Avatars.Components;
#endif

namespace Razgriz.RATS
{
    public partial class RATS
    {
        internal static class AnimatorWindowState
        {
            // Layer scroll fixes
            internal static bool refocusSelectedLayer = false;
            internal static Vector2 layerScrollCache;

            // Transition condition parameter handling
            internal static int ConditionIndex;
            internal static int ConditionMode_pre;
            internal static string ConditionParam_pre;
            internal static AnimatorControllerParameterType ConditionParamType_pre;

            // Node Background Patching
            internal static HashSet<string> handledNodeStyles = new HashSet<string>();
            internal static Dictionary<string, bool> nodeBackgroundPatched = new Dictionary<string, bool>();

            // Layer copy/paste
            internal static AnimatorControllerLayer layerClipboard = null;
            internal static AnimatorController controllerClipboard = null;

            // Transition Menu
            internal static AnimatorTransitionBase redirectTransition;
            internal static AnimatorTransitionBase replicateTransition;

            // State Menu
            internal static AnimatorState multipleState;

            // Double Clicks
            internal static double doubleClickLastClick;
            internal static bool doubleClickLeftControlDown;
        }

#if !RATS_NO_ANIMATOR // Compatibility
        #region BugFixes
        // Prevent scroll position reset when rearranging or editing layers
        [HarmonyPatch]
        [HarmonyPriority(Priority.Low)]
        class PatchLayerScrollReset
        {
            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => AccessTools.Method(LayerControllerViewType,	"ResetUI");

            [HarmonyPrefix]
            static void Prefix(object __instance)
            {
                AnimatorWindowState.layerScrollCache = (Vector2)LayerScrollField.GetValue(__instance);
            }

            [HarmonyPostfix]
            static void Postfix(object __instance)
            {
                Vector2 scrollpos = (Vector2)LayerScrollField.GetValue(__instance);
                if (scrollpos.y == 0)
                    LayerScrollField.SetValue(__instance, AnimatorWindowState.layerScrollCache);
                AnimatorWindowState.refocusSelectedLayer = true; // Defer focusing to OnGUI to get latest list size and window rect
            }
        }

        // Scroll to parameter list bottom when adding a new one to see the rename field
        [HarmonyPatch]
        [HarmonyPriority(Priority.Low)]
        class PatchNewParameterScroll
        {
            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => AccessTools.Method(ParameterControllerViewType, "AddParameterMenu");

            [HarmonyPostfix]
            public static void Postfix(object __instance, object value)
            {
                Traverse.Create(__instance).Field("m_ScrollPosition").SetValue(new Vector2(0, 9001));
            }
        }

#if !UNITY_2021_3_OR_NEWER
        // Break 'undo' of sub-state machine pasting
        [HarmonyPatch]
        [HarmonyPriority(Priority.Low)]
        class PatchBreakUndoSubStateMachinePaste
        {
            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => AccessTools.Method(typeof(Unsupported), "PasteToStateMachineFromPasteboard");

            [HarmonyPostfix]
            static void Postfix(AnimatorStateMachine sm, AnimatorController controller, int layerIndex, Vector3 position)
            {
                Undo.ClearUndo(sm);
            }
        }
#endif

        // Prevent transition condition parameter change from altering the condition function
        [HarmonyPatch]
        [HarmonyPriority(Priority.Low)]
        class PatchTransitionConditionChangeBreakingCondition
        {
            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => AccessTools.Method(AnimatorTransitionInspectorBaseType, "DrawConditionsElement");

            [HarmonyPrefix]
            static void Prefix(object __instance, Rect rect, int index, bool selected, bool focused)
            {
                AnimatorWindowState.ConditionIndex = index;
                SerializedProperty conditions = (SerializedProperty)Traverse.Create(__instance).Field("m_Conditions").GetValue();
                SerializedProperty arrayElementAtIndex = conditions.GetArrayElementAtIndex(index);
                AnimatorWindowState.ConditionMode_pre = arrayElementAtIndex.FindPropertyRelative("m_ConditionMode").intValue;
                AnimatorWindowState.ConditionParam_pre = arrayElementAtIndex.FindPropertyRelative("m_ConditionEvent").stringValue;

                AnimatorController ctrl = Traverse.Create(__instance).Field("m_Controller").GetValue() as AnimatorController;
                if (ctrl)
                {
                    // Unity, why make IndexOfParameter(name) internal -_-
                    foreach (var param in ctrl.parameters)
                    {
                        if (param.name.Equals(AnimatorWindowState.ConditionParam_pre))
                        {
                            AnimatorWindowState.ConditionParamType_pre = param.type;
                            break;
                        }
                    }
                }
            }

            [HarmonyPostfix]
            static void Postfix(object __instance, Rect rect, int index, bool selected, bool focused)
            {
                if (AnimatorWindowState.ConditionIndex == index)
                {
                    SerializedProperty conditions = (SerializedProperty)Traverse.Create(__instance).Field("m_Conditions").GetValue();
                    SerializedProperty arrayElementAtIndex = conditions.GetArrayElementAtIndex(index);
                    SerializedProperty m_ConditionMode = arrayElementAtIndex.FindPropertyRelative("m_ConditionMode");
                    string conditionparam_post = arrayElementAtIndex.FindPropertyRelative("m_ConditionEvent").stringValue;

                    if (!conditionparam_post.Equals(AnimatorWindowState.ConditionParam_pre) && (m_ConditionMode.intValue != AnimatorWindowState.ConditionMode_pre))
                    {
                        // Parameter and condition changed, restore condition if parameter type is same
                        AnimatorController ctrl = Traverse.Create(__instance).Field("m_Controller").GetValue() as AnimatorController;
                        if (ctrl)
                        {
                            // Unity, why make IndexOfParameter(name) internal -_-
                            foreach (var param in ctrl.parameters)
                            {
                                if (param.name.Equals(conditionparam_post))
                                {
                                    // same type or float->int, fully compatible
                                    if ((param.type == AnimatorWindowState.ConditionParamType_pre)
                                        || ((AnimatorWindowState.ConditionParamType_pre == AnimatorControllerParameterType.Float) && (param.type == AnimatorControllerParameterType.Int)))
                                    {
                                        m_ConditionMode.intValue = AnimatorWindowState.ConditionMode_pre;
                                        // Debug.Log("[RATS] Restored transition condition mode");
                                    }
                                    // int->float has restrictions
                                    else if ((AnimatorWindowState.ConditionParamType_pre == AnimatorControllerParameterType.Int) && (param.type == AnimatorControllerParameterType.Float))
                                    {
                                        AnimatorConditionMode premode = (AnimatorConditionMode)AnimatorWindowState.ConditionMode_pre;
                                        if ((premode != AnimatorConditionMode.Equals) && (premode != AnimatorConditionMode.NotEqual))
                                        {
                                            m_ConditionMode.intValue = AnimatorWindowState.ConditionMode_pre;
                                            // Debug.Log("[RATS] Restored transition condition mode 2");
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        #endregion BugFixes

        #region LayerFeatures
        // Default Layer Weight = 1
        [HarmonyPatch]
        [HarmonyPriority(Priority.Low)]
        class PatchLayerWeightDefault
        {
            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => AccessTools.Method(typeof(UnityEditor.Animations.AnimatorController), "AddLayer", new Type[] {typeof(AnimatorControllerLayer)});

            [HarmonyPrefix]
            static void Prefix(ref AnimatorControllerLayer layer)
            {
                layer.defaultWeight = RATS.Prefs.DefaultLayerWeight1 ? 1.0f : 0.0f;
            }
        }

        // Layer copy-pasting
        [HarmonyPatch]
        [HarmonyPriority(Priority.Low)]
        class PatchLayerCopyPaste
        {
            static void CopyLayerMenuOption(object layerControllerView) => CopyLayer(layerControllerView, ref AnimatorWindowState.layerClipboard, ref AnimatorWindowState.controllerClipboard);
            static void PasteLayerMenuOption(object layerControllerView) => PasteLayer(layerControllerView, ref AnimatorWindowState.layerClipboard, ref AnimatorWindowState.controllerClipboard);
            static void PasteLayerSettingsMenuOption(object layerControllerView) => PasteLayerSettings(layerControllerView, ref AnimatorWindowState.layerClipboard);

            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => AccessTools.Method(LayerControllerViewType,	"OnDrawLayer");

            [HarmonyPrefix]
            public static void Prefix(object __instance, Rect rect, int index, bool selected, bool focused)
            {
                Event current = Event.current;
                if (((current.type == EventType.MouseUp) && (current.button == 1)) && rect.Contains(current.mousePosition))
                {
                    current.Use();
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(EditorGUIUtility.TrTextContent("Copy layer", null, (Texture) null), false,
                        new GenericMenu.MenuFunction2(CopyLayerMenuOption), __instance);
                    if (AnimatorWindowState.layerClipboard != null)
                    {
                        menu.AddItem(EditorGUIUtility.TrTextContent("Paste layer", null, (Texture) null), false,
                            new GenericMenu.MenuFunction2(PasteLayerMenuOption), __instance);
                        menu.AddItem(EditorGUIUtility.TrTextContent("Paste layer settings", null, (Texture) null), false,
                            new GenericMenu.MenuFunction2(PasteLayerSettingsMenuOption), __instance);
                    }
                    else
                    {
                        menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Paste layer", null, (Texture) null));
                        menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Paste layer settings", null, (Texture) null));
                    }
                    menu.AddItem(EditorGUIUtility.TrTextContent("Delete layer", null, (Texture) null), false,
                        new GenericMenu.MenuFunction(() => Traverse.Create(__instance).Method("DeleteLayer").GetValue(null)));
                    menu.ShowAsContext();
                }
            }

        }
        
        // Keyboard hooks for layer editing
        [HarmonyPatch]
        [HarmonyPriority(Priority.Low)]
        class PatchLayerCopyPasteKeyboardHooks
        {
            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => AccessTools.Method(LayerControllerViewType, "OnGUI");

            [HarmonyPrefix]
            public static void Prefix(object __instance, Rect rect)
            {
                var rlist = (ReorderableList)LayerListField.GetValue(__instance);
                if (rlist.HasKeyboardControl())
                {
                    Event current = Event.current;
                    switch (current.type)
                    {
                        case EventType.ExecuteCommand:
                            if (current.commandName == "Copy")
                            {
                                current.Use();
                                CopyLayer(__instance, ref AnimatorWindowState.layerClipboard, ref AnimatorWindowState.controllerClipboard);
                            }
                            else if (current.commandName == "Paste")
                            {
                                current.Use();
                                PasteLayer(__instance, ref AnimatorWindowState.layerClipboard, ref AnimatorWindowState.controllerClipboard);
                            }
                            else if (current.commandName == "Duplicate")
                            {
                                current.Use();
                                CopyLayer(__instance, ref AnimatorWindowState.layerClipboard, ref AnimatorWindowState.controllerClipboard);
                                PasteLayer(__instance, ref AnimatorWindowState.layerClipboard, ref AnimatorWindowState.controllerClipboard);
                            }
                            break;

                        case EventType.KeyDown:
                        {
                            KeyCode keyCode = Event.current.keyCode;
                            if (keyCode == KeyCode.F2) // Rename
                            {
                                current.Use();
                                AnimatorWindowState.refocusSelectedLayer = true;
                                AnimatorControllerLayer layer = rlist.list[rlist.index] as AnimatorControllerLayer;
                                var rovl = Traverse.Create(__instance).Property("renameOverlay").GetValue();
                                BeginRenameMethod.Invoke(rovl, new object[] {layer.name, rlist.index, 0.1f});
                                break;
                            }
                            break;
                        }
                    }
                }

                // Adjust scroll to get selected layer visible
                if (AnimatorWindowState.refocusSelectedLayer)
                {
                    AnimatorWindowState.refocusSelectedLayer = false;
                    Vector2 curscroll = (Vector2)LayerScrollField.GetValue(__instance);
                    float height = (float)GetElementHeightMethod.Invoke(rlist, new object[] {rlist.index}) + 20;
                    float offs = (float)GetElementYOffsetMethod.Invoke(rlist, new object[] {rlist.index});
                    if (offs < curscroll.y)
                        LayerScrollField.SetValue(__instance, new Vector2(curscroll.x,offs));
                    else if (offs+height > curscroll.y+rect.height)
                        LayerScrollField.SetValue(__instance, new Vector2(curscroll.x,offs+height-rect.height));
                }
            }
        }

        [HarmonyPatch]
        [HarmonyPriority(Priority.Low)]
        class PatchLayerWriteDefaultsIndicator
        {
            private static GUIStyle LayerWDStyle = new GUIStyle(EditorStyles.boldLabel);

            static PatchLayerWriteDefaultsIndicator()
            {
                LayerWDStyle.fontSize = 9;
            }

            // TODO: Not sure if this should be done for every layer but gonna stick with it for now
            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => AccessTools.Method(LayerControllerViewType, "OnDrawLayer");

            [HarmonyPrefix]
            static void Prefix(object __instance, Rect rect, int index, bool selected, bool focused)
            {
                // Don't show if not configured, or if playing (prevents indicator from getting pushed off screen)
                if(!(Prefs.LayerListShowWD || Prefs.LayerListShowMixedWD) || EditorApplication.isPlaying)
                    return;

                AnimatorController controller = (AnimatorController)AnimatorControllerField.GetValue(IAnimatorControllerEditorField.GetValue(__instance));

                var layerStateMachine = controller.layers[index].stateMachine;

                // Adjust position to be off to the left
                Rect layerLabelRect = rect;
                layerLabelRect.width = 18;
                layerLabelRect.height = 18;
                layerLabelRect.x -= 19;
                layerLabelRect.y += 15;

                if(layerStateMachine.states.Length == 0 && layerStateMachine.stateMachines.Length == 0)
                {
                    EditorGUI.LabelField(layerLabelRect, "  E", LayerWDStyle);
                    return; 
                }

                int layerWDOnCount = 0;
                int layerWDOffCount = 0;

                RATS.RecursivelyDetermineStateMachineWDStatus(layerStateMachine, ref layerWDOnCount, ref layerWDOffCount);

                if(Prefs.LayerListShowMixedWD && layerWDOnCount > 0 && layerWDOffCount > 0)
                {
                    layerLabelRect.width = 16;
                    layerLabelRect.height = 16;
                    layerLabelRect.x += 2;
                    EditorGUI.LabelField(layerLabelRect, new GUIContent(EditorGUIUtility.IconContent("Error").image, "Layer has mixed Write Defaults settings"));
                }
                else if(Prefs.LayerListShowWD)
                {
                    EditorGUI.LabelField(layerLabelRect, (layerWDOnCount > 0 ? "WD" : ""), LayerWDStyle);
                }
            }
        }

        #endregion LayerFeatures

        #region GraphFeatures
        // Set Default Transition Duration/Exit Time
        [HarmonyPatch]
        [HarmonyPriority(Priority.Low)]
        class PatchAnimatorNewTransitionDefaults
        {
            [HarmonyTargetMethods]
            static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(UnityEditor.Animations.AnimatorState), "CreateTransition");
                yield return AccessTools.Method(typeof(UnityEditor.Animations.AnimatorStateMachine), "AddAnyStateTransition");
            }

            [HarmonyPostfix]
            static void Postfix(object __instance, ref AnimatorStateTransition __result)
            {
                // Without this check, it throws warnings when inspecting unrelated transitions
                if(string.IsNullOrEmpty(AssetDatabase.GetAssetPath((UnityEngine.Object)__instance)))
                    return;

                __result.duration = RATS.Prefs.DefaultTransitionTime;
                __result.exitTime = RATS.Prefs.DefaultTransitionExitTime;
                __result.hasExitTime = RATS.Prefs.DefaultTransitionHasExitTime;
                __result.hasFixedDuration = RATS.Prefs.DefaultTransitionFixedDuration;
                __result.interruptionSource = (TransitionInterruptionSource)RATS.Prefs.DefaultTransitionInterruptionSource;
                __result.orderedInterruption = RATS.Prefs.DefaultTransitionOrderedInterruption;
                __result.canTransitionToSelf = RATS.Prefs.DefaultTransitionCanTransitionToSelf;
            }
        }

        // Write Defaults Default State
        [HarmonyPatch]
        [HarmonyPriority(Priority.Low)]
        class PatchAnimatorNewStateDefaults
        {
            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => AccessTools.Method(typeof(UnityEditor.Animations.AnimatorStateMachine), "AddState", new Type[] {typeof(AnimatorState), typeof(Vector3)});

            [HarmonyPrefix]
            static void Prefix(ref AnimatorState state, Vector3 position)
            {
                if(!RATS.Prefs.DefaultStateWriteDefaults) state.writeDefaultValues = false;
            }
        }

        [HarmonyPatch]
        [HarmonyPriority(Priority.Low)]
        class PatchStateMenu
        {
            [HarmonyTargetMethod]
            static MethodBase[] TargetMethods() => new[]
            { 
                AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.StateMachineNode"), "NodeUI"),
                AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.StateNode"), "NodeUI"),
                AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.AnyStateNode"), "NodeUI"),
                AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.EntryNode"), "NodeUI"),
            };

            private static StateMenuEntry[] Entries = {
                new MultipleTransitionEntry(),
                new RedirectMenuEntry(),
                new ReplicateMenuEntry()
            };
            
            static void AddMenuItems(object graph, GenericMenu menu)
            {
                if (!RATS.Prefs.ManipulateTransitionsMenuOption) return;
                if (Entries.Any(x => x.ShouldShow(graph))) menu.AddSeparator("");
                
                foreach (var entry in Entries)
                {
                    if (!entry.ShouldShow(graph)) continue;
                    if (entry.ShouldEnable(graph))
                    {
                        menu.AddItem(EditorGUIUtility.TrTextContent(entry.GetEntryName()), entry.ShouldCheck(graph), (object data) => entry.Callback(data, graph), Event.current.mousePosition);
                    }
                    else
                    {
                        menu.AddDisabledItem(EditorGUIUtility.TrTextContent(entry.GetEntryName()));
                    }
                }
            }
            
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(object __instance, IEnumerable<CodeInstruction> instructions)
            {
                var instructionList = instructions.ToList();
                int genericMenuLocalIndex = -1;
                for (var i = 0; i < instructionList.Count; i++)
                {
                    if (instructionList[i].opcode == OpCodes.Newobj && (ConstructorInfo)instructionList[i].operand == AccessTools.Constructor(typeof(GenericMenu), Type.EmptyTypes))
                    {
                        if (i + 1 < instructionList.Count && (instructionList[i + 1].opcode == OpCodes.Stloc_1 || 
                                                              instructionList[i + 1].opcode == OpCodes.Stloc_2 ||
                                                              instructionList[i + 1].opcode == OpCodes.Stloc_3 ||
                                                              instructionList[i + 1].opcode == OpCodes.Stloc_S))
                        {
                            if (instructionList[i + 1].opcode == OpCodes.Stloc_1)
                                genericMenuLocalIndex = 1;
                            else if (instructionList[i + 1].opcode == OpCodes.Stloc_2)
                                genericMenuLocalIndex = 2;
                            else if (instructionList[i + 1].opcode == OpCodes.Stloc_3)
                                genericMenuLocalIndex = 3;
                            else if (instructionList[i + 1].opcode == OpCodes.Stloc_S) 
                                genericMenuLocalIndex = ((LocalBuilder)instructionList[i + 1].operand).LocalIndex;
                        }
                    } 
                    
                    if (instructionList[i].opcode == OpCodes.Callvirt && (MethodInfo)instructionList[i].operand == AccessTools.Method(typeof(GenericMenu), "ShowAsContext"))
                    {
                        var newInstructions = new[]
                        {
                            new CodeInstruction(OpCodes.Ldarg_0),
                            new CodeInstruction(OpCodes.Ldloc_S, genericMenuLocalIndex),
                            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PatchStateMenu), "AddMenuItems")),
                        }; 
                        instructionList.InsertRange(i, newInstructions);
                        break;
                    }
                }
                return instructionList; 
            }
            
             interface StateMenuEntry
            {
                string GetEntryName();
                bool ShouldCheck(object data) => true;
                bool ShouldEnable(object data) => true;
                bool ShouldShow(object data) => true;
                void Callback(object graph, object data);
            }
            
            class MultipleTransitionEntry : StateMenuEntry
            {
                public string GetEntryName() => "Add Multiple Transitions";
                public bool ShouldCheck(object data) => AnimatorWindowState.multipleState != null;
                public bool ShouldEnable(object data) => true;
                public bool ShouldShow(object data) => true;
                public void Callback(object data, object stateNode)
                {
                    object graph =  AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.StateNode"),
                        "get_graph").Invoke(stateNode, new object[0]);
                    AnimatorStateMachine stateMachine = (AnimatorStateMachine)AccessTools
                        .Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.Graph"),
                            "get_activeStateMachine").Invoke(graph, new object[0]);

                    if (AnimatorWindowState.multipleState == null)
                    {
                        AnimatorState state = Selection.activeObject as AnimatorState;
                        if (state == null) return;
                        AnimatorWindowState.multipleState = state;
                        return;
                    }
                    
                    AnimatorState[] selectedStates = Selection.objects.Where(x => x is AnimatorState).Cast<AnimatorState>().ToArray();
                    foreach (var selectedState in selectedStates)
                    {
                        AnimatorWindowState.multipleState.AddTransition(selectedState);
                    }
                    AnimatorWindowState.multipleState = null;
                    AccessTools.TypeByName("UnityEditor.Graphs.AnimatorControllerTool").GetMethod("RebuildGraph").Invoke(AccessTools.TypeByName("UnityEditor.Graphs.AnimatorControllerTool").GetField("tool").GetValue(null), new object[]{false});
                }
            }
            
            class RedirectMenuEntry : StateMenuEntry
            {
                public string GetEntryName() => "Redirect";
                public bool ShouldShow(object data) => AnimatorWindowState.redirectTransition != null;
                public void Callback(object data, object stateNode)
                {
                    object graph =  AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.StateNode"),
                        "get_graph").Invoke(stateNode, new object[0]);
                    AnimatorStateMachine stateMachine = (AnimatorStateMachine)AccessTools
                        .Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.Graph"),
                            "get_activeStateMachine").Invoke(graph, new object[0]);
                    
                    AnimatorState[] selectedStates = Selection.objects.Where(x => x is AnimatorState).Cast<AnimatorState>().ToArray();
                    if (selectedStates.Length == 0) return;

                    AnimatorStateTransition transition = AnimatorWindowState.redirectTransition as AnimatorStateTransition;
                    

                    foreach (var selectedState in selectedStates)
                    {
                        if (!stateMachine.anyStateTransitions.Contains(transition))
                        {
                            ChildAnimatorState source = stateMachine.states.FirstOrDefault(x => x.state.transitions.Contains(transition));
                            if (source.state == null) continue;

                            AnimatorStateTransition newTransition = new AnimatorStateTransition()
                            {
                                canTransitionToSelf = transition.canTransitionToSelf,
                                conditions = transition.conditions.Select(x => x).ToArray(),
                                destinationState = selectedState,
                                duration = transition.duration,
                                exitTime = transition.exitTime,
                                hasExitTime = transition.hasExitTime,
                                hasFixedDuration = transition.hasFixedDuration,
                                hideFlags = transition.hideFlags,
                                interruptionSource = transition.interruptionSource,
                            };
                            source.state.AddTransition(newTransition);
                            if (AssetDatabase.GetAssetPath((UnityEngine.Object)source.state) != "") 
                                AssetDatabase.AddObjectToAsset((UnityEngine.Object) newTransition, AssetDatabase.GetAssetPath((UnityEngine.Object) source.state));
                        }
                        else
                        {
                            stateMachine.anyStateTransitions = stateMachine.anyStateTransitions.AddItem(new AnimatorStateTransition()
                            {
                                canTransitionToSelf = transition.canTransitionToSelf,
                                conditions = transition.conditions.Select(x => x).ToArray(),
                                duration = transition.duration,
                                destinationState = selectedState,
                                exitTime = transition.exitTime,
                                hasExitTime = transition.hasExitTime,
                                hasFixedDuration = transition.hasFixedDuration,
                                hideFlags = transition.hideFlags,
                                interruptionSource = transition.interruptionSource,
                            }).ToArray();
                            if (AssetDatabase.GetAssetPath(stateMachine) != "")
                                AssetDatabase.AddObjectToAsset((UnityEngine.Object) stateMachine.anyStateTransitions.Last(), AssetDatabase.GetAssetPath(stateMachine));
                        }
                    }
                    
                    AnimatorWindowState.redirectTransition = null;
                    AccessTools.TypeByName("UnityEditor.Graphs.AnimatorControllerTool").GetMethod("RebuildGraph").Invoke(AccessTools.TypeByName("UnityEditor.Graphs.AnimatorControllerTool").GetField("tool").GetValue(null), new object[]{false});
                }
            }
            
            class ReplicateMenuEntry : StateMenuEntry
            {
                public string GetEntryName() => "Replicate";
                public bool ShouldShow(object data) => AnimatorWindowState.replicateTransition != null;

                public void Callback(object data, object stateNode)
                {
                   
                    object graph =  AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.StateNode"),
                        "get_graph").Invoke(stateNode, new object[0]);
                    AnimatorStateMachine stateMachine = (AnimatorStateMachine)AccessTools
                        .Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.Graph"),
                            "get_activeStateMachine").Invoke(graph, new object[0]);
                    
                    AnimatorState[] selectedStates = Selection.objects.Where(x => x is AnimatorState).Cast<AnimatorState>().ToArray();
                    if (selectedStates.Length == 0) return;

                    AnimatorStateTransition transition = AnimatorWindowState.replicateTransition as AnimatorStateTransition;
                    
                    ChildAnimatorState target = stateMachine.states.FirstOrDefault(x => x.state == transition.destinationState);
                    if (target.state == null) return;
                    foreach (var selectedState in selectedStates)
                    {
                        AnimatorStateTransition newTransition = new AnimatorStateTransition()
                        {
                            canTransitionToSelf = transition.canTransitionToSelf,
                            conditions = transition.conditions.Select(x => x).ToArray(),
                            destinationState = target.state,
                            duration = transition.duration,
                            exitTime = transition.exitTime,
                            hasExitTime = transition.hasExitTime,
                            hasFixedDuration = transition.hasFixedDuration,
                            hideFlags = transition.hideFlags,
                            interruptionSource = transition.interruptionSource,
                        };
                        selectedState.AddTransition(newTransition);
                        if (AssetDatabase.GetAssetPath(stateMachine) != "")
                            AssetDatabase.AddObjectToAsset((UnityEngine.Object) newTransition, AssetDatabase.GetAssetPath(stateMachine));
                    }

                    AnimatorWindowState.replicateTransition = null;
                    AccessTools.TypeByName("UnityEditor.Graphs.AnimatorControllerTool").GetMethod("RebuildGraph").Invoke(AccessTools.TypeByName("UnityEditor.Graphs.AnimatorControllerTool").GetField("tool").GetValue(null), new object[]{false});
                }
            }
        }

        [HarmonyPatch]
        [HarmonyPriority(Priority.Low)]
        class PatchTransitionMenu
        {
            [HarmonyTargetMethod]
            static MethodBase[] TargetMethods() => new[]
            { 
                AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.GraphGUI"), "HandleContextMenu"),
            };

            private static TransitionMenuEntry[] Entries = {
                new ReverseMenuEntry(),
                new RedirectMenuEntry(),
                new ReplicateMenuEntry()
            };
            
            static GenericMenu ReplaceMenu(GenericMenu menu, object graph)
            {
                if (!RATS.Prefs.ManipulateTransitionsMenuOption) return menu;
                Vector2 current = Event.current.mousePosition;
                UnityEngine.Object o = Selection.activeObject;
                if (!(o is AnimatorTransitionBase transition)) return menu;

                AnimatorStateMachine stateMachine = (AnimatorStateMachine)AccessTools
                    .Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.GraphGUI"),
                        "get_activeStateMachine").Invoke(graph, new object[0]);
                
                ChildAnimatorState source = stateMachine.states.FirstOrDefault(x => x.state.transitions.Contains(transition));
                ChildAnimatorState target = stateMachine.states.FirstOrDefault(x => x.state == transition.destinationState);
                
                bool IsPointNearLine(Vector2 p, Vector2 a, Vector2 b, float d)
                {
                    Vector2 ab = b - a, ap = p - a;
                    float t = Mathf.Clamp(Vector2.Dot(ap, ab) / ab.sqrMagnitude, 0, 1);
                    return (p - (a + t * ab)).sqrMagnitude <= d * d;
                }

                current = current - new Vector2(100, 20); //Half of a state

                bool isAnyState = stateMachine.anyStateTransitions.Contains(transition);
                
                if ((!isAnyState && source.state != null && target.state != null && IsPointNearLine(current, source.position, target.position, 20)) ||
                    isAnyState && target.state != null && IsPointNearLine(current, stateMachine.anyStatePosition, target.position, 20))
                {
                    GenericMenu replaceMenu = new GenericMenu();
                    foreach (TransitionMenuEntry menuEntry in Entries)
                    {
                        if (menuEntry.ShouldEnable(graph))
                        {
                            replaceMenu.AddItem(EditorGUIUtility.TrTextContent(menuEntry.GetEntryName()), menuEntry.ShouldCheck(graph), (object data) => menuEntry.Callback(data, graph), Event.current.mousePosition);
                        }
                        else
                        {
                            replaceMenu.AddDisabledItem(EditorGUIUtility.TrTextContent(menuEntry.GetEntryName()));
                        }
                    }
                    return replaceMenu;
                }
                else
                {
                    return menu;
                }
            }
            
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(object __instance, IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                var instructionList = instructions.ToList();
                int genericMenuLocalIndex = -1;
                for (var i = 0; i < instructionList.Count; i++)
                {
                    if (instructionList[i].opcode == OpCodes.Callvirt && (MethodInfo)instructionList[i].operand == AccessTools.Method(typeof(GenericMenu), "ShowAsContext"))
                    {
                        var newInstructions = new[]
                        {
                            new CodeInstruction(OpCodes.Ldarg_0),
                            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PatchTransitionMenu), "ReplaceMenu")),
                        }; 
                        instructionList.InsertRange(i, newInstructions);
                        break;
                    }
                }
                return instructionList; 
            }

            interface TransitionMenuEntry
            {
                string GetEntryName();
                bool ShouldCheck(object data) => false;
                bool ShouldEnable(object data) => true;
                void Callback(object graph, object data);
            }
            
            class ReverseMenuEntry : TransitionMenuEntry
            {
                public string GetEntryName() => "Reverse";
                public bool ShouldEnable(object data)
                {
                    if (!(Selection.activeObject is AnimatorTransitionBase transition)) return false;
                    AnimatorStateMachine stateMachine = (AnimatorStateMachine)AccessTools
                        .Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.GraphGUI"),
                            "get_activeStateMachine").Invoke(data, new object[0]);
                    return !stateMachine.anyStateTransitions.Contains(transition);
                }
                
                public void Callback(object graph, object data)
                {
                    if (!(Selection.activeObject is AnimatorStateTransition transition)) return;
                    AnimatorStateMachine stateMachine = (AnimatorStateMachine)AccessTools
                        .Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.GraphGUI"),
                            "get_activeStateMachine").Invoke(data, new object[0]);
                
                    ChildAnimatorState source = stateMachine.states.FirstOrDefault(x => x.state.transitions.Contains(transition));
                    ChildAnimatorState target = stateMachine.states.FirstOrDefault(x => x.state == transition.destinationState);
                    if (source.state == null || target.state == null) return;
                    AnimatorStateTransition newTransition = new AnimatorStateTransition()
                    {
                        canTransitionToSelf = transition.canTransitionToSelf,
                        conditions = transition.conditions.Select(x => x).ToArray(),
                        destinationState = source.state,
                        duration = transition.duration,
                        exitTime = transition.exitTime,
                        hasExitTime = transition.hasExitTime,
                        hasFixedDuration = transition.hasFixedDuration,
                        hideFlags = transition.hideFlags,
                        interruptionSource = transition.interruptionSource,
                    };
                    target.state.AddTransition(newTransition);
                    if (AssetDatabase.GetAssetPath(stateMachine) != "")
                        AssetDatabase.AddObjectToAsset((UnityEngine.Object) newTransition, AssetDatabase.GetAssetPath(stateMachine));
                    AccessTools.TypeByName("UnityEditor.Graphs.AnimatorControllerTool").GetMethod("RebuildGraph").Invoke(AccessTools.TypeByName("UnityEditor.Graphs.AnimatorControllerTool").GetField("tool").GetValue(null), new object[]{false});
                }
            }
            
            class RedirectMenuEntry : TransitionMenuEntry
            {
                public string GetEntryName() => "Redirect";
                public bool ShouldCheck(object data) => AnimatorWindowState.redirectTransition != null;
                public void Callback(object graph, object data)
                {
                    if (!(Selection.activeObject is AnimatorStateTransition transition)) return;

                    if (AnimatorWindowState.redirectTransition == null)
                    {
                        AnimatorWindowState.redirectTransition = transition;
                        AnimatorWindowState.replicateTransition = null;
                    }
                    else
                    {
                        AnimatorWindowState.redirectTransition = null;
                    }
                }
            }
            
            class ReplicateMenuEntry : TransitionMenuEntry
            {
                public string GetEntryName() => "Replicate";
                public bool ShouldCheck(object data) => AnimatorWindowState.replicateTransition != null;
                public void Callback(object graph, object data)
                {
                    if (!(Selection.activeObject is AnimatorStateTransition transition)) return;

                    if (AnimatorWindowState.replicateTransition == null)
                    {
                        AnimatorWindowState.replicateTransition = transition;
                        AnimatorWindowState.redirectTransition = null;
                    }
                    else
                    {
                        AnimatorWindowState.replicateTransition = null;
                    }
                }
            }
        }

        [HarmonyPatch]
        [HarmonyPriority(Priority.Low)]
        class PatchDoubleClickStateMachine
        {
            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => AccessTools.Method(
                AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.GraphGUI"), "OnGraphGUI");

            [HarmonyPostfix]
            static void HandleDoubleClick(object __instance)
            {
                Event e = Event.current;
                if (e.type == EventType.KeyDown && e.keyCode == KeyCode.LeftControl)
                {
                    AnimatorWindowState.doubleClickLeftControlDown = true;
                }
                if (e.type == EventType.KeyUp && e.keyCode == KeyCode.LeftControl)
                {
                    AnimatorWindowState.doubleClickLeftControlDown = false;
                }
                
                if (e.type == EventType.MouseDown && e.button != 2 && AnimatorWindowState.doubleClickLeftControlDown)
                {
                    if (EditorApplication.timeSinceStartup - AnimatorWindowState.doubleClickLastClick <
                        RATS.Prefs.DoubleClickTimeInterval)
                    {
                        AnimatorWindowState.doubleClickLastClick = 0;
                        AnimatorStateMachine stateMachine = (AnimatorStateMachine)AccessTools
                            .Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.GraphGUI"),
                                "get_activeStateMachine").Invoke(__instance, new object[0]);
                        
                        AnimatorState newState = new AnimatorState();
                        stateMachine.AddState(newState, Event.current.mousePosition - new Vector2(100, 20));
                        if (AssetDatabase.GetAssetPath((UnityEngine.Object) stateMachine) != "")
                            AssetDatabase.AddObjectToAsset((UnityEngine.Object) newState, AssetDatabase.GetAssetPath((UnityEngine.Object) stateMachine));
                        newState.hideFlags = HideFlags.HideInHierarchy;
                        Event.current.Use();
                        AccessTools.TypeByName("UnityEditor.Graphs.AnimatorControllerTool").GetMethod("RebuildGraph").Invoke(AccessTools.TypeByName("UnityEditor.Graphs.AnimatorControllerTool").GetField("tool").GetValue(null), new object[]{false});
                    }
                    else
                    {
                        AnimatorWindowState.doubleClickLastClick = EditorApplication.timeSinceStartup;
                    }
                }
            }
        }

        [HarmonyPatch]
        [HarmonyPriority(Priority.Low)]
        class PatchDoubleClickEntryAnyState
        {
            private static MethodInfo IsDoubleClick;
            [HarmonyTargetMethod]
            static MethodBase[] TargetMethods() => new[]
            { 
                AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.AnyStateNode"), "NodeUI"),
                AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.EntryNode"), "NodeUI"),
            };
            
            [HarmonyPostfix]
            static void HandleDoubleClick(object __instance)
            {
                if (IsDoubleClick == null) IsDoubleClick = AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.Node"),  "IsDoubleClick");
                if ((bool)IsDoubleClick.Invoke(__instance, new object[0]) && AnimatorWindowState.doubleClickLeftControlDown)
                {
                    object graphGUI = AccessTools.Field(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.Node"),  "graphGUI").GetValue(__instance);
                    object edgeGUI = AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.GraphGUI"),
                        "get_edgeGUI").Invoke(graphGUI, new object[0]);
                    IEnumerable<UnityEditor.Graphs.Slot> outputSlots = (IEnumerable<UnityEditor.Graphs.Slot>) AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.Node"), "get_outputSlots").Invoke(__instance, new object[0]);
                    AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.IEdgeGUI"), "BeginSlotDragging",
                        new Type[] { typeof(UnityEditor.Graphs.Slot), typeof(bool), typeof(bool) }).Invoke(edgeGUI, new object[]
                    {
                        outputSlots.First(), true, false
                    }); 
                }
            }
        }
        
        [HarmonyPatch]
        [HarmonyPriority(Priority.Low)]
        class PatchDoubleClickNormalState
        {
            private static MethodInfo IsDoubleClick;
            [HarmonyTargetMethod]
            static MethodBase[] TargetMethods() => new[]
            { 
                AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.StateNode"), "NodeUI"),
                AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.StateMachineNode"), "NodeUI"),
            };
            
            [HarmonyPrefix]
            static void HandleDoubleClick(object __instance)
            {
                if (IsDoubleClick == null) IsDoubleClick = AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.Node"),  "IsDoubleClick");
                if ((bool)IsDoubleClick.Invoke(__instance, new object[0]) && AnimatorWindowState.doubleClickLeftControlDown)
                {
                    object graphGUI = AccessTools.Field(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.Node"),  "graphGUI").GetValue(__instance);
                    object edgeGUI = AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.GraphGUI"),
                        "get_edgeGUI").Invoke(graphGUI, new object[0]);
                    IEnumerable<UnityEditor.Graphs.Slot> outputSlots = (IEnumerable<UnityEditor.Graphs.Slot>) AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.Node"), "get_outputSlots").Invoke(__instance, new object[0]);
                    AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.IEdgeGUI"), "BeginSlotDragging",
                        new Type[] { typeof(UnityEditor.Graphs.Slot), typeof(bool), typeof(bool) }).Invoke(edgeGUI, new object[]
                    {
                        outputSlots.First(), true, false
                    }); 
                    Event.current.Use();
                }
            }
        }
        
        #endregion GraphFeatures

        #region ParametersFeatures

        [HarmonyPatch]
        [HarmonyPriority(Priority.Low)]
        class PatchAnimatorParameterVisuals
        {
            private static readonly Type ParameterControllerViewElementType = AccessTools.Inner(ParameterControllerViewType, "Element");
            private static readonly GUIStyle AnimatorParameterTypeLabel = new GUIStyle(EditorStyles.miniLabel);
            
            static PatchAnimatorParameterVisuals()
            {
                AnimatorParameterTypeLabel.alignment = TextAnchor.MiddleRight;
                AnimatorParameterTypeLabel.fontStyle = FontStyle.Bold;
                Color animatorParameterTypeLabelTextColor = AnimatorParameterTypeLabel.normal.textColor;
                animatorParameterTypeLabelTextColor.a = 0.4f;
                AnimatorParameterTypeLabel.normal.textColor = animatorParameterTypeLabelTextColor;
            }

            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => AccessTools.Method(ParameterControllerViewElementType, "OnGUI");

            [HarmonyPostfix]
            static void Postfix(object __instance, Rect rect, int index, bool selected, bool focused)
            {
                if(!Prefs.ParameterListShowParameterTypeLabels)
                    return;

                AnimatorControllerParameter animatorControllerParameter = Traverse.Create(__instance).Field("m_Parameter").GetValue<AnimatorControllerParameter>();
                string parameterTypeLabel = animatorControllerParameter.type.ToString();
                if(Prefs.ParameterListShowParameterTypeLabelShorten) parameterTypeLabel = parameterTypeLabel.Substring(0, 1); // Shorten to first letter

                // Label width is 66 wide, x position is 1 width before the parameter value
                // Minus 6f on the width to create spacing/padding on parameter value
                const float labelWidth = 66f;
                Rect labelTypeRect = new Rect(rect.xMax - labelWidth * 2f, rect.y, labelWidth - 6f, rect.height);
                GUI.Label(labelTypeRect, parameterTypeLabel, AnimatorParameterTypeLabel);
            }
        }

        // Check if parameter is being used in an entry transition or in parameter drivers and warn if so (unity doesn't do this)
        [HarmonyPatch]
        [HarmonyPriority(Priority.Low)]
        class PatchAnimatorParameterDelete
        {
            private static readonly FieldInfo IAnimatorControllerEditorField = AccessTools.Field(ParameterControllerViewType, "m_Host");
            private static readonly PropertyInfo ParameterControllerViewLiveLinkField = AccessTools.Property(IAnimatorControllerEditorType, "liveLink");

            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => AccessTools.Method(ParameterControllerViewType, "OnRemoveParameter");

            [HarmonyPrefix]
            static bool Prefix(object __instance, int index)
            {
                object iAnimatorControllerEditor = IAnimatorControllerEditorField.GetValue(__instance);
                object liveLinkValue = ParameterControllerViewLiveLinkField.GetValue(iAnimatorControllerEditor);
                bool liveLink = liveLinkValue != null && (bool)liveLinkValue;

                if(liveLink) return false;

                AnimatorController controller = (AnimatorController)AnimatorControllerField.GetValue(IAnimatorControllerEditorField.GetValue(__instance));
                AnimatorControllerParameter parameter = controller.parameters[index];
                
                List<AnimatorTransition> entryTransitions = new List<AnimatorTransition>();
                List<AnimatorState> statesWithParameterDrivers = new List<AnimatorState>();

                foreach(var layer in controller.layers)
                {
                    foreach(var entryTransition in layer.stateMachine.entryTransitions)
                    {
                        if(entryTransition.conditions.Any(condition => condition.parameter == parameter.name))
                        {
                            entryTransitions.Add(entryTransition);
                        }
                    }

#if VRC_SDK_VRCSDK3 && !UDON
                    foreach(var behavior in layer.stateMachine.behaviours)
                    {
                        if (behavior is VRC.SDK3.Avatars.Components.VRCAvatarParameterDriver parameterDriver)
                        {
                            if (parameterDriver.parameters.Any(param => param.name == parameter.name))
                            {
                                statesWithParameterDrivers.Add(layer.stateMachine.defaultState);
                            }
                        }
                    }

                    foreach(var childState in layer.stateMachine.states)
                    {
                        foreach(var behavior in childState.state.behaviours)
                        {
                            if (behavior is VRC.SDK3.Avatars.Components.VRCAvatarParameterDriver parameterDriver)
                            {
                                if (parameterDriver.parameters.Any(param => param.name == parameter.name))
                                {
                                    statesWithParameterDrivers.Add(layer.stateMachine.defaultState);
                                }
                            }
                        }
                    }
#endif
                }

                if(entryTransitions.Count > 0)
                {
                    string text = "Delete parameter " + parameter.name + "?";
                    string text2 = "It is used by : \n";
                    foreach(var transition in entryTransitions)
                    {
                        text2 += "Transition from entry to " + transition.destinationState.name + "\n";
                    }
                    foreach(var state in statesWithParameterDrivers)
                    {
                        text2 += "Parameter Driver on " + state.name + "\n";
                    }
                    // string message = $"Parameter '{parameter.name}' is being used in entry transitions. Are you sure you want to delete it?";
                    return EditorUtility.DisplayDialog("Delete Parameter", text2, "Yes", "No");
                }

                return true;
            }
        }


        #endregion ParametersFeatures

#endif //!RATS_NO_ANIMATOR

        #region GraphVisuals

        // Controller asset pinging/selection via bottom bar
        [HarmonyPatch]
        [HarmonyPriority(Priority.Low)]
        class PatchAnimatorBottomBar
        {
            static GUIStyle buttonStyle = new GUIStyle(EditorStyles.miniBoldLabel);

            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => AccessTools.Method(AnimatorWindowType, "DoGraphBottomBar");

            [HarmonyPostfix]
            static void Postfix(object __instance, Rect nameRect)
            {
                UnityEngine.Object ctrl = (UnityEngine.Object)AnimatorControllerGetter.Invoke(__instance, null);
                if (ctrl != (UnityEngine.Object)null)
                {
                    AnimatorController controller = (AnimatorController)ctrl;

                    int layerCountWDOn = 0;
                    int layerCountWDOff = 0;
                    RATS.RecursivelyDetermineControllerWDStatus(controller, ref layerCountWDOn, ref layerCountWDOff);

                    string WDStatus = "Mixed";

                    if(layerCountWDOn == 0)
                        WDStatus = "Off";
                    else if(layerCountWDOff == 0)
                        WDStatus = "On";

                    string compatibilityString = "";

                    #if RATS_NO_ANIMATOR
                    compatibilityString = " (Compatibility)";
                    #endif
                    GUIContent RATSLabel = new GUIContent($"  RATS{compatibilityString}  |  WD: {WDStatus}   ", (Texture)RATSGUI.GetRATSIcon());
                    float RATSLabelWidth = (buttonStyle).CalcSize(RATSLabel).x;
                    float controllerLabelWidth = (EditorStyles.miniLabel).CalcSize(new GUIContent(AssetDatabase.GetAssetPath(ctrl))).x;
                    float controllerIconWidth = 16;
                    Rect pingControllerRect = new Rect(nameRect.x + nameRect.width - controllerLabelWidth - controllerIconWidth, nameRect.y, controllerLabelWidth + controllerIconWidth, nameRect.height);
                    Rect RATSLabelrect = new Rect(nameRect.x + nameRect.width - pingControllerRect.width - RATSLabelWidth, nameRect.y, RATSLabelWidth, nameRect.height);

                    GUILayout.BeginArea(RATSLabelrect);
                    EditorGUILayout.LabelField(RATSLabel, buttonStyle);
                    GUILayout.EndArea();
                    EditorGUIUtility.AddCursorRect(RATSLabelrect, MouseCursor.Link); // Show hand cursor on hover

                    GUILayout.BeginArea(pingControllerRect);
                    EditorGUILayout.LabelField(new GUIContent((Texture)EditorGUIUtility.IconContent("AnimatorController On Icon").image), EditorStyles.miniLabel, GUILayout.Width(controllerIconWidth));
                    GUILayout.EndArea();
                    EditorGUIUtility.AddCursorRect(pingControllerRect, MouseCursor.Link);

                    Event current = Event.current;
                    if ((current.type == EventType.MouseDown) && (current.button == 0))
                    {
                        if(RATSLabelrect.Contains(current.mousePosition))
                        {
                            current.Use();
                            GenericMenu menu = new GenericMenu();
                            menu.AddItem(EditorGUIUtility.TrTextContent("RATS Options", null, (Texture) null), false,
                                new GenericMenu.MenuFunction2((object obj) => RATSGUI.ShowWindow()), null);
                            menu.AddItem(EditorGUIUtility.TrTextContent("Refresh Textures", null, (Texture) null), false,
                                new GenericMenu.MenuFunction2((object obj) => RATS.HandleTextures()), null);
                            menu.ShowAsContext();
                        }
                        else if(pingControllerRect.Contains(current.mousePosition))
                        {
                            current.Use();
                            EditorGUIUtility.PingObject(ctrl);
                            // Adhere to the 'select only on double click' convention
                            if (current.clickCount == 2) Selection.activeObject = ctrl;
                        }
                    } 
                    
                }
            }
        }

        // Graph Background
        [HarmonyPatch]
        [HarmonyPriority(Priority.Low)]
        class PatchAnimatorGridBackground
        {
            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => AccessTools.Method(AnimatorWindowGraphGUIType, "DrawGrid");

            [HarmonyPostfix]
            static void Postfix(object __instance, Rect gridRect, float zoomLevel)
            {
                if(!RATS.Prefs.GraphGridOverride || Event.current.type != UnityEngine.EventType.Repaint)
                    return;

                // Overwrite the whole grid drawing lol 
                GL.PushMatrix();

                // Draw Background
                GL.Begin(GL.QUADS);
                Color backgroundColor = RATS.Prefs.GraphGridBackgroundColor;
                backgroundColor.a = 1;
                GL.Color(backgroundColor);
                GL.Vertex(new Vector3(gridRect.xMin, gridRect.yMin));
                GL.Vertex(new Vector3(gridRect.xMin, gridRect.yMax));
                GL.Vertex(new Vector3(gridRect.xMax, gridRect.yMax));
                GL.Vertex(new Vector3(gridRect.xMax, gridRect.yMin));
                GL.End();

                // Draw Grid
                GL.Begin(GL.LINES);
                float tMajor = Mathf.InverseLerp(0.25f, 1f, zoomLevel);
                float tMinor = Mathf.InverseLerp(0.0f, 1f, zoomLevel * 0.5f);
                
                // Major
                GL.Color(Color.Lerp(Color.clear, RATS.Prefs.GraphGridColorMajor, tMajor));
                DrawGridLines(gridRect, RATS.Prefs.GraphGridScalingMajor * 100f);

                // Minor
                GL.Color(Color.Lerp(Color.clear, RATS.Prefs.GraphGridColorMinor, tMinor));
                DrawGridLines(gridRect, RATS.Prefs.GraphGridScalingMajor * (100f / RATS.Prefs.GraphGridDivisorMinor));

                GL.End();
                GL.PopMatrix();
            }

            static void DrawGridLines(Rect gridRect, float gridSize)
            {
                for(float x = gridRect.xMin - (gridRect.xMin % gridSize); x < gridRect.xMax; x += gridSize)
                {
                    GL.Vertex(new Vector3(x, gridRect.yMin)); 
                    GL.Vertex(new Vector3(x, gridRect.yMax));
                }
                for(float y = gridRect.yMin - (gridRect.yMin % gridSize); y < gridRect.yMax; y += gridSize)
                {
                    GL.Vertex(new Vector3(gridRect.xMin, y)); 
                    GL.Vertex(new Vector3(gridRect.xMax, y));
                }
            }
        }
    
        // Customize Grid Snapping
        [HarmonyPatch]
        [HarmonyPriority(Priority.Low)]
        class PatchAnimatorSnapToGrid
        {
            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => AccessTools.Method(AnimatorWindowGraphGUIType, "SnapPositionToGrid");

            [HarmonyPrefix]
            static bool Prefix(ref Rect __result, Rect position)
            {
                // Logical XOR
                bool doDesnap = Event.current.control ^ RATS.Prefs.GraphDragNoSnap;
                if(doDesnap)
                {
                    __result = position;
                    return false;
                }
                else if(RATS.Prefs.GraphDragSnapToModifiedGrid && RATS.Prefs.GraphGridOverride) // Enforce Minor Grid Spacing Snapping
                {
                    float minorGridSpacing = RATS.Prefs.GraphGridScalingMajor * (100f / RATS.Prefs.GraphGridDivisorMinor);
                    // prevent states from disappearing when GraphGridScalingMajor is 0
                    if (minorGridSpacing < 1)
                        minorGridSpacing = 1;
                    __result = new Rect(Mathf.Round(position.x / minorGridSpacing) * minorGridSpacing, Mathf.Round(position.y / minorGridSpacing) * minorGridSpacing, position.width, position.height);
                    return false;
                }

                return !doDesnap;
            }
        }

        // Node Icons
        [HarmonyPatch]
        [HarmonyPriority(Priority.Low)]
        class PatchAnimatorNodeStyles
        {
            static Color defaultTextColor = new Color(0.922f, 0.922f, 0.922f, 1.0f);

            [HarmonyTargetMethods]
            static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.Styles"), "GetNodeStyle");
            }

            [HarmonyPostfix]
            public static void Postfix(object __instance, ref GUIStyle __result, string styleName, int color, bool on)
            {
                string styleCacheKey = GetStyleCacheKey(styleName, color, on);

                // Only modify style if we haven't done so yet
                if(!AnimatorWindowState.handledNodeStyles.Add(styleCacheKey))
                    return;

                if(RATS.Prefs.NodeStyleOverride)
                {
                    __result.normal.textColor = RATS.Prefs.StateTextColor;
                    __result.fontSize = RATS.Prefs.StateLabelFontSize;

                    if(styleName == "node") // Regular state node
                    {
                        switch (color)
                        {
                            case 6:  __result.normal.background = on ? nodeBackgroundImageRedActive : nodeBackgroundImageRed; break;
                            case 5:  __result.normal.background = on ? nodeBackgroundImageOrangeActive : nodeBackgroundImageOrange; break;
                            case 4:  __result.normal.background = on ? nodeBackgroundImageYellowActive : nodeBackgroundImageYellow; break;
                            case 3:  __result.normal.background = on ? nodeBackgroundImageGreenActive : nodeBackgroundImageGreen; break;
                            case 2:  __result.normal.background = on ? nodeBackgroundImageAquaActive : nodeBackgroundImageAqua; break;
                            case 1:  __result.normal.background = on ? nodeBackgroundImageActive : nodeBackgroundImage; break;
                            default: __result.normal.background = on ? nodeBackgroundImageActive : nodeBackgroundImage; break;
                        }
                    }
                    else if(styleName == "node hex") // SubStateMachine node
                    {
                        __result.normal.background = on ? stateMachineBackgroundImageActive : stateMachineBackgroundImage;
                    }
                }
                else
                {
                    __result.normal.background = EditorGUIUtility.Load(styleCacheKey) as Texture2D; 
                    __result.normal.textColor = defaultTextColor;
                    __result.fontSize = 12;
                }
            }

            public static string GetStyleCacheKey(string styleName, int color, bool on)
            {
                string onOff = on ? " on" : "";

                if(styleName == "node hex")
                    return $"node{color} hex{onOff}";
                else if(styleName == "node")
                    return $"node{color}{onOff}";
                else
                    return $"{styleName}{color}{onOff}";
            }
        }

        // Show motion name and extra details on state graph nodes
        static Color lastTextColor = RATS.Prefs.StateTextColor;
        static Color lastOnColor = RATS.Prefs.StateExtraLabelsColorEnabled;
        static Color lastOffColor = RATS.Prefs.StateExtraLabelsColorDisabled;
        [HarmonyPatch]
        [HarmonyPriority(Priority.Low)]
        class PatchAnimatorLabels
        {
            private static GUIStyle StateMotionStyle = null;
            private static GUIStyle StateExtrasStyle = null;
            private static GUIStyle StateExtrasStyleActive = null;
            private static GUIStyle StateExtrasStyleInactive = null;
            private static GUIStyle StateBlendtreeStyle = null;

            [HarmonyTargetMethods]
            static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.StateNode"), "NodeUI");
                yield return AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.StateMachineNode"), "NodeUI");
            }

            [HarmonyPostfix]
            public static void Postfix(object __instance, UnityEditor.Graphs.GraphGUI host)
            {
                // Figure out which node type
                AnimatorState aState = Traverse.Create(__instance).Field("state").GetValue<AnimatorState>();
                bool hasMotion = aState != null;
                AnimatorStateMachine aStateMachine = Traverse.Create(__instance).Field("stateMachine").GetValue<AnimatorStateMachine>();
                bool hasStateMachine = aStateMachine != null;

                // Lazy-init styles because built-in ones not available during static init
                if (StateMotionStyle == null || (lastOnColor != RATS.Prefs.StateExtraLabelsColorEnabled) || (lastOffColor != RATS.Prefs.StateExtraLabelsColorDisabled) || lastTextColor != RATS.Prefs.StateTextColor)
                {
                    StateExtrasStyle = new GUIStyle(EditorStyles.label);
                    StateExtrasStyle.alignment = TextAnchor.UpperRight;
                    StateExtrasStyle.fontStyle = FontStyle.Bold;
                    StateExtrasStyle.normal.textColor = RATS.Prefs.StateTextColor;

                    StateExtrasStyleActive = new GUIStyle(EditorStyles.label);
                    StateExtrasStyleActive.alignment = TextAnchor.UpperRight;
                    StateExtrasStyleActive.fontStyle = FontStyle.Bold;
                    StateExtrasStyleActive.normal.textColor = RATS.Prefs.StateExtraLabelsColorEnabled;

                    StateExtrasStyleInactive = new GUIStyle(EditorStyles.label);
                    StateExtrasStyleInactive.alignment = TextAnchor.UpperRight;
                    StateExtrasStyleInactive.fontStyle = FontStyle.Bold;
                    StateExtrasStyleInactive.normal.textColor = RATS.Prefs.StateExtraLabelsColorDisabled;

                    StateMotionStyle = new GUIStyle(EditorStyles.miniBoldLabel);
                    StateMotionStyle.fontSize = 9;
                    StateMotionStyle.alignment = TextAnchor.LowerCenter;
                    StateMotionStyle.normal.textColor = RATS.Prefs.StateTextColor;

                    StateBlendtreeStyle = new GUIStyle(EditorStyles.label);
                    StateBlendtreeStyle.alignment = TextAnchor.UpperLeft;
                    StateBlendtreeStyle.fontStyle = FontStyle.Bold;
                }

                lastOnColor = RATS.Prefs.StateExtraLabelsColorEnabled;
                lastOffColor = RATS.Prefs.StateExtraLabelsColorDisabled;
                Rect stateRect = GUILayoutUtility.GetLastRect();

                bool debugShowLabels = Event.current.alt;

                // Tags in corner, similar to what layer editor does
                if ((hasMotion || hasStateMachine))
                {
                    bool isWD = false;
                    bool hasblendtree = false;
                    bool hasBehavior = false;
                    bool hasMotionTime = false;
                    bool hasSpeedParam = false;
                    bool isLoopTime = false;
                    bool isEmptyAnim = false;
                    bool isEmptyState = false;

                    int baseSize = 14;

                    int off1 = (debugShowLabels || (RATS.Prefs.StateExtraLabelsWD && RATS.Prefs.StateExtraLabelsBehavior)) ? baseSize : 0;
                    int off2 = (debugShowLabels || (RATS.Prefs.StateExtraLabelsMotionTime && RATS.Prefs.StateExtraLabelsSpeed)) ? baseSize : 0;

                    Rect wdLabelRect 			= new Rect(stateRect.x - off1, stateRect.y - baseSize * 2, stateRect.width, baseSize);
                    Rect behaviorLabelRect 		= new Rect(stateRect.x, 	   stateRect.y - baseSize * 2, stateRect.width, baseSize);

                    Rect motionTimeLabelRect 	= new Rect(stateRect.x, 	   stateRect.y - baseSize, stateRect.width, baseSize);
                    Rect speedLabelRect 		= new Rect(stateRect.x - off2, stateRect.y - baseSize, stateRect.width, baseSize);

                    if (hasMotion) // Animation/Blendtree
                    {
                        isWD = aState.writeDefaultValues;
                        hasMotionTime = aState.timeParameterActive;
                        hasSpeedParam = aState.speedParameterActive;
                        
                        if(aState.motion != null) 
                        {
                            hasblendtree = aState.motion.GetType() == typeof(BlendTree);
                            if(!hasblendtree)
                            {
                                isLoopTime = ((AnimationClip)aState.motion).isLooping;
                                isEmptyAnim = ((AnimationClip)aState.motion).empty;
                            }
                        }
                        else
                        {
                            isEmptyState = true;
                        }

                        if(aState.behaviours != null) hasBehavior = aState.behaviours.Length > 0;
                    }
                    else if(hasStateMachine) // SubStateMachine
                    {
                        // Move behavior label to fit SSM
                        behaviorLabelRect = new Rect(stateRect.x, stateRect.y - 20, stateRect.width, 15);
                        if (aStateMachine.behaviours != null) hasBehavior = aStateMachine.behaviours.Length > 0;
                    }

                    float iconOffset = 0;

                    // Loop time label
                    if(isLoopTime && (debugShowLabels || RATS.Prefs.StateLoopedLabels))
                    {
                        float loopTimeIconSize = 16f * RATS.Prefs.StateGraphIconScale;
                        Rect loopingLabelRect = new Rect(stateRect.x + 1, stateRect.y - 29, loopTimeIconSize, loopTimeIconSize);
                        EditorGUI.LabelField(loopingLabelRect, new GUIContent(EditorGUIUtility.IconContent("d_preAudioLoopOff@2x").image, "Animation Clip is Looping"));
                        iconOffset += 14f * RATS.Prefs.StateGraphIconScale;
                    }
                    
                    // Empty Animation/State Warning, top left (option)
                    if(RATS.Prefs.ShowWarningsTopLeft)
                    {
                        float warningsIconSize = 14f * RATS.Prefs.StateGraphIconScale;
                        Rect emptyWarningRect = new Rect(stateRect.x + iconOffset + 1, stateRect.y - 28, warningsIconSize, warningsIconSize);
                        if((debugShowLabels || RATS.Prefs.StateAnimIsEmptyLabel))
                        {
                            if(isEmptyAnim) EditorGUI.LabelField(emptyWarningRect, new GUIContent(EditorGUIUtility.IconContent("Warning@2x").image, "Animation Clip has no Keyframes"));
                            else if(isEmptyState) EditorGUI.LabelField(emptyWarningRect, new GUIContent(EditorGUIUtility.IconContent("Error@2x").image, "State has no Motion assigned"));
                        }
                    }

                    #if !RATS_NO_ANIMATOR
                        if(!hasStateMachine && (debugShowLabels || RATS.Prefs.StateExtraLabelsWD)) EditorGUI.LabelField(wdLabelRect, "WD", (isWD ? StateExtrasStyleActive : StateExtrasStyleInactive));
                        if(				(debugShowLabels || RATS.Prefs.StateExtraLabelsBehavior)) EditorGUI.LabelField(behaviorLabelRect, "B", (hasBehavior ? StateExtrasStyleActive : StateExtrasStyleInactive));
                        if(hasMotion && (debugShowLabels || RATS.Prefs.StateExtraLabelsMotionTime)) EditorGUI.LabelField(motionTimeLabelRect, "M", (hasMotionTime ? StateExtrasStyleActive : StateExtrasStyleInactive));
                        if(hasMotion && (debugShowLabels || RATS.Prefs.StateExtraLabelsSpeed)) EditorGUI.LabelField(speedLabelRect, "S", (hasSpeedParam ? StateExtrasStyleActive : StateExtrasStyleInactive));

                        if (hasMotion && (debugShowLabels || RATS.Prefs.StateMotionLabels))
                        {
                            // use the value of aState.motion.name if it's is not null, otherwise use a different value
                            string motionName = aState?.motion?.name ?? "[none]";

                            float iconSize = 14f * RATS.Prefs.StateGraphIconScale;

                            GUIContent labelIconContent = new GUIContent();
                            if(hasblendtree)
                            {
                                labelIconContent.image = EditorGUIUtility.IconContent("d_BlendTree Icon").image;
                                labelIconContent.tooltip = "State contains a Blendtree";
                            }
                            else if(isEmptyAnim && !RATS.Prefs.ShowWarningsTopLeft)
                            {
                                labelIconContent.image = EditorGUIUtility.IconContent("Warning@2x").image;
                                labelIconContent.tooltip = "Animation Clip has no Keyframes";
                            }
                            else if(isEmptyState && !RATS.Prefs.ShowWarningsTopLeft)
                            {
                                labelIconContent.image = EditorGUIUtility.IconContent("Error@2x").image;
                                labelIconContent.tooltip = "State has no Motion assigned";
                            }
                            else 
                            {
                                string animationClipIconName = RATS.Prefs.StateColoredAnimIcon ? "d_AnimationClip Icon" : "AnimationClip On Icon";
                                labelIconContent.image = EditorGUIUtility.IconContent(animationClipIconName).image;
                                labelIconContent.tooltip = "State contains an Animation Clip";
                            }

                            GUIContent motionLabel = new GUIContent(motionName);
                            Vector2 motionLabelSize = StateMotionStyle.CalcSize(motionLabel);

                            Rect motionLabelRect = new Rect(stateRect.x + stateRect.width/2 - motionLabelSize.x/2, stateRect.y - motionLabelSize.y/2, motionLabelSize.x, motionLabelSize.y);
                            motionLabelRect.x = Mathf.Clamp(motionLabelRect.x, iconSize, stateRect.width/2);

                            Vector2 motionIconOffset = new Vector2(0f, 1f + motionLabelRect.y - iconSize/2f + motionLabelRect.height/2f);

                            if(RATS.Prefs.StateGraphIconLocationIsCorner)
                                motionIconOffset.x += -1f + iconSize/8f;
                            else
                                motionIconOffset.x += -7f + (motionLabelRect.x - iconSize/2f);

                            Rect motionIconRect = new Rect(motionIconOffset.x, motionIconOffset.y, iconSize, iconSize);

                            EditorGUI.LabelField(motionLabelRect, motionLabel, StateMotionStyle);
                            EditorGUI.LabelField(motionIconRect, labelIconContent);
                        }
                    #endif //!RATS_NO_ANIMATOR
                }
            }
        }

        #endregion GraphVisuals
    }
}
#endif // UNITY_EDITOR && RATS_HARMONY