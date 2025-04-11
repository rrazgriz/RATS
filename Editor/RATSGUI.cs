// RATS - Raz's Animator Tweaks'n Stuff
// Original AnimatorExtensions by Dj Lukis.LT, under MIT License

// Copyright (c) 2023 Razgriz
// SPDX-License-Identifier: MIT

#if UNITY_EDITOR
using System;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Razgriz.RATS
{
    [InitializeOnLoad]
    internal class RATSDefineHandler
    {
        static bool DoesAssemblyExist(string assemblyName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.FullName.StartsWith(assemblyName))
                    return true;
            }
            return false;
        }

        static RATSDefineHandler()
        {
            if(!DoesAssemblyExist("0Harmony"))
            {
                RATSGUI.SetDefineSymbol("RATS_HARMONY", false);

                if(!SessionState.GetBool("RATSHarmonyWarning", false))
                {
                    EditorUtility.DisplayDialog("RATS: Harmony not found", 
                        "RATS requires Harmony to function. Please provide 0Harmony.dll somewhere in the project.\nA Harmony distribution is available via VPM.", 
                        "OK" );
                    SessionState.SetBool("RATSHarmonyWarning", true);
                }
            }
            else
            {
                RATSGUI.SetDefineSymbol("RATS_HARMONY", true);
            }
        }
    }

    [Serializable]
    public class RATSPreferences
    {
        public bool DisableAnimatorGraphFixes = false;
        public bool StateMotionLabels = true;
        public bool StateBlendtreeLabels = true;
        public bool StateAnimIsEmptyLabel = true;
        public bool StateColoredAnimIcon = false;
        public bool StateLoopedLabels = true;
        public float StateGraphIconScale = 1.0f;
        public bool StateGraphIconLocationIsCorner = false;
        public Color StateExtraLabelsColorEnabled = new Color(1.0f, 1.0f, 1.0f, 0.8f);
        public Color StateExtraLabelsColorDisabled = new Color(1.0f, 1.0f, 1.0f, 0.05f);
        public bool ShowWarningsTopLeft = true;
        public bool StateExtraLabelsWD = true;
        public bool StateExtraLabelsBehavior = true;
        public bool StateExtraLabelsMotionTime = true;
        public bool StateExtraLabelsSpeed = true;
        public bool GraphGridOverride = true;
        public float GraphGridDivisorMinor = 1.0f;
        public float GraphGridScalingMajor = 0.0f;
        public bool GraphDragNoSnap = false;
        public bool GraphDragSnapToModifiedGrid = false;
        public Color GraphGridBackgroundColor = new Color(0.15f, 0.15f, 0.15f, 1.0f);
        public Color GraphGridColorMajor = new Color(0f, 0f, 0f, 0.18f);
        public Color GraphGridColorMinor = new Color(0f, 0f, 0f, 0.28f);
        public bool NodeStyleOverride = true;
        public Color StateTextColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        public Color StateGlowColor = new Color(44/255f, 119/255f, 212/255f, 1f);
        public Color StateColorGray = new Color(0.3f, 0.3f, 0.3f, 1f);
        public Color SubStateMachineColor = new Color(0.05f, 0.25f, 0.5f, 1f);
        public Color StateColorOrange = new Color(0.78f, 0.38f, 0.15f, 1f);
        public Color StateColorAqua = new Color(0.22f, 0.58f, 0.59f, 1f);
        public Color StateColorGreen = new Color(0.07f, 0.47f, 0.20f, 1f);
        public Color StateColorRed = new Color(0.67f, 0.02f, 0.12f, 1f);
        public int StateLabelFontSize = 12;
        public bool DefaultStateWriteDefaults = false;
        public bool DefaultLayerWeight1 = true;
        public bool DefaultTransitionHasExitTime = true;
        public bool DefaultTransitionFixedDuration = true;
        public float DefaultTransitionTime = 0.0f;
        public float DefaultTransitionExitTime = 0.0f;
        public int DefaultTransitionInterruptionSource = 0;
        public bool DefaultTransitionOrderedInterruption = true;
        public bool DefaultTransitionCanTransitionToSelf = true;
        public bool ManipulateTransitionsMenuOption = true;
        public bool DoubleClickObjectCreation = true;
        public float DoubleClickTimeInterval = 0.15f;
        public bool EditNonAnyStateSelfTransition = false;
        public bool LayerListShowWD = true;
        public bool LayerListShowMixedWD = true;
        public bool ParameterListShowParameterTypeLabels = true;
        public bool ParameterListShowParameterTypeLabelShorten = false;
        public bool AnimationWindowShowActualPropertyNames = false;
        public bool AnimationWindowShowFullPath = false;
        public bool AnimationWindowTrimActualNames = false;
        public float AnimationWindowIndentScale = 1.0f;
        public bool ProjectWindowExtensions = true;
        public bool ProjectWindowFilesize = true;
        public bool ProjectWindowFolderChildren = true;
        public Color ProjectWindowLabelTextColor = new Color(1.0f, 1.0f, 1.0f, 0.3f);
        public TextAnchor ProjectWindowLabelAlignment = TextAnchor.MiddleRight;
    }

    public static class RATSPreferenceHandler
    {
        const string RATS_EDITORPREFSKEY = "RATS.PreferencesSerialized";

        public static void Save(RATSPreferences prefs, string key = RATS_EDITORPREFSKEY)
        {
            string prefsJson = JsonUtility.ToJson(prefs);
            EditorPrefs.SetString(key, prefsJson);
        }

        public static void Load(ref RATSPreferences prefs, string key = RATS_EDITORPREFSKEY)
        {
            string prefsJson = EditorPrefs.GetString(key, "{}");
            JsonUtility.FromJsonOverwrite(prefsJson, prefs);
            // Debug.Log($"[RATS] Loaded prefs from EditorPrefs Key: {key}");
            // Update our prefs in case the user has upgraded or something
            Save(prefs, key);
        }
    }

    public class RATSGUI : EditorWindow
    {
#if RATS_HARMONY
        const bool hasHarmony = true;
#else
        const bool hasHarmony = false;
#endif

        const int optionsIndentStep = 2;

        public static bool sectionExpandedBehavior = true;
        public static bool sectionExpandedStyling = true;
        public static bool sectionExpandedInfo = true;

        public static bool hasInitializedPreferences = false;

        static GUIStyle ToggleButtonStyle;
        static Vector2 scrollPosition = Vector2.zero;

        [MenuItem("Tools/RATS/Options")]
        public static void ShowWindow()
        {
            RATSGUI window = EditorWindow.GetWindow<RATSGUI>();
            window.titleContent = new GUIContent("  RATS", GetRATSIcon());
        }

        void OnInspectorUpdate() 
        {
            this.Repaint();
        }

        public static void OnEnable()
        {
            HandlePreferences();
        }

        public static void HandlePreferences()
        {
            if(!hasInitializedPreferences) // Need to grab from EditorPrefs
            {
                RATSPreferenceHandler.Load(ref RATS.Prefs);
                hasInitializedPreferences = true;
            }
            else // Already grabbed, set them instead
            {
                RATSPreferenceHandler.Save(RATS.Prefs);
            }
        }

        void OnGUI()
        {
            if (!hasInitializedPreferences) HandlePreferences();

            DrawRATSOptionsHeader();

#if !RATS_HARMONY
                EditorGUILayout.HelpBox(" RATS requires Harmony to function. Please install Harmony via VPM, or provide 0Harmony.dll somewhere in the project.", MessageType.Error);
#endif

            using (EditorGUILayout.ScrollViewScope scrollView = new EditorGUILayout.ScrollViewScope(scrollPosition))
            {
                scrollPosition = scrollView.scrollPosition;

                sectionExpandedInfo = EditorGUILayout.BeginFoldoutHeaderGroup(sectionExpandedInfo, new GUIContent("  Info", EditorGUIUtility.IconContent("d_ModelImporter Icon").image));
                if(sectionExpandedInfo)
                {
                    EditorGUI.indentLevel += 1;
                    DrawRATSInfo();
                    EditorGUI.indentLevel -= 1;
                }
                EditorGUILayout.EndFoldoutHeaderGroup();

                EditorGUILayout.Space(8);
                DrawUILine();
                sectionExpandedBehavior = EditorGUILayout.BeginFoldoutHeaderGroup(sectionExpandedBehavior, new GUIContent("  Behavior", EditorGUIUtility.IconContent("d_MoreOptions").image));
                EditorGUI.BeginDisabledGroup(!hasHarmony);
                if(sectionExpandedBehavior)
                {
                    EditorGUI.indentLevel += 1;
                    DrawNodeSnappingOptions();
                    DrawGraphStateDefaultsOptions();
                    DrawStateMachinePatchOptions();
                    DrawMultiEditorOptions();
                    DrawCompatibilityOptions();
                    EditorGUI.indentLevel -= 1;
                }
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndFoldoutHeaderGroup();

                EditorGUILayout.Space(8);
                DrawUILine();
                sectionExpandedStyling = EditorGUILayout.BeginFoldoutHeaderGroup(sectionExpandedStyling, new GUIContent("  Appearance", EditorGUIUtility.IconContent("d_ColorPicker.CycleSlider").image));
                EditorGUI.BeginDisabledGroup(!hasHarmony);
                if(sectionExpandedStyling)
                {
                    EditorGUI.indentLevel += 1;
                    DrawGraphLabelsOptions();
                    DrawGridStyleOptions();
                    DrawNodeStyleOptions();
                    DrawAnimatorParameterOptions();
                    DrawAnimationWindowAppearanceOptions();
                    DrawProjectWindowOptions();
                    EditorGUI.indentLevel -= 1;
                }
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
            EditorGUI.EndDisabledGroup();

            DrawRATSOptionsFooter();

            if (GUI.changed) HandlePreferences();
        }

        // UI Sections
        private static void DrawRATSInfo()
        {
            GUIStyle wrappedLabel = new GUIStyle(GUI.skin.GetStyle("label")) { wordWrap = true };

            SectionLabel("Added Features");
            EditorGUILayout.LabelField(
            " •  Options for changing default Write Defaults setting, forcing default layer weight to 1, etc \n" +
            " •  Copy and Paste layers using keyboard shorcuts or by right clicking \n" +
            " •  Show Layer WD Status \n" +
            " •  Press F2 to rename selected layer \n" +
            " •  Highlight/Select active animator controller by clicking its path in the bottom bar \n" +
            " •  Custom Node/Grid Styling \n" +
            " •  State Labels/Icons/Warnings \n" +
            " •  Animation Window: Improve Label Appearance \n" +
            "", wrappedLabel);

            SectionLabel("Bug Fixes");
            EditorGUILayout.LabelField(
            " •  Keep new or edited layer in view when editing controllers \n" +
            " •  Scroll to bottom of parameter list when adding a new parameter \n" +
            " •  Disable undo of 'Paste Sub-Sate Machine' action as it leaves dangling sub-assets \n" +
            " •  Prevent transition condition mode/function resetting when swapping parameter \n" +
            "", wrappedLabel);
        }

        private static void DrawGraphStateDefaultsOptions()
        {
            // Graph/State Defaults
            using (new GUILayout.VerticalScope())
            {
                DrawUILine(lightUILineColor);
                EditorGUI.BeginDisabledGroup(RATS.Prefs.DisableAnimatorGraphFixes); // Compatibility
                SectionLabel(new GUIContent("  Animator Graph Defaults", EditorGUIUtility.IconContent("d_CreateAddNew").image));
                EditorGUI.indentLevel += optionsIndentStep;

                RATS.Prefs.DefaultStateWriteDefaults = BooleanDropdown(RATS.Prefs.DefaultStateWriteDefaults, "Write Defaults", "Off", "On");
                RATS.Prefs.DefaultLayerWeight1 = BooleanDropdown(RATS.Prefs.DefaultLayerWeight1, "Layer Weight", "0", "1");
                using (new GUILayout.HorizontalScope())
                {
                    RATS.Prefs.DefaultTransitionHasExitTime = EditorGUILayout.ToggleLeft(new GUIContent("Has Exit Time"), RATS.Prefs.DefaultTransitionHasExitTime);
                    RATS.Prefs.DefaultTransitionExitTime = EditorGUILayout.DelayedFloatField("Exit Time  ", RATS.Prefs.DefaultTransitionExitTime);
                    RATS.Prefs.DefaultTransitionExitTime = Mathf.Clamp(RATS.Prefs.DefaultTransitionExitTime, 0f, 10f);
                }

                using (new GUILayout.HorizontalScope())
                {
                    RATS.Prefs.DefaultTransitionFixedDuration = EditorGUILayout.ToggleLeft(new GUIContent("Fixed Duration"), RATS.Prefs.DefaultTransitionFixedDuration);
                    RATS.Prefs.DefaultTransitionTime = EditorGUILayout.DelayedFloatField("Transition Time  ", RATS.Prefs.DefaultTransitionTime);
                    RATS.Prefs.DefaultTransitionTime = Mathf.Clamp(RATS.Prefs.DefaultTransitionTime, 0f, 10f);
                }
                
                using (new GUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Interruption Source");
                    RATS.Prefs.DefaultTransitionInterruptionSource = (int)(TransitionInterruptionSource)EditorGUILayout.EnumPopup((TransitionInterruptionSource)RATS.Prefs.DefaultTransitionInterruptionSource);
                }
                RATS.Prefs.DefaultTransitionOrderedInterruption = EditorGUILayout.ToggleLeft(new GUIContent("Ordered Interruption"), RATS.Prefs.DefaultTransitionOrderedInterruption);
                RATS.Prefs.DefaultTransitionCanTransitionToSelf = EditorGUILayout.ToggleLeft(new GUIContent("Can Transition To Self"), RATS.Prefs.DefaultTransitionCanTransitionToSelf);

                EditorGUI.EndDisabledGroup(); // Compatibility 
                EditorGUI.indentLevel -= optionsIndentStep;
            }
        }

        private static void DrawGraphLabelsOptions()
        {
            // Graph Labels
            using (new GUILayout.VerticalScope())
            {
                DrawUILine(lightUILineColor);
                SectionLabel(new GUIContent("  Animator Graph Labels", EditorGUIUtility.IconContent("d_AnimatorController Icon").image));
                EditorGUI.indentLevel += optionsIndentStep;

                using (new GUILayout.HorizontalScope())
                {
                    ToggleButton(ref RATS.Prefs.StateLoopedLabels, new GUIContent("   Loop Time", EditorGUIUtility.IconContent("d_preAudioLoopOff@2x").image, "Show an icon when a state's animation is set to Loop Time"));
                    ToggleButton(ref RATS.Prefs.StateBlendtreeLabels, new GUIContent("   Blendtrees", EditorGUIUtility.IconContent("d_BlendTree Icon").image, "Show an icon when a state's motion is a Blendtree"));
                }
                using (new GUILayout.HorizontalScope())
                {
                    ToggleButton(ref RATS.Prefs.StateAnimIsEmptyLabel, new GUIContent("   Empty Anims/States", EditorGUIUtility.IconContent("Warning").image, "Display a warning if a state's animation is empty or if a state has no motion"));
                    ToggleButton(ref RATS.Prefs.StateColoredAnimIcon, new GUIContent("   Colored Anim Icon", EditorGUIUtility.IconContent("d_AnimationClip Icon").image, "Should the Animation clip icon be monochrome or colored?"));
                }
                
                RATS.Prefs.StateGraphIconScale = EditorGUILayout.Slider("Icon Scale", RATS.Prefs.StateGraphIconScale, 1.0f, 1.5f);

                using (new GUILayout.HorizontalScope())
                {
                    RATS.Prefs.ShowWarningsTopLeft = BooleanDropdown(RATS.Prefs.ShowWarningsTopLeft, "Warning Location", "Next To Motion Name", "Top Left");
                    RATS.Prefs.StateGraphIconLocationIsCorner = BooleanDropdown(RATS.Prefs.StateGraphIconLocationIsCorner, "Icon Location", "Next to Motion Name", "Left Corner");
                }

                EditorGUILayout.Space(8);
                using (new GUILayout.HorizontalScope())
                {
                    ToggleButton(ref RATS.Prefs.StateExtraLabelsWD, "<b>WD</b>  Write Defaults", "Indicate whether a state has Write Defaults enabled");
                    ToggleButton(ref RATS.Prefs.StateExtraLabelsBehavior, "<b>B</b>      Behavior", "Indicate whether a state has a State Behavior");
                }
                using (new GUILayout.HorizontalScope())
                {
                    ToggleButton(ref RATS.Prefs.StateExtraLabelsSpeed, "<b>S</b>      Speed Param", "Indicate whether a state has a Speed parameter");
                    ToggleButton(ref RATS.Prefs.StateExtraLabelsMotionTime, "<b>M</b>     Motion Time", "Indicate whether a state has a Motion Time parameter");
                }
                using (new GUILayout.HorizontalScope())
                {
                    ToggleButton(ref RATS.Prefs.StateMotionLabels, "<b>Tt</b>    Motion Names", "Show the name of the state's clip/blendtree");
                }

                using (new GUILayout.HorizontalScope())
                {
                    RATS.Prefs.StateExtraLabelsColorEnabled = EditorGUILayout.ColorField("Extras Enabled", RATS.Prefs.StateExtraLabelsColorEnabled);
                    RATS.Prefs.StateExtraLabelsColorDisabled = EditorGUILayout.ColorField("Extras Disabled", RATS.Prefs.StateExtraLabelsColorDisabled);
                }

                using (new GUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Tip: Hold ALT to see all labels at any time", new GUIStyle("miniLabel"));
                }
                EditorGUI.indentLevel -= optionsIndentStep;
            }
        }

        private static void DrawAnimatorParameterOptions()
        {
            // Parameter List
            using (new GUILayout.VerticalScope())
            {
                DrawUILine(lightUILineColor);
                SectionLabel(new GUIContent("  Parameters List", EditorGUIUtility.IconContent("d_VerticalLayoutGroup Icon").image));
                EditorGUI.indentLevel += optionsIndentStep;

                using (new GUILayout.HorizontalScope())
                {
                    ToggleButton(ref RATS.Prefs.ParameterListShowParameterTypeLabels, "Show Parameter Type Labels", "Show the type of parameter being animated next to its value");
                    ToggleButton(ref RATS.Prefs.ParameterListShowParameterTypeLabelShorten, "Shorten Label", "Shorten the label to just the first letter");
                }

                EditorGUI.indentLevel -= optionsIndentStep;
            }
        }

        private static void DrawStateMachinePatchOptions()
        {
            // State Machine
            using (new GUILayout.VerticalScope())
            {
                DrawUILine(lightUILineColor);
                SectionLabel(new GUIContent("  StateMachine Patches", EditorGUIUtility.IconContent("d_AnimatorStateMachine Icon").image));
                EditorGUI.indentLevel += optionsIndentStep;

                using (new GUILayout.HorizontalScope())
                {
                    ToggleButton(ref RATS.Prefs.ManipulateTransitionsMenuOption, "Add Transition Manipulation Right-Click Options", "Add options to right-click menu of States and Transitions");
                    ToggleButton(ref RATS.Prefs.DoubleClickObjectCreation, "Double Click Object Creation", "Double Click on a state while holding left Control to create a Transition, double click elsewhere to create a State");
                }

                if (RATS.Prefs.DoubleClickObjectCreation)
                {
                    RATS.Prefs.DoubleClickTimeInterval = EditorGUILayout.Slider("Click Interval", RATS.Prefs.DoubleClickTimeInterval, 0.0f, 1.0f);
                }

                EditorGUI.indentLevel -= optionsIndentStep;
            }
        }

        private static void DrawAnimationWindowAppearanceOptions()
        {
            // Animation Window
            using (new GUILayout.VerticalScope())
            {
                DrawUILine(lightUILineColor);
                SectionLabel(new GUIContent("  Animation Window", EditorGUIUtility.IconContent("d_UnityEditor.AnimationWindow").image));
                EditorGUI.indentLevel += optionsIndentStep;

                using (new GUILayout.HorizontalScope())
                {
                    ToggleButton(ref RATS.Prefs.AnimationWindowShowActualPropertyNames, "Show Actual Property Names", "Show the actual name of properties instead of Unity's display names");
                    ToggleButton(ref RATS.Prefs.AnimationWindowShowFullPath, "Show Full Path", "Show the full path of properties being animated");
                }
                using (new GUILayout.HorizontalScope())
                {
                    ToggleButton(ref RATS.Prefs.AnimationWindowTrimActualNames, "Trim m_ From Actual Names", "Trim the leading m_ from actual property names");
                }

                RATS.Prefs.AnimationWindowIndentScale = EditorGUILayout.Slider("Indent Scale", RATS.Prefs.AnimationWindowIndentScale, 0.0f, 1.0f);
                RATS.Prefs.AnimationWindowIndentScale = Mathf.Round(RATS.Prefs.AnimationWindowIndentScale * 20f) / 20f;

                EditorGUILayout.LabelField("When disabling these options, click on a different animation to refresh", new GUIStyle("miniLabel"));
                EditorGUI.indentLevel -= optionsIndentStep;
            }
        }

        private static void DrawMultiEditorOptions()
        {

            using (new GUILayout.VerticalScope())
            {
                DrawUILine(lightUILineColor);
                SectionLabel(new GUIContent("  Multi-Editor", EditorGUIUtility.IconContent("d_UnityEditor.FindDependencies").image));
                EditorGUI.indentLevel += optionsIndentStep;
                ToggleButton(ref RATS.Prefs.EditNonAnyStateSelfTransition, "Edit Non-AnyState Self Transition", "Allows the multi-editor to edit the 'Can Transition To Self' option of non-AnyState transitions.");
                EditorGUI.indentLevel -= optionsIndentStep;
            }
        }
        
        private static void DrawCompatibilityOptions()
        {
            // Disable Patch Categories
            using (new GUILayout.VerticalScope())
            {
                DrawUILine(lightUILineColor);
                SectionLabel(new GUIContent("  Compatibility", EditorGUIUtility.IconContent("d_UnityEditor.Graphs.AnimatorControllerTool").image));
                EditorGUI.indentLevel += optionsIndentStep;

                EditorGUI.BeginChangeCheck();
                ToggleButton(ref RATS.Prefs.DisableAnimatorGraphFixes, "Disable Graph Window Patches (takes a few seconds)", "Allows other utilities to patch Controller editor window");
                if (EditorGUI.EndChangeCheck())
                {
                    SetDefineSymbol("RATS_NO_ANIMATOR", RATS.Prefs.DisableAnimatorGraphFixes);

                    // Disable Options that conflict with other editor tools
                    if (RATS.Prefs.DisableAnimatorGraphFixes)
                    {
                        RATS.Prefs.StateLoopedLabels = false;
                        RATS.Prefs.StateBlendtreeLabels = false;
                        RATS.Prefs.StateAnimIsEmptyLabel = false;
                        RATS.Prefs.ShowWarningsTopLeft = false;
                        RATS.Prefs.StateExtraLabelsWD = false;
                        RATS.Prefs.StateExtraLabelsBehavior = false;
                        RATS.Prefs.StateExtraLabelsSpeed = false;
                        RATS.Prefs.StateExtraLabelsMotionTime = false;
                        RATS.Prefs.StateMotionLabels = false;
                        RATS.Prefs.GraphGridOverride = false;
                    }
                    HandlePreferences();

                    // Try to force recompilation, takes a few seconds
                    UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                    AssetDatabase.Refresh();
                }
                EditorGUI.indentLevel -= optionsIndentStep;
            }
        }

        private static void DrawGridStyleOptions()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                DrawUILine(lightUILineColor);
                SectionLabel(new GUIContent("  Grid", EditorGUIUtility.IconContent("GridBrush Icon").image));
                EditorGUI.indentLevel += optionsIndentStep;

                ToggleButton(ref RATS.Prefs.GraphGridOverride, "Use Custom Grid");
                RATS.Prefs.GraphGridBackgroundColor = EditorGUILayout.ColorField(new GUIContent("Background"), RATS.Prefs.GraphGridBackgroundColor, true, false, false);

                RATS.Prefs.GraphGridScalingMajor = EditorGUILayout.Slider("Major Size", RATS.Prefs.GraphGridScalingMajor, 0.0f, 5.0f);
                RATS.Prefs.GraphGridDivisorMinor = EditorGUILayout.Slider("Minor Divisions", RATS.Prefs.GraphGridDivisorMinor, 1.0f, 50f);
                RATS.Prefs.GraphGridDivisorMinor = Mathf.Round(RATS.Prefs.GraphGridDivisorMinor * 1f) / 1f;

                RATS.Prefs.GraphGridColorMajor = EditorGUILayout.ColorField("Major", RATS.Prefs.GraphGridColorMajor);
                RATS.Prefs.GraphGridColorMinor = EditorGUILayout.ColorField("Minor", RATS.Prefs.GraphGridColorMinor);
                EditorGUI.indentLevel -= optionsIndentStep;
            }
        }

        private static void DrawNodeStyleOptions()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                DrawUILine(lightUILineColor);
                SectionLabel(new GUIContent("  Nodes", EditorGUIUtility.IconContent("AnimatorState Icon").image));
                EditorGUI.indentLevel += optionsIndentStep;

                EditorGUI.BeginChangeCheck();
                ToggleButton(ref RATS.Prefs.NodeStyleOverride, "Use Custom Node Style");
                RATS.Prefs.StateLabelFontSize = EditorGUILayout.IntSlider("Font Size", RATS.Prefs.StateLabelFontSize, 5, 20);

                RATS.Prefs.StateTextColor = EditorGUILayout.ColorField("Text Color", RATS.Prefs.StateTextColor);
                RATS.Prefs.StateGlowColor = EditorGUILayout.ColorField("Highlight", RATS.Prefs.StateGlowColor);
                RATS.Prefs.StateColorGray = EditorGUILayout.ColorField("Normal State", RATS.Prefs.StateColorGray);
                RATS.Prefs.SubStateMachineColor = EditorGUILayout.ColorField("SubStateMachines", RATS.Prefs.SubStateMachineColor);
                RATS.Prefs.StateColorOrange = EditorGUILayout.ColorField("Default State", RATS.Prefs.StateColorOrange);
                RATS.Prefs.StateColorAqua = EditorGUILayout.ColorField("Any State", RATS.Prefs.StateColorAqua);
                RATS.Prefs.StateColorGreen = EditorGUILayout.ColorField("Entry State", RATS.Prefs.StateColorGreen);
                RATS.Prefs.StateColorRed = EditorGUILayout.ColorField("Exit State", RATS.Prefs.StateColorRed);
                
                if (EditorGUI.EndChangeCheck())
                {
#if RATS_HARMONY
                    RATS.AnimatorWindowState.handledNodeStyles.Clear();
#endif
                    RATS.UpdateGraphTextures();
                }
                EditorGUI.indentLevel -= optionsIndentStep;
            }
        }

        private static void DrawNodeSnappingOptions()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                DrawUILine(lightUILineColor);
                SectionLabel(new GUIContent("  Node Snapping", EditorGUIUtility.IconContent("AnimatorStateTransition Icon").image));
                EditorGUI.indentLevel += optionsIndentStep;

                using (new GUILayout.HorizontalScope())
                {
                    ToggleButton(ref RATS.Prefs.GraphDragNoSnap, "Disable Snapping by Default", "Disable grid snapping (hold Control while dragging for alternate mode)");
                    ToggleButton(ref RATS.Prefs.GraphDragSnapToModifiedGrid, "Snap to custom grid", "Snaps to user-specified grid");
                }
                EditorGUILayout.LabelField("Tip: hold Control while dragging for the opposite of this setting", new GUIStyle("miniLabel"));
                EditorGUI.indentLevel -= optionsIndentStep;
            }
        }

        private static void DrawProjectWindowOptions()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                DrawUILine(lightUILineColor);
                SectionLabel(new GUIContent("  Project Window (Experimental)", EditorGUIUtility.IconContent("d_Project").image));
                EditorGUI.indentLevel += optionsIndentStep;

                using (new GUILayout.HorizontalScope())
                {
                    ToggleButton(ref RATS.Prefs.ProjectWindowExtensions, "Show file extensions", "Show file extension (list view only)");
                    ToggleButton(ref RATS.Prefs.ProjectWindowFilesize, "Show file size", "Show filesize (list view only)");
                }
                ToggleButton(ref RATS.Prefs.ProjectWindowFolderChildren, "Show Folder Children Count");
                RATS.Prefs.ProjectWindowLabelAlignment = (TextAnchor)EditorGUILayout.EnumPopup("Alignment", RATS.Prefs.ProjectWindowLabelAlignment);
                RATS.Prefs.ProjectWindowLabelTextColor = EditorGUILayout.ColorField(new GUIContent("Label Text Color"), RATS.Prefs.ProjectWindowLabelTextColor, true, true, false);
                EditorGUILayout.LabelField("Note: these options currently only apply in list mode in the project/asset selection views.", new GUIStyle("miniLabel"));
                EditorGUI.indentLevel -= optionsIndentStep;
            }
        }

        private static void DrawRATSOptionsHeader()
        {
            EditorGUILayout.LabelField(new GUIContent("  RATS", GetRATSIcon()), new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.LabelField(new GUIContent($"  Raz's Animator Tweaks 'n' Stuff   •   v{RATS.Version}"), new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter });
            DrawUILine();
        }

        private static void DrawRATSOptionsFooter()
        {
            using (new GUILayout.VerticalScope())
            {
                DrawUILine();
                using (new GUILayout.HorizontalScope())
                {
                    // Github link button
                    using (new GUILayout.HorizontalScope())
                    {
                        bool githubLinkClicked = GUILayout.Button(new GUIContent("  View Repo on Github", GetGithubIcon()), new GUIStyle("Button"));
                        EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link); // Lights up button with link cursor
                        if (githubLinkClicked) Application.OpenURL(@"https://github.com/rrazgriz/RATS");
                    }

                    // Version & Name
                    using (new GUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"   RATS  v{RATS.Version}   •   Razgriz", new GUIStyle("Label"));
                    }

                    bool resetPrefsButton = GUILayout.Button(new GUIContent("  Reset Preferences", EditorGUIUtility.IconContent("CollabConflict").image), new GUIStyle("Button"));
                    bool doResetPrefs = false;
                    if(resetPrefsButton)
                        doResetPrefs = EditorUtility.DisplayDialog("RATS: Reset Options", "This will reset all RATS options to defaults. Do you want to continue?", "Reset", "Cancel");
                    
                    if(doResetPrefs)
                    {
                        RATSPreferences defaultPrefsTemp = new RATSPreferences();
                        RATSPreferenceHandler.Save(defaultPrefsTemp);
                        RATSPreferenceHandler.Load(ref RATS.Prefs);
#if RATS_HARMONY
                        RATS.AnimatorWindowState.handledNodeStyles.Clear();
#endif
                        RATS.UpdateGraphTextures();
                    }
                }
            }
        }

        // Helper Functions
        public static void SectionLabel(string label) => SectionLabel(new GUIContent(label));
        public static void SectionLabel(GUIContent label)
        {
            EditorGUILayout.LabelField(label, new GUIStyle("BoldLabel"));
        }

        public static void ToggleButton(ref bool param, string label, string tooltip="") => ToggleButton(ref param, new GUIContent(label, tooltip));
        public static void ToggleButton(ref bool param, GUIContent label)
        {
            if(ToggleButtonStyle == null)
            {
                ToggleButtonStyle = new GUIStyle("Label");
                ToggleButtonStyle.richText = true;
            }

            param = EditorGUILayout.ToggleLeft(label, param, ToggleButtonStyle);
        }

        public static bool BooleanDropdown(bool value, string label, string falseLabel, string trueLabel)
        {
            return Dropdown(value ? 1 : 0, label, new string[] {falseLabel, trueLabel}) == 1;
        }
        public static int Dropdown(int value, string label, string[] optionLabels)
        {
            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, new GUILayoutOption[] {GUILayout.MinWidth(20f)});
                return EditorGUILayout.Popup(value, optionLabels, new GUILayoutOption[] {GUILayout.MinWidth(50f)});
            }
        }

        public static readonly Color lightUILineColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);
        public static readonly Color heavyUILineColor = new Color(0.5f, 0.5f, 0.5f, 1.0f);
        public static void DrawUILine() => DrawUILine(heavyUILineColor);
        public static void DrawUILine(Color color, int thickness = 1, int padding = 10)
        {
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding+thickness));
            EditorGUI.DrawRect(new Rect(r.x - 2, r.y + padding/2, r.width + 6, thickness), color);
        }

        public static Texture GetRATSIcon() => AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath("a5de26a705a067a4caed95b51ab10ea4"));
        public static Texture GetGithubIcon() => AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath("9cb81a504770b4943839f4d46d94208f"));

        public static Texture2D TextureFromBase64PNG(string base64)
        {
            Byte[] tex_b64_bytes = System.Convert.FromBase64String(base64);
            Texture2D tex = new Texture2D(1,1);
            tex.LoadImage(tex_b64_bytes);
            return tex;
        }

        // Add/Remove scripting define symbols
        // Copyright Thryrallo, from ThryEditor
        // SPDX-License-Identifier: MIT
        public static void SetDefineSymbol(string symbol, bool active) => SetDefineSymbol(symbol, active, true);
        public static void SetDefineSymbol(string symbol, bool active, bool refresh_if_changed)
        {
            try
            {
                string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(
                        BuildTargetGroup.Standalone);
                if (!symbols.Contains(symbol) && active)
                {
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(
                                BuildTargetGroup.Standalone, symbols + ";" + symbol);
                    if(refresh_if_changed)
                        AssetDatabase.Refresh();
                }
                else if (symbols.Contains(symbol) && !active)
                {
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(
                                BuildTargetGroup.Standalone, Regex.Replace(symbols, @";?" + @symbol, ""));
                    if(refresh_if_changed)
                        AssetDatabase.Refresh();
                }
            }
            catch (Exception e)
            {
                e.ToString();
            }
        }
    }
}
#endif