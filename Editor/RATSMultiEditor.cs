using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
#if RATS_HARMONY
using HarmonyLib;
#endif
using Razgriz.RATS;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Graphs;
using UnityEditorInternal;
using UnityEngine;
#if VRC_SDK_VRCSDK3 && !UDON
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
#endif
using AnimatorController = UnityEditor.Animations.AnimatorController;
using AnimatorControllerParameter = UnityEngine.AnimatorControllerParameter;
using AnimatorControllerParameterType = UnityEngine.AnimatorControllerParameterType;
using Object = UnityEngine.Object;

public class RATSMultiEditor : EditorWindow
{
    private int selectedTab = 0;
#if VRC_SDK_VRCSDK3 && !UDON
    private string[] tabNames = new string[] { "States", "Transitions", "Parameter Drivers" }; // TODO: Animator Tracking Control
#else 
    private string[] tabNames = new string[] { "States", "Transitions"};
#endif
    private static AnimatorController controller;
    private static AnimatorStateMachine stateMachine;
    private static object tool;
    private static object graphGUI;
    
#if RATS_HARMONY
    [HarmonyPatch]
    [HarmonyPriority(Priority.Low)]
    private class GetControllerPatch
    {
        [HarmonyTargetMethod]
        static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.GraphGUI"), "OnGraphGUI");
        
        [HarmonyPrefix]
        static void OnGraphGUI(object __instance)
        {
            if (graphGUI == __instance) return;
            graphGUI = __instance;
            tool = AccessTools
                .Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.GraphGUI"), "get_tool")
                .Invoke(__instance, Array.Empty<object>());
            controller = (AnimatorController)AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimatorControllerTool"), "get_animatorController").Invoke(tool, Array.Empty<object>());
            stateMachine = (AnimatorStateMachine)AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.GraphGUI"), "get_activeStateMachine").Invoke(graphGUI, Array.Empty<object>());
        }
    }
#endif
    
    Object[] selectionCache = Array.Empty<Object>();
    private void Update()
    {
        Object[] oldSelection = selectionCache;
        selectionCache = Selection.objects; 
        if (oldSelection.Length != selectionCache.Length)
        {
            Repaint();
            return;
        }

        for (var i = 0; i < oldSelection.Length; i++)
        {
            if (oldSelection[i] != selectionCache[i])
            {
                Repaint();
                return;
            }
        }
    }

    [MenuItem("Tools/RATS/Multi-Editor")]
    public static void ShowWindow()
    {
        RATSMultiEditor window = EditorWindow.GetWindow<RATSMultiEditor>();
        window.titleContent = new GUIContent("  RATS Multi-Editor", RATSGUI.GetRATSIcon());
    }

    private void OnGUI()
    {
#if !RATS_HARMONY
        controller = EditorGUILayout.ObjectField("Animator Controller", controller, typeof(AnimatorController), false) as AnimatorController;        
#endif
        // Draw the tabs
        selectedTab = GUILayout.Toolbar(selectedTab, tabNames);
        
        // Draw the appropriate content based on selected tab
        switch (selectedTab)
        {
            case 0:
                DrawStatesTab();
                break;
            case 1:
                DrawTransitionsTab();
                break;
#if VRC_SDK_VRCSDK3 && !UDON
            case 2:
                DrawParameterDriversTab();
                break;
#endif
        }
    }
    
    private void DrawStatesTab()
    {
        List<AnimatorState> states = Selection.objects.Where(x => x != null && x is AnimatorState).Cast<AnimatorState>().ToList();
        
        if (states.Count == 0)
        {
            GUILayout.Label("Please select one or more states to start editing.", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        if (controller == null) return;
        
        AnimatorControllerParameter[] parameters = controller.parameters;
        string[] paramNames = parameters.Where(p => p.type == AnimatorControllerParameterType.Float).Select(p => p.name).ToArray();
        
        // Motion field
        using (new GUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Motion", GUILayout.Width(350f));
            bool sharedMotion = states.All(x => x.motion == states[0].motion);
            Motion currentMotion = sharedMotion ? states[0].motion : null;
            Motion newMotion = EditorGUILayout.ObjectField(currentMotion, typeof(Motion), false) as Motion;
            if (newMotion != currentMotion)
            {
                states.ForEach(x =>
                {
                    Undo.RegisterCompleteObjectUndo(x, "Modify States");
                    x.motion = newMotion;
                });
            }
        }
        
        // Speed field
        using (new GUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Speed", GUILayout.Width(350f));
            bool sharedSpeed = states.All(x => x.speed == states[0].speed);
            float currentSpeed = sharedSpeed ? states[0].speed : 0;
            if (sharedSpeed)
            {
                float newSpeed = EditorGUILayout.FloatField(currentSpeed);
                if (newSpeed != currentSpeed)
                {
                    states.ForEach(x =>
                    {
                        Undo.RegisterCompleteObjectUndo(x, "Modify States");
                        x.speed = newSpeed;
                    });
                }
            }
            else
            {
                string newSpeedString = EditorGUILayout.TextField("--");
                if (float.TryParse(newSpeedString, out float newSpeedFloat))
                {
                    states.ForEach(x =>
                    {
                        Undo.RegisterCompleteObjectUndo(x, "Modify States");
                        x.speed = newSpeedFloat;
                    });
                }
            }
        }

        // Multiplier field
        using (new GUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("   Multiplier", GUILayout.Width(350f));
            bool sharedMultiplier = states.All(x => x.speedParameter == states[0].speedParameter);
            string currentMultiplier = sharedMultiplier ? states[0].speedParameter : "--";
            bool sharedMultiplierEnabled = states.All(x => x.speedParameterActive == states[0].speedParameterActive);
            bool currentMultiplierEnabled = sharedMultiplierEnabled ? states[0].speedParameterActive : false;
            using (new EditorGUI.DisabledScope(!(sharedMultiplierEnabled && currentMultiplierEnabled)))
            {
                int currentMultiplierIndex = Array.FindIndex(paramNames, p => p == currentMultiplier);
                int newMultiplierIndex = EditorGUILayout.Popup(currentMultiplierIndex, paramNames);
                if (newMultiplierIndex != currentMultiplierIndex)
                {
                    states.ForEach(x =>
                    {
                        Undo.RegisterCompleteObjectUndo(x, "Modify States");
                        x.speedParameter = paramNames[newMultiplierIndex];
                    });
                }
            }
            bool newMultiplierEnabled = EditorGUILayout.Toggle(states[0].speedParameterActive, GUILayout.Width(12f));
            EditorGUILayout.LabelField("Parameter", GUILayout.Width(70));
            if (sharedMultiplierEnabled && newMultiplierEnabled != currentMultiplierEnabled)
            {
                states.ForEach(x =>
                {
                    Undo.RegisterCompleteObjectUndo(x, "Modify States");
                    x.speedParameterActive = newMultiplierEnabled;
                });
            }
        }


        // Motion Time field
        using (new GUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Motion Time", GUILayout.Width(350f));
            bool sharedMotionTime = states.All(x => x.timeParameter == states[0].timeParameter);
            string currentMotionTime = sharedMotionTime ? states[0].timeParameter : "--";
            bool sharedMotionTimeEnabled = states.All(x => x.timeParameterActive == states[0].timeParameterActive);
            bool currentMotionTimeEnabled = sharedMotionTimeEnabled ? states[0].timeParameterActive : false;
            using (new EditorGUI.DisabledScope(!(sharedMotionTimeEnabled && currentMotionTimeEnabled)))
            {
                int currentMotionTimeIndex = Array.FindIndex(paramNames, p => p == currentMotionTime);
                int newMotionTimeIndex = EditorGUILayout.Popup(currentMotionTimeIndex, paramNames);
                if (newMotionTimeIndex != currentMotionTimeIndex)
                {
                    states.ForEach(x =>
                    {
                        Undo.RegisterCompleteObjectUndo(x, "Modify States");
                        x.timeParameter = paramNames[newMotionTimeIndex];
                    });
                }
            }
            bool newMultiplierEnabled = EditorGUILayout.Toggle(states[0].timeParameterActive, GUILayout.Width(12f));
            EditorGUILayout.LabelField("Parameter", GUILayout.Width(70));
            if (newMultiplierEnabled != currentMotionTimeEnabled)
            {
                states.ForEach(x =>
                {
                    Undo.RegisterCompleteObjectUndo(x, "Modify States");
                    x.timeParameterActive = newMultiplierEnabled;
                });
            }
        }

        // Mirror field
        using (new GUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Mirror", GUILayout.Width(350f));
            bool sharedMirrorParameterEnabled = states.All(x => x.mirrorParameterActive == states[0].mirrorParameterActive);
            bool currentMirrorParameterEnabled = sharedMirrorParameterEnabled ? states[0].mirrorParameterActive : false;
            if (sharedMirrorParameterEnabled && currentMirrorParameterEnabled)
            {
                bool sharedMirrorParameter = states.All(x => x.mirrorParameter == states[0].mirrorParameter);
                string currentMirrorParameter = sharedMirrorParameter ? states[0].mirrorParameter : "--";
                
                int currentMirrorIndex = Array.FindIndex(paramNames, p => p == currentMirrorParameter);
                int newMirrorIndex = EditorGUILayout.Popup(currentMirrorIndex, paramNames);
                if (newMirrorIndex != currentMirrorIndex)
                {
                    states.ForEach(x =>
                    {
                        Undo.RegisterCompleteObjectUndo(x, "Modify States");
                        x.mirrorParameter = paramNames[newMirrorIndex];
                    });
                }
            }
            else
            {
                bool sharedMirrorEnabled = states.All(x => x.mirror == states[0].mirror);
                bool currentMirrorEnabled = sharedMirrorEnabled ? states[0].mirror : false;
                
                bool newMirror = EditorGUILayout.Toggle(currentMirrorEnabled);
                if (newMirror != currentMirrorEnabled)
                {
                    states.ForEach(x =>
                    {
                        Undo.RegisterCompleteObjectUndo(x, "Modify States");
                        x.mirror = newMirror;
                    });
                }
            }
            bool newMirrorEnabled = EditorGUILayout.Toggle(states[0].mirrorParameterActive, GUILayout.Width(12f));
            EditorGUILayout.LabelField("Parameter", GUILayout.Width(70));
            if (newMirrorEnabled != currentMirrorParameterEnabled)
            {
                states.ForEach(x =>
                {
                    Undo.RegisterCompleteObjectUndo(x, "Modify States");
                    x.mirrorParameterActive = newMirrorEnabled;
                });
            }
        }
      
        // Cycle Offset field
        using (new GUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Cycle Offset", GUILayout.Width(350f));
            bool sharedCycleOffsetParameterEnabled = states.All(x => x.cycleOffsetParameterActive == states[0].cycleOffsetParameterActive);
            bool currentCycleOffsetParameterEnabled = sharedCycleOffsetParameterEnabled ? states[0].cycleOffsetParameterActive : false;
            if (sharedCycleOffsetParameterEnabled && currentCycleOffsetParameterEnabled)
            {
                bool sharedCycleOffsetParameter = states.All(x => x.cycleOffsetParameter == states[0].cycleOffsetParameter);
                string currentCycleOffsetParameter = sharedCycleOffsetParameter ? states[0].cycleOffsetParameter : "--";
                
                int currentCycleOffsetIndex = Array.FindIndex(paramNames, p => p == currentCycleOffsetParameter);
                int newCycleOffsetIndex = EditorGUILayout.Popup(currentCycleOffsetIndex, paramNames);
                if (newCycleOffsetIndex != currentCycleOffsetIndex)
                {
                    states.ForEach(x =>
                    {
                        Undo.RegisterCompleteObjectUndo(x, "Modify States");
                        x.cycleOffsetParameter = paramNames[newCycleOffsetIndex];
                    });
                }
            }
            else
            {
                bool sharedCycleOffset = states.All(x => x.cycleOffset == states[0].cycleOffset);
                float currentCycleOffset = sharedCycleOffset ? states[0].cycleOffset : 0;
                
                if (sharedCycleOffset)
                {
                    float newCycleOffset = EditorGUILayout.FloatField(currentCycleOffset);
                    if (newCycleOffset != currentCycleOffset)
                    {
                        states.ForEach(x =>
                        {
                            Undo.RegisterCompleteObjectUndo(x, "Modify States");
                            x.cycleOffset = newCycleOffset;
                        });
                    }
                }
                else
                {
                    string newCycleOffsetString = EditorGUILayout.TextField("--");
                    if (float.TryParse(newCycleOffsetString, out float newCycleOffset))
                    {
                        states.ForEach(x =>
                        {
                            Undo.RegisterCompleteObjectUndo(x, "Modify States");
                            x.cycleOffset = newCycleOffset;
                        });
                    }
                }
            }
            bool newCycleOffsetEnabled = EditorGUILayout.Toggle(states[0].cycleOffsetParameterActive, GUILayout.Width(12f));
            EditorGUILayout.LabelField("Parameter", GUILayout.Width(70));
            if (newCycleOffsetEnabled != currentCycleOffsetParameterEnabled)
            {
                states.ForEach(x =>
                {
                    Undo.RegisterCompleteObjectUndo(x, "Modify States");
                    x.cycleOffsetParameterActive = newCycleOffsetEnabled;
                });
            }
        }

        // Write Defaults toggle
        DrawBoolField(states,
            state => state.writeDefaultValues,
            (state, value) => state.writeDefaultValues = value,
            "Write Defaults");
    }

    struct SourceCondition
    {
        public AnimatorCondition condition;
        public AnimatorStateTransition transition;
        public int index;
    }
    
    struct TargetCondition
    {
        public AnimatorCondition condition;
        public List<(AnimatorStateTransition, int)> references;
    }

    private ReorderableList conditions;
    private void DrawTransitionsTab()
    {
        List<AnimatorStateTransition> transitions = Selection.objects.Where(x => x != null && x is AnimatorStateTransition).Cast<AnimatorStateTransition>().ToList();
        
        if (transitions.Count == 0)
        {
            GUILayout.Label("Please select one or more transitions to start editing.", EditorStyles.centeredGreyMiniLabel);
            return;
        }
        
        // Has Exit Time
        DrawBoolField(transitions, 
            transition => transition.hasExitTime,
            (transition, value) => transition.hasExitTime = value, 
            "Has Exit Time");
        
        // Exit Time
        DrawFloatField(transitions,
            transition => transition.exitTime,
            (transition, value) => transition.exitTime = value,
            "Exit Time");
        
        // Fixed Duration
        DrawBoolField(transitions,
            transition => transition.hasFixedDuration,
            (transition, value) => transition.hasFixedDuration = value,
            "Fixed Duration");
        
        // Transition Duration
        DrawFloatField(transitions,
            transition => transition.duration,
            (transition, value) => transition.duration = value,
            "Transition Duration");
        
        // Transition Offset
        DrawFloatField(transitions,
            transition => transition.offset,
            (transition, value) => transition.offset = value,
            "Transition Offset");
        
        // Interruption Source
        using (new GUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Interruption Source", GUILayout.Width(350f));
            bool sharedValue = transitions.All(x => x.interruptionSource == transitions[0].interruptionSource);
            TransitionInterruptionSource currentValue = sharedValue ? transitions[0].interruptionSource : TransitionInterruptionSource.None;
            TransitionInterruptionSource newValue = (TransitionInterruptionSource) EditorGUILayout.EnumPopup(currentValue);
            if (newValue != currentValue)
            {
                transitions.ForEach(x =>
                {
                    Undo.RegisterCompleteObjectUndo(x, "Modify Transition");
                    x.interruptionSource = newValue;
                });
            }
        }

        if (transitions.Any(x => stateMachine.anyStateTransitions.Contains(x)) ||
            RATS.Prefs.EditNonAnyStateSelfTransition)
        {
            var selfTransitionConditions = RATS.Prefs.EditNonAnyStateSelfTransition
                ? transitions
                : transitions.Where(x => stateMachine.anyStateTransitions.Contains(x)).ToList();
            DrawBoolField(selfTransitionConditions,
                transition => transition.canTransitionToSelf,
                (transition, value) => transition.canTransitionToSelf = value,
                "Can Transition To Self");
        }
        
        
        List<TargetCondition> sharedConditions = new List<TargetCondition>();
        
        var conditionLists = transitions.Select(t => (
            t.conditions.ToList().Zip(Enumerable.Range(0, t.conditions.Length), (c, i) => new SourceCondition
            {
                condition = c,
                transition = t,
                index = i
            }).ToList())).ToList();
        
        void ExtractConditions(List<List<SourceCondition>> conditionListList, List<TargetCondition> result, 
            Func<AnimatorCondition, AnimatorCondition, bool> matchFunc,
            Func<AnimatorCondition, AnimatorCondition> transformFunc)
        {
            List<SourceCondition> sourceCopy = conditionListList.First().ToList();
            for (var i = 0; i < sourceCopy.Count; i++)
            {
                var sourceCondition = sourceCopy[i];
                var checkCondition = sourceCondition.condition;
                bool existsInAll = conditionListList.All(transition =>
                    transition.Any(c => matchFunc(c.condition, checkCondition)));

                if (!existsInAll) continue;

                // To clear up, it adds the condition, and a list of pairs from transition to the index in said transition, to know what to edit
                result.Add(new TargetCondition
                {
                    condition = transformFunc(checkCondition),
                    references = conditionListList.Select(conditions =>
                            {
                                var sourceCondition = conditions.First(c => matchFunc(checkCondition, c.condition));
                                return (sourceCondition.transition, sourceCondition.index);
                            }).ToList()
                });

                // Remove from all transitions (remove all that match all three properties)
                foreach (var conditionList in conditionListList)
                {
                    SourceCondition remove = conditionList.First(c => matchFunc(c.condition, checkCondition));
                    conditionList.Remove(remove);
                }
            }
        }

        // Check in order of specificity
        ExtractConditions(conditionLists, sharedConditions, 
            (c1, c2) => c1.parameter == c2.parameter && c1.mode == c2.mode && c1.threshold == c2.threshold,
            c => new AnimatorCondition()
            {
                parameter = c.parameter,
                mode = c.mode,
                threshold = c.threshold
            });

        ExtractConditions(conditionLists, sharedConditions,
            (c1, c2) => c1.parameter == c2.parameter && c1.mode == c2.mode,
            c => new AnimatorCondition()
            {
                parameter = c.parameter,
                mode = c.mode,
            });
        
        ExtractConditions(conditionLists, sharedConditions,
            (c1, c2) => c1.parameter == c2.parameter && c1.threshold == c2.threshold,
            c => new AnimatorCondition()
            {
                parameter = c.parameter,
                threshold = c.threshold
            });

        ExtractConditions(conditionLists, sharedConditions,
            (c1, c2) => c1.parameter == c2.parameter,
            c => new AnimatorCondition()
            {
                parameter = c.parameter
            });

        
        conditions ??= new ReorderableList(sharedConditions, typeof(AnimatorCondition), false, true, true, true);
        conditions.list = sharedConditions;
        conditions.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
        {
            if (controller == null) return;
            
            AnimatorCondition condition = sharedConditions[index].condition;
            Rect paramRect = new Rect(rect.x, rect.y + 2, rect.width * 0.4f, EditorGUIUtility.singleLineHeight);
            Rect modeRect = new Rect(rect.x + rect.width * 0.42f, rect.y + 2, rect.width * 0.3f, EditorGUIUtility.singleLineHeight);
            Rect valueRect = new Rect(rect.x + rect.width * 0.74f, rect.y + 2, rect.width * 0.26f, EditorGUIUtility.singleLineHeight);
            
            Rect doubleRect = new Rect(rect.x + rect.width * 0.42f, rect.y + 2, rect.width * 0.58f, EditorGUIUtility.singleLineHeight);

            // Get available parameters from the animator
            AnimatorControllerParameter[] parameters = controller.parameters;
            string[] paramNames = parameters.Select(p => p.name).ToArray();

            // Find current parameter index
            int currentParamIndex = Array.FindIndex(parameters, p => p.name == condition.parameter);

            // Parameter dropdown
            int newParamIndex = EditorGUI.Popup(paramRect, currentParamIndex, paramNames);
            if (newParamIndex != currentParamIndex && newParamIndex >= 0)
            {
                var type = parameters.First(x => x.name == paramNames[newParamIndex]).type;
                sharedConditions[index].references.ForEach((x) =>
                {
                    var (transition, index) = x;
                    Undo.RegisterCompleteObjectUndo(transition, "Modify Transition Conditions");
                    var conditions = transition.conditions;
                    conditions[index].parameter = paramNames[newParamIndex];
                    conditions[index].mode = type == AnimatorControllerParameterType.Bool
                        ? AnimatorConditionMode.If
                        : AnimatorConditionMode.Greater;
                    transition.conditions = conditions;
                });
            }

            // Mode dropdown
            switch (parameters[newParamIndex].type)
            {
                case AnimatorControllerParameterType.Bool:
                case AnimatorControllerParameterType.Trigger:
                    string[] boolStrings = { "true", "false" };
                    int currentBoolIndex = condition.mode == AnimatorConditionMode.If ? 0 : 1;
                    var newBoolIndex = EditorGUI.Popup(doubleRect, currentBoolIndex, boolStrings);
                    if (currentBoolIndex != newBoolIndex)
                    {
                        sharedConditions[index].references.ForEach((x) =>
                        {
                            var (transition, index) = x;
                            Undo.RegisterCompleteObjectUndo(transition, "Modify Transition Conditions");
                            var conditions = transition.conditions;
                            conditions[index].mode = new [] {AnimatorConditionMode.If, AnimatorConditionMode.IfNot}[newBoolIndex];
                            transition.conditions = conditions;
                        });
                    }
                    break;
                case AnimatorControllerParameterType.Float:
                    string[] floatStrings = { "Greater", "Less" };
                    int currentFloatIndex = condition.mode == AnimatorConditionMode.Greater ? 0 : 1;
                    var newFloatIndex = EditorGUI.Popup(modeRect, currentFloatIndex, floatStrings);
                    if (currentFloatIndex != newFloatIndex)
                    {
                        sharedConditions[index].references.ForEach((x) =>
                        {
                            var (transition, index) = x;
                            Undo.RegisterCompleteObjectUndo(transition, "Modify Transition Conditions");
                            var conditions = transition.conditions;
                            conditions[index].mode = new [] {AnimatorConditionMode.Greater, AnimatorConditionMode.Less}[newFloatIndex];
                            transition.conditions = conditions;
                        });
                    }
                    break;
                case AnimatorControllerParameterType.Int:
                    string[] intStrings = { "Greater", "Less", "Equals", "NotEqual" };
                    int currentIntIndex = condition.mode == AnimatorConditionMode.Greater ? 0 : 
                            condition.mode == AnimatorConditionMode.Less ? 1 : 
                            condition.mode == AnimatorConditionMode.Equals ? 2 : 3;
                    var newIntIndex = EditorGUI.Popup(modeRect, currentIntIndex, intStrings);
                    if (currentIntIndex != newIntIndex)
                    {
                        sharedConditions[index].references.ForEach((x) =>
                        {
                            var (transition, index) = x;
                            Undo.RegisterCompleteObjectUndo(transition, "Modify Transition Conditions");
                            var conditions = transition.conditions;
                            conditions[index].mode = new [] {AnimatorConditionMode.Greater, AnimatorConditionMode.Less, AnimatorConditionMode.Equals, AnimatorConditionMode.NotEqual}[newIntIndex];
                            transition.conditions = conditions;
                        });
                    }
                    break;
            }
            
            
            // Threshold field (only show for modes that need it)
            if ((parameters[newParamIndex].type == AnimatorControllerParameterType.Float ||
                parameters[newParamIndex].type == AnimatorControllerParameterType.Int) &&
                condition.mode != AnimatorConditionMode.If && 
                condition.mode != AnimatorConditionMode.IfNot)
            {
                var newThreshold = EditorGUI.FloatField(valueRect, condition.threshold);
                if (newThreshold != condition.threshold)
                {
                    sharedConditions[index].references.ForEach((x) =>
                    { 
                        var (transition, index) = x;
                        Undo.RegisterCompleteObjectUndo(transition, "Modify Transition Conditions");
                        var conditions = transition.conditions;
                        conditions[index].threshold = newThreshold;
                        transition.conditions = conditions;
                    });
                }
            }
        };
        
        conditions.elementHeight = EditorGUIUtility.singleLineHeight + 2;
        conditions.drawHeaderCallback = (Rect rect) => {
            EditorGUI.LabelField(rect, "Conditions");
        };
        
        conditions.onAddCallback = (ReorderableList list) => {
            transitions.ForEach(t =>
            {
                Undo.RegisterCompleteObjectUndo(t, "Modify Transition Conditions");
                if (controller.parameters.Length == 0)
                {
                    t.conditions = t.conditions.Append(new AnimatorCondition()
                    {
                        mode = AnimatorConditionMode.If,
                        parameter = "",
                        threshold = 0f
                    }).ToArray();
                }
                else
                {
                    
                    t.conditions = t.conditions.Append(new AnimatorCondition()
                    {
                        mode = controller.parameters[0].type == AnimatorControllerParameterType.Bool ? AnimatorConditionMode.If : AnimatorConditionMode.Greater,
                        parameter = controller.parameters[0].name,
                        threshold = 0f
                    }).ToArray();
                }

            });
        };

        conditions.onRemoveCallback = (ReorderableList list) =>
        {
            var toRemove = sharedConditions[list.index];
            toRemove.references.ForEach(x =>
            {
                (var transition, var index) = x;
                Undo.RegisterCompleteObjectUndo(transition, "Modify Transition Conditions");
                var conditions = transition.conditions.ToList();
                conditions.RemoveAt(index);
                transition.conditions = conditions.ToArray();
            });
        };
        
        conditions.onChangedCallback = (ReorderableList list) => {
            // Mark any serialized object as dirty if needed
            EditorUtility.SetDirty(controller);
        };

        conditions.DoLayoutList();
    }

#if VRC_SDK_VRCSDK3 && !UDON
    struct SourceParameter
    {
        public VRC_AvatarParameterDriver.Parameter parameter;
        public VRCAvatarParameterDriver driver;
        public int index;
    }
    
    struct TargetParameter
    {
        public VRC_AvatarParameterDriver.Parameter parameter;
        public List<(VRCAvatarParameterDriver, int)> references;
    }

    private ReorderableList parameters;
    
    private void DrawParameterDriversTab()
    {
        List<AnimatorState> states = Selection.objects.Where(x => x != null && x is AnimatorState s && s.behaviours.Any(x => x is VRCAvatarParameterDriver)).Cast<AnimatorState>().ToList();
        
        if (states.Count == 0)
        {
            GUILayout.Label("Please select one or more states with Parameter Drivers to start editing.", EditorStyles.centeredGreyMiniLabel);
            return;
        }
        
        List<TargetParameter> sharedDrivers = new List<TargetParameter>();
        
        var driverList = states.Select(s => (
            s.behaviours.Where(x => x is VRCAvatarParameterDriver)
                .Cast<VRCAvatarParameterDriver>()
                .SelectMany(d => d.parameters.Zip(Enumerable.Range(0, d.parameters.Count), (p, i) => new SourceParameter()
            {
                parameter = p,
                driver = d,
                index = i
            }))).ToList()).ToList();
        
        void ExtractParameters(List<List<SourceParameter>> parameterListList, List<TargetParameter> result, 
            Func<VRC_AvatarParameterDriver.Parameter, VRC_AvatarParameterDriver.Parameter, bool> matchFunc,
            Func<VRC_AvatarParameterDriver.Parameter, VRC_AvatarParameterDriver.Parameter> transformFunc)
        {
            List<SourceParameter> sourceCopy = parameterListList.First().ToList();
            for (var i = 0; i < sourceCopy.Count; i++)
            {
                var sourceParameter = sourceCopy[i];
                var checkParameter = sourceParameter.parameter;
                bool existsInAll = parameterListList.All(state =>
                    state.Any(c => matchFunc(c.parameter, checkParameter)));

                if (!existsInAll) continue;

                // To clear up, it adds the parameter, and a list of pairs from driver to the index in said driver, to know what to edit
                result.Add(new TargetParameter()
                {
                    parameter = transformFunc(checkParameter),
                    references = parameterListList.Select(parameters =>
                            {
                                var sourceCondition = parameters.First(c => matchFunc(checkParameter, c.parameter));
                                return (sourceCondition.driver, sourceCondition.index);
                            }).ToList()
                });

                // Remove from all transitions (remove all that match all three properties)
                foreach (var conditionList in parameterListList)
                {
                    SourceParameter remove = conditionList.First(c => matchFunc(c.parameter, checkParameter));
                    conditionList.Remove(remove);
                }
            }
        }

        // Check in order of specificity
        // Random
        
        ExtractParameters(driverList, sharedDrivers,
            (p1, p2) => p1.type == p2.type && p1.type == VRC_AvatarParameterDriver.ChangeType.Random &&
                        p1.name == p2.name &&
                        ((controller.parameters.FirstOrDefault(x => x.name == p1.name)?
                            .type == AnimatorControllerParameterType.Bool) 
                                ? p1.chance == p2.chance
                                : (p1.valueMin == p2.valueMin && p1.valueMax == p2.valueMax)),
            p => new VRC_AvatarParameterDriver.Parameter()
            {
                type = p.type,
                name = p.name,
                source = p.source,
                value = p.value,
                chance = p.chance,
                valueMin = p.valueMin,
                valueMax = p.valueMax
            });
        
        ExtractParameters(driverList, sharedDrivers,
            (p1, p2) => p1.type == p2.type && p1.type == VRC_AvatarParameterDriver.ChangeType.Random &&
                        p1.name == p2.name,
            p => new VRC_AvatarParameterDriver.Parameter()
            {
                type = p.type,
                name = p.name,
            });
        
        ExtractParameters(driverList, sharedDrivers,
            (p1, p2) => p1.type == p2.type && p1.type == VRC_AvatarParameterDriver.ChangeType.Random,
            p => new VRC_AvatarParameterDriver.Parameter()
            {
                type = p.type,
            });

        // Set
        ExtractParameters(driverList, sharedDrivers,
            (p1, p2) => p1.type == p2.type && p1.type == VRC_AvatarParameterDriver.ChangeType.Set &&
                        p1.name == p2.name  && p1.value == p2.value,
            p => new VRC_AvatarParameterDriver.Parameter()
            {
                type = p.type,
                name = p.name,
                value = p.value
            });
        
        ExtractParameters(driverList, sharedDrivers,
            (p1, p2) => p1.type == p2.type && p1.type == VRC_AvatarParameterDriver.ChangeType.Set &&
                        p1.name == p2.name,
            p => new VRC_AvatarParameterDriver.Parameter()
            {
                type = p.type,
                name = p.name,
            });
        
        ExtractParameters(driverList, sharedDrivers,
            (p1, p2) => p1.type == p2.type && p1.type == VRC_AvatarParameterDriver.ChangeType.Set,
            p => new VRC_AvatarParameterDriver.Parameter()
            {
                type = p.type,
            });
        
        // Add
        ExtractParameters(driverList, sharedDrivers,
            (p1, p2) => p1.type == p2.type && p1.type == VRC_AvatarParameterDriver.ChangeType.Add &&
                        p1.name == p2.name  && p1.value == p2.value,
            p => new VRC_AvatarParameterDriver.Parameter()
            {
                type = p.type,
                name = p.name,
                value = p.value
            });
        
        ExtractParameters(driverList, sharedDrivers,
            (p1, p2) => p1.type == p2.type && p1.type == VRC_AvatarParameterDriver.ChangeType.Add &&
                        p1.name == p2.name,
            p => new VRC_AvatarParameterDriver.Parameter()
            {
                type = p.type,
                name = p.name,
            });
        
        ExtractParameters(driverList, sharedDrivers,
            (p1, p2) => p1.type == p2.type && p1.type == VRC_AvatarParameterDriver.ChangeType.Add,
            p => new VRC_AvatarParameterDriver.Parameter()
            {
                type = p.type,
            });
        
        // Copy
        ExtractParameters(driverList, sharedDrivers,
            (p1, p2) => p1.type == p2.type && p1.type == VRC_AvatarParameterDriver.ChangeType.Copy &&
                        p1.source == p2.source && p1.name == p2.name,
            p => new VRC_AvatarParameterDriver.Parameter()
            {
                type = p.type,
                source = p.source,
                name = p.name
            });
        
        ExtractParameters(driverList, sharedDrivers,
            (p1, p2) => p1.type == p2.type && p1.type == VRC_AvatarParameterDriver.ChangeType.Copy &&
                        p1.source == p2.source,
            p => new VRC_AvatarParameterDriver.Parameter()
            {
                type = p.type,
                source = p.source
            });
        
        ExtractParameters(driverList, sharedDrivers,
            (p1, p2) => p1.type == p2.type && p1.type == VRC_AvatarParameterDriver.ChangeType.Copy,
            p => new VRC_AvatarParameterDriver.Parameter()
            {
                type = p.type,
            });
        
        parameters ??= new ReorderableList(sharedDrivers, typeof(AnimatorCondition), false, true, true, true);
        parameters.list = sharedDrivers;
        parameters.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
        {
            if (controller == null) return;
            
            VRC_AvatarParameterDriver.Parameter parameter = sharedDrivers[index].parameter;
            Rect typeRect = new Rect(rect.x, rect.y + 2, rect.width * 0.4f, EditorGUIUtility.singleLineHeight);
            Rect secondRect = new Rect(rect.x + rect.width * 0.42f, rect.y + 2, rect.width * 0.3f, EditorGUIUtility.singleLineHeight);
            Rect thirdRect = new Rect(rect.x + rect.width * 0.74f, rect.y + 2, rect.width * 0.26f, EditorGUIUtility.singleLineHeight);

            Rect thirdRect1 = new Rect(rect.x + rect.width * 0.74f, rect.y + 2, rect.width * 0.12f, EditorGUIUtility.singleLineHeight);
            Rect thirdRect2 = new Rect(rect.x + rect.width * 0.88f, rect.y + 2, rect.width * 0.12f, EditorGUIUtility.singleLineHeight);

            VRC_AvatarParameterDriver.ChangeType newType = (VRC_AvatarParameterDriver.ChangeType)EditorGUI.EnumPopup(typeRect, parameter.type);
            if (newType != parameter.type)
            {
                sharedDrivers[index].references.ForEach((x) =>
                {
                    var (parameterDriver, index) = x;
                    Undo.RegisterCompleteObjectUndo(parameterDriver, "Modify Parameter Drivers");
                    var parameters = parameterDriver.parameters;
                    parameters[index].type = newType;
                    
                    if (parameters[index].name == null)
                        parameters[index].name = controller.parameters.FirstOrDefault()?.name;
                    
                    if (parameters[index].source == null)
                        parameters[index].source = controller.parameters.FirstOrDefault()?.name;
                    
                    parameterDriver.parameters = parameters;
                });
            }
            
            // Get available parameters from the animator
            AnimatorControllerParameter[] parameters = controller.parameters;
            string[] paramNames = parameters.Select(p => p.name).ToArray();
            
            switch (parameter.type)
            {
                // Set/Add
                case VRC_AvatarParameterDriver.ChangeType.Add:
                case VRC_AvatarParameterDriver.ChangeType.Set:
                    int currentParamIndex = Array.FindIndex(parameters, p => p.name == parameter.name);
                    int newParamIndex = EditorGUI.Popup(secondRect, currentParamIndex, paramNames);
                    if (newParamIndex != currentParamIndex && newParamIndex >= 0)
                    {
                        sharedDrivers[index].references.ForEach((x) =>
                        {
                            var (driver, index) = x;
                            Undo.RegisterCompleteObjectUndo(driver, "Modify Parameter Drivers");
                            var parameters = driver.parameters;
                            parameters[index].name = paramNames[newParamIndex];
                            driver.parameters = parameters;
                        });
                    }

                    if (newParamIndex < 0 || parameters[newParamIndex].type == AnimatorControllerParameterType.Bool ||
                        parameters[newParamIndex].type == AnimatorControllerParameterType.Trigger)
                    {
                        bool newValue = EditorGUI.Toggle(thirdRect, parameter.value == 1.0);
                        if (newValue != (parameter.value == 1.0))
                        {
                            sharedDrivers[index].references.ForEach((x) =>
                            {
                                var (driver, index) = x;
                                Undo.RegisterCompleteObjectUndo(driver, "Modify Parameter Drivers");
                                var parameters = driver.parameters;
                                parameters[index].value = newValue ? 1.0f : 0.0f;
                                driver.parameters = parameters;
                            });
                        }
                    }
                    else
                    {
                        float newValue = EditorGUI.FloatField(thirdRect, parameter.value);
                        if (newValue != parameter.value)
                        {
                            sharedDrivers[index].references.ForEach((x) =>
                            {
                                var (driver, index) = x;
                                Undo.RegisterCompleteObjectUndo(driver, "Modify Parameter Drivers");
                                var parameters = driver.parameters;
                                parameters[index].value = newValue;
                                driver.parameters = parameters;
                            });
                        }   
                    }
                    break;
                
                // Copy
                case VRC_AvatarParameterDriver.ChangeType.Copy:
                    int currentSourceParamIndex = Array.FindIndex(parameters, p => p.name == parameter.source);
                    int newSourceParamIndex = EditorGUI.Popup(secondRect, currentSourceParamIndex, paramNames);
                    if (newSourceParamIndex != currentSourceParamIndex && newSourceParamIndex >= 0)
                    {
                        sharedDrivers[index].references.ForEach((x) =>
                        {
                            var (driver, index) = x;
                            var parameters = driver.parameters;
                            Undo.RegisterCompleteObjectUndo(driver, "Modify Parameter Drivers");
                            parameters[index].source = paramNames[newSourceParamIndex];
                            driver.parameters = parameters;
                        });
                    }
                    
                    int currentDestParamIndex = Array.FindIndex(parameters, p => p.name == parameter.name);
                    int newDestParamIndex = EditorGUI.Popup(thirdRect, currentDestParamIndex, paramNames);
                    if (newDestParamIndex != currentDestParamIndex && newDestParamIndex >= 0)
                    {
                        sharedDrivers[index].references.ForEach((x) =>
                        {
                            var (driver, index) = x;
                            var parameters = driver.parameters;
                            Undo.RegisterCompleteObjectUndo(driver, "Modify Parameter Drivers");
                            parameters[index].name = paramNames[newDestParamIndex];
                            driver.parameters = parameters;
                        });
                    }
                    break;
                
                // Random
                case VRC_AvatarParameterDriver.ChangeType.Random:
                    int currentRandomParamIndex = Array.FindIndex(parameters, p => p.name == parameter.name);
                    int newRandomParamIndex = EditorGUI.Popup(secondRect, currentRandomParamIndex, paramNames);
                    if (newRandomParamIndex != currentRandomParamIndex && newRandomParamIndex >= 0)
                    {
                        sharedDrivers[index].references.ForEach((x) =>
                        {
                            var (driver, index) = x;
                            var parameters = driver.parameters;
                            Undo.RegisterCompleteObjectUndo(driver, "Modify Parameter Drivers");
                            parameters[index].name = paramNames[newRandomParamIndex];
                            driver.parameters = parameters;
                        });
                    }


                    if (newRandomParamIndex <= 0 || parameters[newRandomParamIndex].type == AnimatorControllerParameterType.Bool ||
                        parameters[newRandomParamIndex].type == AnimatorControllerParameterType.Trigger)
                    {
                        float newRandomValue = EditorGUI.FloatField(thirdRect, parameter.chance);
                        if (newRandomValue != parameter.chance)
                        {
                            sharedDrivers[index].references.ForEach((x) =>
                            {
                                var (driver, index) = x;
                                var parameters = driver.parameters;
                                Undo.RegisterCompleteObjectUndo(driver, "Modify Parameter Drivers");
                                parameters[index].chance = newRandomValue;
                                driver.parameters = parameters;
                            });
                        }
                    }
                    else
                    {
                        float newRandomMinValue = EditorGUI.FloatField(thirdRect1, parameter.valueMin);
                        if (newRandomMinValue != parameter.valueMin)
                        {
                            sharedDrivers[index].references.ForEach((x) =>
                            {
                                var (driver, index) = x;
                                var parameters = driver.parameters;
                                Undo.RegisterCompleteObjectUndo(driver, "Modify Parameter Drivers");
                                parameters[index].valueMin = newRandomMinValue;
                                driver.parameters = parameters;
                            });
                        }
                        
                        float newRandomMaxValue = EditorGUI.FloatField(thirdRect2, parameter.valueMax);
                        if (newRandomMaxValue != parameter.valueMax)
                        {
                            sharedDrivers[index].references.ForEach((x) =>
                            {
                                var (driver, index) = x;
                                var parameters = driver.parameters;
                                Undo.RegisterCompleteObjectUndo(driver, "Modify Parameter Drivers");
                                parameters[index].valueMax = newRandomMaxValue;
                                driver.parameters = parameters;
                            });
                        }
                    }
                    break;
            }
        };
        
        parameters.elementHeight = EditorGUIUtility.singleLineHeight + 2;
        parameters.drawHeaderCallback = (Rect rect) => {
            EditorGUI.LabelField(rect, "Parameter Drivers");
        };
        
        parameters.onAddCallback = (ReorderableList list) => {
            states.ForEach(s =>
            {
                var driver = s.behaviours.Where(x => x is VRCAvatarParameterDriver).Cast<VRCAvatarParameterDriver>().Last();
                Undo.RegisterCompleteObjectUndo(driver, "Modify Parameter Drivers");
                driver.parameters = driver.parameters.Append(new VRC_AvatarParameterDriver.Parameter()
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Add,
                    name = controller.parameters.Length == 0 ? "" : controller.parameters[0].name,
                }).ToList();
            });
        };
        
        parameters.onRemoveCallback = (ReorderableList list) =>
        {
            var toRemove = sharedDrivers[list.index];
            toRemove.references.ForEach(x =>
            {
                (var driver, var index) = x;
                Undo.RegisterCompleteObjectUndo(driver, "Modify Parameter Drivers");
                var parameters = driver.parameters.ToList();
                parameters.RemoveAt(index);
                driver.parameters = parameters.ToList();
            });
        };
        
        parameters.onChangedCallback = (ReorderableList list) => {
            // Mark any serialized object as dirty if needed
            EditorUtility.SetDirty(controller);
        };
        
        parameters.DoLayoutList();
    }
#endif
    
    private void DrawFloatField<T>(List<T> objects, Func<T, float> getter, Action<T, float> setter, string name) where T: Object
    {
        using (new GUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(name, GUILayout.Width(350f));
            bool sharedValue = objects.All(x => getter(x) == getter(objects[0]));
            float currentValue = sharedValue ? getter(objects[0]) : 0.0f;
            float newValue = EditorGUILayout.FloatField(currentValue);
            if (newValue != currentValue)
            {
                objects.ForEach(x =>
                {
                    Undo.RegisterCompleteObjectUndo(x, "Modify Controller");
                    setter(x, newValue);
                });
            }
        }
    }

    private void DrawBoolField<T>(List<T> objects, Func<T, bool> getter, Action<T, bool> setter, string name) where T: Object
    {
        using (new GUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(name,  GUILayout.Width(350f));
            bool sharedValue = objects.All(x => getter(x) == getter(objects[0]));
            bool currentValue = sharedValue ? getter(objects[0]) : false;
            bool newValue = EditorGUILayout.Toggle(currentValue);
            if (newValue != currentValue)
            {
                objects.ForEach(x =>
                {
                    Undo.RegisterCompleteObjectUndo(x, "Modify Controller");
                    setter(x, newValue);
                });
            }
        }
    }
}
