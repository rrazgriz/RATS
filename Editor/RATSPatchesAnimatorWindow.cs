// RATS - Raz's Animator Tweaks'n Stuff
// Original AnimatorExtensions by Dj Lukis.LT, under MIT License

// Copyright (c) 2023 Razgriz
// SPDX-License-Identifier: MIT

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using ReorderableList = UnityEditorInternal.ReorderableList;
using HarmonyLib;

namespace Razgriz.RATS
{
    public partial class RATS
    {
#if !RATS_NO_ANIMATOR // CEditor Compatibility

        #region BugFixes
        // Prevent scroll position reset when rearranging or editing layers
        private static Vector2 _layerScrollCache;
        [HarmonyPatch]
        class PatchLayerScrollReset
        {
            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => AccessTools.Method(LayerControllerViewType,	"ResetUI");

            [HarmonyPrefix]
            static void Prefix(object __instance)
            {
                _layerScrollCache = (Vector2)LayerScrollField.GetValue(__instance);
            }

            [HarmonyPostfix]
            static void Postfix(object __instance)
            {
                Vector2 scrollpos = (Vector2)LayerScrollField.GetValue(__instance);
                if (scrollpos.y == 0)
                    LayerScrollField.SetValue(__instance, _layerScrollCache);
                _refocusSelectedLayer = true; // Defer focusing to OnGUI to get latest list size and window rect
            }
        }

        // Scroll to parameter list bottom when adding a new one to see the rename field
        [HarmonyPatch]
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

        // Break 'undo' of sub-state machine pasting
        [HarmonyPatch]
        class PatchBreakUndoSubStateMachinePaste
        {
            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => typeof(Unsupported).GetMethod("PasteToStateMachineFromPasteboard", BindingFlags.Static | BindingFlags.Public);//AccessTools.Method(typeof(Unsupported), "PasteToStateMachineFromPasteboard");

            [HarmonyPostfix]
            static void Postfix(
                AnimatorStateMachine sm,
                AnimatorController controller,
                int layerIndex,
                Vector3 position)
            {
                Undo.ClearUndo(sm);
            }
        }

        // Prevent transition condition parameter change from altering the condition function
        private static int ConditionIndex;
        private static int ConditionMode_pre;
        private static string ConditionParam_pre;
        private static AnimatorControllerParameterType ConditionParamType_pre;
        [HarmonyPatch]
        class PatchTransitionConditionChangeBreakingCondition
        {
            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => AccessTools.Method(AnimatorTransitionInspectorBaseType, "DrawConditionsElement");

            [HarmonyPrefix]
            static void Prefix(object __instance, Rect rect, int index, bool selected, bool focused)
            {
                ConditionIndex = index;
                SerializedProperty conditions = (SerializedProperty)Traverse.Create(__instance).Field("m_Conditions").GetValue();
                SerializedProperty arrayElementAtIndex = conditions.GetArrayElementAtIndex(index);
                ConditionMode_pre = arrayElementAtIndex.FindPropertyRelative("m_ConditionMode").intValue;
                ConditionParam_pre = arrayElementAtIndex.FindPropertyRelative("m_ConditionEvent").stringValue;

                AnimatorController ctrl = Traverse.Create(__instance).Field("m_Controller").GetValue() as AnimatorController;
                if (ctrl)
                {
                    // Unity, why make IndexOfParameter(name) internal -_-
                    foreach (var param in ctrl.parameters)
                    {
                        if (param.name.Equals(ConditionParam_pre))
                        {
                            ConditionParamType_pre = param.type;
                            break;
                        }
                    }
                }
            }

            [HarmonyPostfix]
            static void Postfix(object __instance, Rect rect, int index, bool selected, bool focused)
            {
                if (ConditionIndex == index)
                {
                    SerializedProperty conditions = (SerializedProperty)Traverse.Create(__instance).Field("m_Conditions").GetValue();
                    SerializedProperty arrayElementAtIndex = conditions.GetArrayElementAtIndex(index);
                    SerializedProperty m_ConditionMode = arrayElementAtIndex.FindPropertyRelative("m_ConditionMode");
                    string conditionparam_post = arrayElementAtIndex.FindPropertyRelative("m_ConditionEvent").stringValue;

                    if (!conditionparam_post.Equals(ConditionParam_pre) && (m_ConditionMode.intValue != ConditionMode_pre))
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
                                    if ((param.type == ConditionParamType_pre)
                                        || ((ConditionParamType_pre == AnimatorControllerParameterType.Float) && (param.type == AnimatorControllerParameterType.Int)))
                                    {
                                        m_ConditionMode.intValue = ConditionMode_pre;
                                        Debug.Log("RATS: Restored transition condition mode");
                                    }
                                    // int->float has restrictions
                                    else if ((ConditionParamType_pre == AnimatorControllerParameterType.Int) && (param.type == AnimatorControllerParameterType.Float))
                                    {
                                        AnimatorConditionMode premode = (AnimatorConditionMode)ConditionMode_pre;
                                        if ((premode != AnimatorConditionMode.Equals) && (premode != AnimatorConditionMode.NotEqual))
                                        {
                                            m_ConditionMode.intValue = ConditionMode_pre;
                                            Debug.Log("RATS: Restored transition condition mode 2");
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
            static MethodBase TargetMethod() => AccessTools.Method(AnimatorControllerType, "AddLayer", new Type[] {typeof(AnimatorControllerLayer)});

            [HarmonyPrefix]
            static void Prefix(ref AnimatorControllerLayer layer)
            {
                layer.defaultWeight = RATS.Prefs.DefaultLayerWeight1 ? 1.0f : 0.0f;
            }
        }

        // Layer copy-pasting
        [HarmonyPatch]
        class PatchLayerCopyPaste
        {
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
                        new GenericMenu.MenuFunction2(RATS.CopyLayer), __instance);
                    if (_layerClipboard != null)
                    {
                        menu.AddItem(EditorGUIUtility.TrTextContent("Paste layer", null, (Texture) null), false,
                            new GenericMenu.MenuFunction2(RATS.PasteLayer), __instance);
                        menu.AddItem(EditorGUIUtility.TrTextContent("Paste layer settings", null, (Texture) null), false,
                            new GenericMenu.MenuFunction2(RATS.PasteLayerSettings), __instance);
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
                                CopyLayer(__instance);
                            }
                            else if (current.commandName == "Paste")
                            {
                                current.Use();
                                PasteLayer(__instance);
                            }
                            else if (current.commandName == "Duplicate")
                            {
                                current.Use();
                                CopyLayer(__instance);
                                PasteLayer(__instance);
                                // todo: dupe without polluting clipboard
                            }
                            break;

                        case EventType.KeyDown:
                        {
                            KeyCode keyCode = Event.current.keyCode;
                            if (keyCode == KeyCode.F2) // Rename
                            {
                                current.Use();
                                _refocusSelectedLayer = true;
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
                if (_refocusSelectedLayer)
                {
                    _refocusSelectedLayer = false;
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

        #endregion LayerFeatures

        #region GraphFeatures
        // Set Default Transition Duration/Exit Time
        [HarmonyPatch]
        class PatchAnimatorNewTransitionDefaults
        {
            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => AccessTools.Method(AnimatorStateType, "CreateTransition");

            [HarmonyPostfix]
            static void Postfix(ref AnimatorStateTransition __result)
            {
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
        class PatchAnimatorNewStateDefaults
        {
            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => AccessTools.Method(AnimatorStateMachineType, "AddState", new Type[] {typeof(AnimatorState), typeof(Vector3)});

            [HarmonyPrefix]
            static void Prefix(ref AnimatorState state, Vector3 position)
            {
                if(!RATS.Prefs.DefaultStateWriteDefaults) state.writeDefaultValues = false;
            }
        }

        // Controller asset pinging/selection via bottom bar
        [HarmonyPatch]
        class PatchAnimatorBottomBar
        {
            static GUIStyle buttonStyle = EditorStyles.miniBoldLabel;

            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => AccessTools.Method(AnimatorWindowType, "DoGraphBottomBar");

            [HarmonyPostfix]
            static void Postfix(object __instance, Rect nameRect)
            {
                UnityEngine.Object ctrl = (UnityEngine.Object)AnimatorControllerGetter.Invoke(__instance, null);
                if (ctrl != (UnityEngine.Object)null)
                {
                    GUIContent RATSLabel = new GUIContent($"  RATS", (Texture)RATSGUI.GetRATSIcon());
                    GUIContent ControllerLabel = new GUIContent($"  {AssetDatabase.GetAssetPath(ctrl)}", EditorGUIUtility.IconContent("AnimatorController On Icon").image);
                    float RATSLabelWidth = (buttonStyle).CalcSize(RATSLabel).x;
                    float controllerNameWidth = (EditorStyles.miniLabel).CalcSize(ControllerLabel).x;
                    Rect RATSLabelrect = new Rect(nameRect.x, nameRect.y - 2, RATSLabelWidth, nameRect.height);
                    Rect pingControllerRect = new Rect(nameRect.x + nameRect.width - controllerNameWidth, nameRect.y, controllerNameWidth, nameRect.height);

                    GUILayout.BeginArea(RATSLabelrect);
                    GUILayout.Label(RATSLabel, buttonStyle);
                    GUILayout.EndArea();
                    
                    EditorGUIUtility.AddCursorRect(RATSLabelrect, MouseCursor.Link); // "I'm clickable!"
                    EditorGUIUtility.AddCursorRect(pingControllerRect, MouseCursor.Link); // "I'm clickable!"

                    Event current = Event.current;
                    if ((current.type == EventType.MouseDown) && (current.button == 0))
                    {
                        if(RATSLabelrect.Contains(current.mousePosition))
                        {
                            RATSGUI.ShowWindow();
                        }

                        if(pingControllerRect.Contains(current.mousePosition))
                        {
                            EditorGUIUtility.PingObject(ctrl);
                            if (current.clickCount == 2) // Adhere to the 'select only on double click' convention
                                Selection.activeObject = ctrl;
                            current.Use();
                        }
                    } 
                    
                }
            }
        }

        #endregion GraphFeatures

#endif //RATS_NO_ANIMATOR

        #region GraphVisuals

        // Background
        [HarmonyPatch]
        [HarmonyPriority(Priority.Low)]
        class PatchAnimatorGridBackground
        {
            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => AccessTools.Method(AnimatorWindowGraphGUIType, "DrawGrid");

            [HarmonyPostfix]
            static void Postfix(object __instance, Rect gridRect, float zoomLevel)
            {
                // Overwrite the whole grid drawing lol 
                if(RATS.Prefs.GraphGridOverride)
                {
                    GL.PushMatrix();

                    // Draw Background
                    GL.Begin(GL.QUADS);
                    Color backgroundColor = RATS.Prefs.GraphGridBackgroundColor;
                    backgroundColor.a = 1;
                    GL.Color(backgroundColor);
                    GL.Vertex(new Vector3(gridRect.xMin, gridRect.yMin, 0f));
                    GL.Vertex(new Vector3(gridRect.xMin, gridRect.yMax, 0f));
                    GL.Vertex(new Vector3(gridRect.xMax, gridRect.yMax, 0f));
                    GL.Vertex(new Vector3(gridRect.xMax, gridRect.yMin, 0f));
                    GL.End();

                    // Draw Grid
                    GL.Begin(GL.LINES);
                    float tMajor = Mathf.InverseLerp(0.25f, 1f, zoomLevel);
                    float tMinor = Mathf.InverseLerp(0.0f, 1f, zoomLevel * 0.5f);
                    
                    float gridSize;
                    // Major
                    GL.Color(Color.Lerp(Color.clear, RATS.Prefs.GraphGridColorMajor, tMajor));
                    gridSize = RATS.Prefs.GraphGridScalingMajor * 100f;
                    for (float x = gridRect.xMin - gridRect.xMin % gridSize; x < gridRect.xMax; x += gridSize)
                    {
                        GL.Vertex(new Vector3(x, gridRect.yMin)); GL.Vertex(new Vector3(x, gridRect.yMax));
                    }
                    for (float y = gridRect.yMin - gridRect.yMin % gridSize; y < gridRect.yMax; y += gridSize)
                    {
                        GL.Vertex(new Vector3(gridRect.xMin, y)); GL.Vertex(new Vector3(gridRect.xMax, y));
                    }

                    // Minor
                    GL.Color(Color.Lerp(Color.clear, RATS.Prefs.GraphGridColorMinor, tMinor));
                    gridSize = RATS.Prefs.GraphGridScalingMajor * (100f / RATS.Prefs.GraphGridDivisorMinor);
                    for (float x = gridRect.xMin - gridRect.xMin % gridSize; x < gridRect.xMax; x += gridSize)
                    {
                        GL.Vertex(new Vector3(x, gridRect.yMin)); GL.Vertex(new Vector3(x, gridRect.yMax));
                    }
                    for (float y = gridRect.yMin - gridRect.yMin % gridSize; y < gridRect.yMax; y += gridSize)
                    {
                        GL.Vertex(new Vector3(gridRect.xMin, y)); GL.Vertex(new Vector3(gridRect.xMax, y));
                    }

                    GL.End();
                    GL.PopMatrix();
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
                    __result = new Rect(Mathf.Round(position.x / minorGridSpacing) * minorGridSpacing, Mathf.Round(position.y / minorGridSpacing) * minorGridSpacing, position.width, position.height);
                    return false;
                }

                return !doDesnap;
            }
        }

        static Color defaultTextColor = new Color(0.922f, 0.922f, 0.922f, 1.0f);

        // Node Icons
        [HarmonyPatch]
        [HarmonyPriority(Priority.LowerThanNormal)]
        class PatchAnimatorNodeStyles
        {
            [HarmonyTargetMethods]
            static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.Styles"), "GetNodeStyle");
            }

            [HarmonyPostfix]
            public static void Postfix(object __instance, ref GUIStyle __result, string styleName, int color, bool on)
            {
                string styleHash = GetStyleCacheKey(styleName, color, on);

                if(RATS.Prefs.NodeStyleOverride)
                {
                    bool isPatched = false;
                    bool wasPatchedAtSomePoint = nodeBackgroundPatched.TryGetValue(styleHash, out isPatched);
                    
                    if(true || RATSGUI.updateNodeStyle || !wasPatchedAtSomePoint || !isPatched)
                    {
                        nodeBackgroundPatched[styleHash] = true;

                        if(styleName == "node") // Regular state node
                        {
                            switch(color)
                            {
                                case 6: __result.normal.background = on ? nodeBackgroundImageRedActive : nodeBackgroundImageRed; break; // Red 
                                case 5: __result.normal.background = on ? nodeBackgroundImageOrangeActive : nodeBackgroundImageOrange; break; // Orange
                                case 4: __result.normal.background = on ? nodeBackgroundImageYellowActive : nodeBackgroundImageYellow; break; // Yellow
                                case 3: __result.normal.background = on ? nodeBackgroundImageGreenActive : nodeBackgroundImageGreen; break; // Green
                                case 2: __result.normal.background = on ? nodeBackgroundImageAquaActive : nodeBackgroundImageAqua; break; // Aqua
                                case 1: __result.normal.background = on ? nodeBackgroundImageBlueActive : nodeBackgroundImageBlue; break; // Blue
                                default:__result.normal.background = on ? nodeBackgroundImageActive : nodeBackgroundImage; break; // Anything Else
                            }
                        }
                        else if(styleName == "node hex") // SubStateMachine node
                        {
                            __result.normal.background = on ? stateMachineBackgroundImageActive : stateMachineBackgroundImage;
                        }

                        __result.normal.textColor = RATS.Prefs.StateTextColor;
                        __result.fontSize = RATS.Prefs.StateLabelFontSize;
                    }
                }
                else
                {
                    __result.normal.background = EditorGUIUtility.Load(styleHash) as Texture2D; 
                    __result.normal.textColor = defaultTextColor;
                    __result.fontSize = 12;
                }
            }

            private static Dictionary<string, bool> nodeBackgroundPatched = new Dictionary<string, bool>();

            public static string GetStyleCacheKey(string styleName, int color, bool on)
            {
                if(styleName == "node hex")
                    return String.Format("node{0} hex{1}", color.ToString(), on ? " on" : "");
                else if(styleName == "node")
                    return String.Format("node{0}{1}", color.ToString(), on ? " on" : "");
                else
                    return String.Format("{0}{1}{2}", styleName, color.ToString(), on ? " on" : "");
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

                    int iconOffset = 0;

                    // Loop time label
                    if(isLoopTime && (debugShowLabels || RATS.Prefs.StateLoopedLabels))
                    {
                        Rect loopingLabelRect = new Rect(stateRect.x + 1, stateRect.y - 29, 16, 16);
                        EditorGUI.LabelField(loopingLabelRect, new GUIContent(EditorGUIUtility.IconContent("d_preAudioLoopOff@2x").image, "Animation Clip is Looping"));
                        iconOffset += 14;
                    }
                    
                    // Empty Animation/State Warning, top left (option)
                    if(RATS.Prefs.ShowWarningsTopLeft)
                    {
                        Rect emptyWarningRect = new Rect(stateRect.x + iconOffset + 1, stateRect.y - 28, 14, 14);
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

                            float iconSize = 13f;

                            GUIContent labelIconContent = new GUIContent();
                            if(hasblendtree)
                            {
                                labelIconContent.image = EditorGUIUtility.IconContent("d_BlendTree Icon").image;
                                labelIconContent.tooltip = "State contains a Blendtree";
                                iconSize = 14;
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
                                labelIconContent.image = EditorGUIUtility.IconContent("AnimationClip On Icon").image;
                                labelIconContent.tooltip = "State contains an Animation Clip";
                                iconSize = 16;
                            }

                            GUIContent motionLabel = new GUIContent(motionName);
                            float width = EditorStyles.label.CalcSize(motionLabel).x;
                            float height = EditorStyles.label.CalcSize(motionLabel).y;

                            Rect motionLabelRect = new Rect(stateRect.x + stateRect.width/2 - width/2, stateRect.y - height/2, width, height);
                            Rect motionIconRect = new Rect(motionLabelRect.x - iconSize/2 - 0.5f, motionLabelRect.y + height/2 - iconSize/2, iconSize, iconSize);

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
#endif