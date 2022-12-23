// RATS - Raz's Animator Tweaks'n Stuff
// Original AnimatorExtensions by Dj Lukis.LT, under MIT License

// Copyright (c) 2022 Razgriz
// SPDX-License-Identifier: MIT

#if UNITY_EDITOR
using System;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Razgriz.RATS
{
    [Serializable]
    public class RATSPreferences
    {
        const string RATS_EDITORPREFS_KEY = "RATS.PreferencesSerialized";

        public bool DisableAnimatorGraphFixes = false;
        public bool StateMotionLabels = true;
        public bool StateBlendtreeLabels = true;
        public bool StateAnimIsEmptyLabel = true;
        public bool StateLoopedLabels = true;
        public bool HideOffLabels = false;
        public bool ShowWarningsTopLeft = false;
        public bool StateExtraLabelsWD = true;
        public bool StateExtraLabelsBehavior = true;
        public bool StateExtraLabelsMotionTime = false;
        public bool StateExtraLabelsSpeed = false;
        public bool GraphGridOverride = false;
        public float GraphGridDivisorMinor = 10.0f;
        public float GraphGridScalingMajor = 1.0f;
        public bool GraphDragNoSnap = false;
        public bool GraphDragSnapToModifiedGrid = false;
        public Color GraphGridBackgroundColor = GUI.backgroundColor;
        public Color GraphGridColorMajor = new Color(0f, 0f, 0f, 0.18f);
        public Color GraphGridColorMinor = new Color(0f, 0f, 0f, 0.28f);
        public Color StateTextColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        public Color StateColorGray = new Color(0.3f, 0.3f, 0.3f, 1f);
        public Color StateColorOrange = new Color(0.78f, 0.38f, 0.15f, 1f);
        public Color StateColorAqua = new Color(0.22f, 0.58f, 0.59f, 1f);
        public Color StateColorGreen = new Color(0.07f, 0.47f, 0.20f, 1f);
        public Color StateColorRed = new Color(0.67f, 0.02f, 0.12f, 1f);
        public int StateLabelFontSize = 12;
        public bool NewStateWriteDefaults = false;
        public bool NewLayersWeight1 = true;
        public bool NewTransitionsZeroTime = true;
        public bool NewTransitionsExitTime = false;
        public bool AnimationWindowShowActualPropertyNames = false;
        public bool AnimationWindowShowFullPath = false;
        public bool AnimationWindowTrimActualNames = false;
        public float AnimationWindowIndentScale = 1.0f;
    }

    public static class RATSPreferenceHandler
    {
        const string RATS_EDITORPREFSKEY = "RATS.PreferencesSerialized";

        public static void Save(RATSPreferences prefs, string key = RATS_EDITORPREFSKEY)
        {
            string prefsJson = JsonUtility.ToJson(prefs);
            EditorPrefs.SetString(RATS_EDITORPREFSKEY, prefsJson);
            Debug.Log("Saved RATS prefs: " + prefsJson);
        }

        public static void Load(ref RATSPreferences prefs, string key = RATS_EDITORPREFSKEY)
        {
            string prefsJson = EditorPrefs.GetString(key, "{}");
            JsonUtility.FromJsonOverwrite(prefsJson, prefs);
            Debug.Log("Loaded RATS prefs: " + prefsJson);
            // Update our prefs in case the user has upgraded or something
            Save(prefs, key);
        }
    }

    public class RATSGUI : EditorWindow
    {
        public const string version = "2022.12.23";

        public static bool prefs_DisableAnimatorGraphFixes;

        public static bool prefs_StateMotionLabels;
        public static bool prefs_StateBlendtreeLabels;
        public static bool prefs_StateAnimIsEmptyLabel;
        public static bool prefs_StateLoopedLabels;
        public static bool prefs_HideOffLabels;
        public static bool prefs_ShowWarningsTopLeft;

        public static bool prefs_StateExtraLabelsWD;
        public static bool prefs_StateExtraLabelsBehavior;
        public static bool prefs_StateExtraLabelsMotionTime;
        public static bool prefs_StateExtraLabelsSpeed;

        public static bool prefs_GraphGridOverride;
        public static float prefs_GraphGridDivisorMinor;
        public static float prefs_GraphGridScalingMajor;
        public static Color prefs_GraphGridColorMinor = new Color(0.0f, 0.0f, 0.0f, 0.18f);
        public static Color prefs_GraphGridColorMajor = new Color(0.0f, 0.0f, 0.0f, 0.28f);
        public static Color prefs_GraphGridBackgroundColor = new Color(0.1647f, 0.1647f, 0.1647f, 1.0f);
        public static bool prefs_GraphDragNoSnap;
        public static bool prefs_GraphDragSnapToModifiedGrid;
        public static Color prefs_StateTextColor = new Color(0.9f, 0.9f, 0.9f, 1.0f);
        public static Color prefs_StateColorGray = new Color(0.3f, 0.3f, 0.3f, 1.0f);
        public static Color prefs_StateColorOrange = new Color(198/255f, 96/255f, 37/255f, 1f);
        public static Color prefs_StateColorAqua = new Color(56/255f, 148/255f, 150/255f, 1f);
        public static Color prefs_StateColorGreen = new Color(17/255f, 119/255f, 51/255f, 1f);
        public static Color prefs_StateColorRed = new Color(170/255f, 5/255f, 30/255f, 1f);
        public static int prefs_StateLabelFontSize;
        public static bool prefs_NodeStyleOverride = true;

        public static bool prefs_NewStateWriteDefaults;
        public static bool prefs_NewLayersWeight1;
        public static bool prefs_NewTransitionsZeroTime;
        public static bool prefs_NewTransitionsExitTime;

        public static bool prefs_AnimationWindowShowActualPropertyNames;
        public static float prefs_AnimationWindowIndentScale;
        
        public static bool prefs_AnimationWindowShowFullPath;
        public static bool prefs_AnimationWindowTrimActualNames;

        public static bool hasInitializedPreferences = false;
        public static bool updateNodeStyle = false;

        public enum Tabs : int
        {
            Tweaks = 0,
            Theming = 1,
            AnimEditor = 2
        }

        string[] toolbarStrings = {"Tweaks", "Theming", "AnimEditor"};

        static Tabs tab = Tabs.Tweaks;

        private static Texture2D githubIcon;
        private static Texture2D editorWindowIcon;
        static GUIStyle ToggleButtonStyle;

        [MenuItem("Tools/RATS/Options")]
        public static void ShowWindow()
        {
            if(editorWindowIcon == null)
            {
                // Decode from base64 encoded 16x16 icon
                editorWindowIcon = TextureFromBase64PNG(RATSConstants.RATSLogoBase64);
            }

            RATSGUI window = EditorWindow.GetWindow<RATSGUI>();
            window.titleContent = new GUIContent("  RATS", editorWindowIcon);
        }

        void OnInspectorUpdate() {
            this.Repaint();
        }

        void OnGUI()
        {
            if(!hasInitializedPreferences) HandlePreferences();
            
            tab = (Tabs)GUILayout.Toolbar((int)tab, toolbarStrings);

            switch(tab)
            {
                default:
                    EditorGUI.BeginDisabledGroup(prefs_DisableAnimatorGraphFixes); // CEditor Compatibility
                    // Graph/State Defaults
                    using(new GUILayout.VerticalScope())
                    {
                        SectionLabel(new GUIContent("  Animator Graph Defaults", EditorGUIUtility.IconContent("d_CreateAddNew").image));
                        using(new GUILayout.HorizontalScope())
                        {
                            ToggleButton(ref prefs_NewStateWriteDefaults, "New States: WD Setting", "Enable or disable Write Defaults on new states");
                            ToggleButton(ref prefs_NewLayersWeight1, "New Layers: 1 Weight", "Set new layers to have 1 weight automatically");
                        }
                        using(new GUILayout.HorizontalScope())
                        {
                            ToggleButton(ref prefs_NewTransitionsExitTime, "New Transition: Has Exit Time", "Enable or Disable Has Exit Time on new transitions");
                            ToggleButton(ref prefs_NewTransitionsZeroTime, "New Transition: 0 Time", "Set new transitions to have 0 exit/transition time");
                        }
                    }

                    EditorGUI.EndDisabledGroup(); // CEditor Compatibility 

                    // Graph Labels
                    using(new GUILayout.VerticalScope())
                    {
                        DrawUILine();
                        SectionLabel(new GUIContent("  Animator Graph Labels", EditorGUIUtility.IconContent("d_AnimatorController Icon").image));

                        using(new GUILayout.HorizontalScope())
                        {
                            ToggleButton(ref prefs_StateLoopedLabels, new GUIContent("   Loop Time", EditorGUIUtility.IconContent("d_preAudioLoopOff@2x").image, "Show an icon when a state's animation is set to Loop Time"));
                            ToggleButton(ref prefs_StateBlendtreeLabels, new GUIContent("   Blendtrees", EditorGUIUtility.IconContent("d_BlendTree Icon").image, "Show an icon when a state's motion is a Blendtree"));
                        }
                        using(new GUILayout.HorizontalScope())
                        {
                            ToggleButton(ref prefs_StateAnimIsEmptyLabel, new GUIContent("   Empty Anims/States", EditorGUIUtility.IconContent("Warning").image, "Display a warning if a state's animation is empty or if a state has no motion"));
                            ToggleButton(ref prefs_ShowWarningsTopLeft, "Warning Icons Top Left", "Show warnings in top left instead of next to name");
                        }

                        DrawUILine(new Color(0.5f, 0.5f, 0.5f, 0.2f));
                        using(new GUILayout.HorizontalScope())
                        {
                            ToggleButton(ref prefs_StateExtraLabelsWD, "<b>WD</b>  Write Defaults", "Indicate whether a state has Write Defaults enabled");
                            ToggleButton(ref prefs_StateExtraLabelsBehavior, "<b>B</b>      Behavior", "Indicate whether a state has a State Behavior");
                        }
                        using(new GUILayout.HorizontalScope())
                        {
                            ToggleButton(ref prefs_StateExtraLabelsSpeed, "<b>S</b>      Speed Param", "Indicate whether a state has a Speed parameter");
                            ToggleButton(ref prefs_StateExtraLabelsMotionTime, "<b>M</b>     Motion Time", "Indicate whether a state has a Motion Time parameter");
                        }
                        using(new GUILayout.HorizontalScope())
                        {
                            ToggleButton(ref prefs_StateMotionLabels, "<b>Tt</b>    Motion Names", "Show the name of the state's clip/blendtree");
                            ToggleButton(ref prefs_HideOffLabels, "Hide Labels Completely", "Hide Labels when condition is false, instead of dimming");
                        }
                        
                        using(new GUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField("Tip: Hold ALT to see all labels at any time", new GUIStyle("miniLabel"));
                        }
                    }

                    // Animation Window
                    using(new GUILayout.VerticalScope())
                    {
                        DrawUILine();
                        SectionLabel(new GUIContent("  Animation Window", EditorGUIUtility.IconContent("d_UnityEditor.AnimationWindow").image));
                        
                        using(new GUILayout.HorizontalScope())
                        {
                            ToggleButton(ref prefs_AnimationWindowShowActualPropertyNames, "Show Actual Property Names", "Show the actual name of properties instead of Unity's display names");
                            ToggleButton(ref prefs_AnimationWindowShowFullPath, "Show Full Path", "Show the full path of properties being animated");
                        }
                        using(new GUILayout.HorizontalScope())
                        {
                            ToggleButton(ref prefs_AnimationWindowTrimActualNames, "Trim m_ From Actual Names", "Trim the leading m_ from actual property names");
                        }

                        prefs_AnimationWindowIndentScale = EditorGUILayout.Slider("Hierarchy Indent Scale", prefs_AnimationWindowIndentScale, 0.0f, 1.0f);
                        prefs_AnimationWindowIndentScale = Mathf.Round(prefs_AnimationWindowIndentScale * 20f)/20f;

                        EditorGUILayout.LabelField("When disabling these options, click on a different animation to refresh", new GUIStyle("miniLabel"));
                    }

                    // Disable Patch Categories
                    using(new GUILayout.VerticalScope())
                    {
                        DrawUILine();
                        SectionLabel(new GUIContent("  Compatibility", EditorGUIUtility.IconContent("d_UnityEditor.Graphs.AnimatorControllerTool").image));

                        EditorGUI.BeginChangeCheck();
                        ToggleButton(ref prefs_DisableAnimatorGraphFixes, "Disable Graph Window Patches (takes a few seconds)", "Allows other utilities to patch Controller editor window");
                        if(EditorGUI.EndChangeCheck())
                        {
                            SetDefineSymbol("RAZGRIZ_AEXTENSIONS_NOANIMATOR", prefs_DisableAnimatorGraphFixes);

                            // Disable Options that conflict with CEditor
                            if(prefs_DisableAnimatorGraphFixes)
                            {
                                prefs_StateLoopedLabels = false;
                                prefs_StateBlendtreeLabels = false;
                                prefs_StateAnimIsEmptyLabel = false;
                                prefs_ShowWarningsTopLeft = false;
                                prefs_StateExtraLabelsWD = false;
                                prefs_StateExtraLabelsBehavior = false;
                                prefs_StateExtraLabelsSpeed = false;
                                prefs_StateExtraLabelsMotionTime = false;
                                prefs_StateMotionLabels = false;
                                prefs_HideOffLabels = false;
                                prefs_GraphGridOverride = false;
                            }
                            HandlePreferences();

                            // Try to force recompilation, takes a few seconds
                            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                            AssetDatabase.Refresh();
                        }
                    }
                    break;


                case Tabs.Theming:
                    // Graph Styling
                    using(new GUILayout.VerticalScope())
                    {
                        SectionLabel(new GUIContent("  Animator Graph Styling", EditorGUIUtility.IconContent("d_ColorPicker.CycleSlider").image));

                        ToggleButton(ref prefs_GraphGridOverride, "Override Default Grid Style");
                        prefs_GraphGridBackgroundColor = EditorGUILayout.ColorField(new GUIContent("Background"), prefs_GraphGridBackgroundColor, true, false, false);

                        prefs_GraphGridScalingMajor = EditorGUILayout.Slider("Major Grid Spacing", prefs_GraphGridScalingMajor, 0.0f, 5.0f);
                        prefs_GraphGridDivisorMinor = EditorGUILayout.Slider("Minor Grid Divisions", prefs_GraphGridDivisorMinor, 1.0f, 50f);
                        prefs_GraphGridDivisorMinor = Mathf.Round(prefs_GraphGridDivisorMinor * 1f)/1f;

                        prefs_GraphGridColorMajor = EditorGUILayout.ColorField("Major Grid", prefs_GraphGridColorMajor);
                        prefs_GraphGridColorMinor = EditorGUILayout.ColorField("Minor Grid", prefs_GraphGridColorMinor);

                        DrawUILine(new Color(0.5f, 0.5f, 0.5f, 0.2f));
                        
                        EditorGUI.BeginChangeCheck();
                        prefs_StateTextColor = EditorGUILayout.ColorField("State Text Color", prefs_StateTextColor);
                        prefs_StateColorGray = EditorGUILayout.ColorField("Normal State Color", prefs_StateColorGray);
                        prefs_StateColorOrange = EditorGUILayout.ColorField("Default State Color", prefs_StateColorOrange);
                        prefs_StateColorAqua = EditorGUILayout.ColorField("Any State Color", prefs_StateColorAqua);
                        prefs_StateColorGreen = EditorGUILayout.ColorField("Entry State Color", prefs_StateColorGreen);
                        prefs_StateColorRed = EditorGUILayout.ColorField("Exit State Color", prefs_StateColorRed);

                        DrawUILine(new Color(0.5f, 0.5f, 0.5f, 0.2f));
                        ToggleButton(ref prefs_NodeStyleOverride, "Override Default Node Style");
                        prefs_StateLabelFontSize = EditorGUILayout.IntSlider("State Name Font Size", prefs_StateLabelFontSize, 5, 20);

                        updateNodeStyle = false;
                        if(EditorGUI.EndChangeCheck())
                        {
                            RATS.UpdateGraphTextures();
                            updateNodeStyle = true;
                        }

                        using(new GUILayout.HorizontalScope())
                        {
                            ToggleButton(ref prefs_GraphDragNoSnap, "Disable Snapping by Default", "Disable grid snapping (hold Control while dragging for alternate mode)");
                            ToggleButton(ref prefs_GraphDragSnapToModifiedGrid, "Snap to custom grid", "Snaps to user-specified grid");
                        }

                        EditorGUILayout.LabelField("Tip: hold Control while dragging for the opposite of this setting", new GUIStyle("miniLabel"));
                    }
                    break;

                case Tabs.AnimEditor:

                    break;
            }

            // Footer
            using(new GUILayout.VerticalScope())
            {
                DrawUILine();
                using(new GUILayout.HorizontalScope())
                {
                    // Github link button
                    using(new GUILayout.HorizontalScope())
                    {
                        if(githubIcon == null)
                        {
                            // Decode from base64 encoded 16x16 github icon png
                            githubIcon = TextureFromBase64PNG(RATSConstants.GithubLogoBase64);
                        }

                        bool githubLinkClicked = GUILayout.Button(new GUIContent("  View Repo on Github", githubIcon), new GUIStyle("Button"));
                        EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link); // Lights up button with link cursor
                        if (githubLinkClicked) Application.OpenURL(@"https://github.com/rrazgriz/RATS");
                    }
                    
                    // Version & Name
                    using(new GUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("   v" + version + "   •   Razgriz", new GUIStyle("Label"));
                    }
                }
            }

            if (GUI.changed) HandlePreferences();
        }

        public static void OnEnable()
        {
            if(editorWindowIcon == null)
            {
                // Decode from base64 encoded 16x16 github icon png
                Byte[] editorWindowIcon_b64_bytes = System.Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAGXRFWHRTb2Z0d2FyZQBBZG9iZSBJbWFnZVJlYWR5ccllPAAAA2ZpVFh0WE1MOmNvbS5hZG9iZS54bXAAAAAAADw/eHBhY2tldCBiZWdpbj0i77u/IiBpZD0iVzVNME1wQ2VoaUh6cmVTek5UY3prYzlkIj8+IDx4OnhtcG1ldGEgeG1sbnM6eD0iYWRvYmU6bnM6bWV0YS8iIHg6eG1wdGs9IkFkb2JlIFhNUCBDb3JlIDUuMy1jMDExIDY2LjE0NTY2MSwgMjAxMi8wMi8wNi0xNDo1NjoyNyAgICAgICAgIj4gPHJkZjpSREYgeG1sbnM6cmRmPSJodHRwOi8vd3d3LnczLm9yZy8xOTk5LzAyLzIyLXJkZi1zeW50YXgtbnMjIj4gPHJkZjpEZXNjcmlwdGlvbiByZGY6YWJvdXQ9IiIgeG1sbnM6eG1wTU09Imh0dHA6Ly9ucy5hZG9iZS5jb20veGFwLzEuMC9tbS8iIHhtbG5zOnN0UmVmPSJodHRwOi8vbnMuYWRvYmUuY29tL3hhcC8xLjAvc1R5cGUvUmVzb3VyY2VSZWYjIiB4bWxuczp4bXA9Imh0dHA6Ly9ucy5hZG9iZS5jb20veGFwLzEuMC8iIHhtcE1NOk9yaWdpbmFsRG9jdW1lbnRJRD0ieG1wLmRpZDozN0JGMzM3RjEyMkRFRDExQjI2OTkxN0ZBNEFFQjQ2NyIgeG1wTU06RG9jdW1lbnRJRD0ieG1wLmRpZDo3RjI0ODlEMzJEN0MxMUVEOUIwOERDM0I2MjBENkEwOCIgeG1wTU06SW5zdGFuY2VJRD0ieG1wLmlpZDo3RjI0ODlEMjJEN0MxMUVEOUIwOERDM0I2MjBENkEwOCIgeG1wOkNyZWF0b3JUb29sPSJBZG9iZSBQaG90b3Nob3AgQ1M2IChXaW5kb3dzKSI+IDx4bXBNTTpEZXJpdmVkRnJvbSBzdFJlZjppbnN0YW5jZUlEPSJ4bXAuaWlkOjNBQkYzMzdGMTIyREVEMTFCMjY5OTE3RkE0QUVCNDY3IiBzdFJlZjpkb2N1bWVudElEPSJ4bXAuZGlkOjM3QkYzMzdGMTIyREVEMTFCMjY5OTE3RkE0QUVCNDY3Ii8+IDwvcmRmOkRlc2NyaXB0aW9uPiA8L3JkZjpSREY+IDwveDp4bXBtZXRhPiA8P3hwYWNrZXQgZW5kPSJyIj8+4Mj3cQAAAG1JREFUeNpi/P//PwMlgImBQkCxASw4xNH9xUipC/7T3Av/iXEuNnUsRGiCgRw0tWDDGNHSwX8kRf/JiQVGLAbhC0RGFjwK5hBwxWdCYYBPsxYQX0ePBWL9zYgvHTAS0MiIKxAZiTSE+ikRIMAAwHwXKDkCfHcAAAAASUVORK5CYII=");
                editorWindowIcon = new Texture2D(1,1);
                editorWindowIcon.LoadImage(editorWindowIcon_b64_bytes);
            }
            
            HandlePreferences();
        }

        public static void HandlePreferences()
        {
            if(!hasInitializedPreferences) // Need to grab from EditorPrefs
            {
                RATSPreferenceHandler.Load(ref RATS.Prefs);

                prefs_DisableAnimatorGraphFixes = EditorPrefs.GetBool("RATS.prefs_DisableAnimatorGraphFixes", false);

                prefs_StateMotionLabels = EditorPrefs.GetBool("RATS.prefs_StateMotionLabels", true);
                prefs_StateBlendtreeLabels = EditorPrefs.GetBool("RATS.prefs_StateBlendtreeLabels", true);
                prefs_StateAnimIsEmptyLabel = EditorPrefs.GetBool("RATS.prefs_StateAnimIsEmptyLabel", true);
                prefs_StateLoopedLabels = EditorPrefs.GetBool("RATS.prefs_StateLoopedLabels", true);
                prefs_HideOffLabels = EditorPrefs.GetBool("RATS.prefs_HideOffLabels", false);
                prefs_ShowWarningsTopLeft = EditorPrefs.GetBool("RATS.prefs_ShowWarningsTopLeft", false);

                prefs_StateExtraLabelsWD = EditorPrefs.GetBool("RATS.prefs_StateExtraLabelsWD", true);
                prefs_StateExtraLabelsBehavior = EditorPrefs.GetBool("RATS.prefs_StateExtraLabelsBehavior", true);
                prefs_StateExtraLabelsMotionTime = EditorPrefs.GetBool("RATS.prefs_StateExtraLabelsMotionTime", false);
                prefs_StateExtraLabelsSpeed = EditorPrefs.GetBool("RATS.prefs_StateExtraLabelsSpeed", false);

                prefs_GraphGridOverride = EditorPrefs.GetBool("RATS.prefs_GraphGridOverride", false);
                prefs_GraphGridDivisorMinor = EditorPrefs.GetFloat("RATS.prefs_GraphGridDivisorMinor", 10.0f);
                prefs_GraphGridScalingMajor = EditorPrefs.GetFloat("RATS.prefs_GraphGridScalingMajor", 1.0f);
                
                ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString("RATS.prefs_GraphGridBackgroundColor", ColorUtility.ToHtmlStringRGBA(GUI.backgroundColor)), out prefs_GraphGridBackgroundColor);
                ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString("RATS.prefs_GraphGridColorMajor", "0000002e"), out prefs_GraphGridColorMajor);
                ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString("RATS.prefs_GraphGridColorMinor", "00000047"), out prefs_GraphGridColorMinor);
                prefs_GraphDragNoSnap = EditorPrefs.GetBool("RATS.prefs_GraphDragNoSnap", false);
                prefs_GraphDragSnapToModifiedGrid = EditorPrefs.GetBool("RATS.prefs_GraphDragSnapToModifiedGrid", false);
                ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString("RATS.prefs_StateTextColor", "E5E5E5FF"), out prefs_StateTextColor);
                ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString("RATS.prefs_StateColorGray", "4D4D4DFF"), out prefs_StateColorGray);
                ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString("RATS.prefs_StateColorOrange", "C66025FF"), out prefs_StateColorOrange);
                ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString("RATS.prefs_StateColorAqua", "389496FF"), out prefs_StateColorAqua);
                ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString("RATS.prefs_StateColorGreen", "117733FF"), out prefs_StateColorGreen);
                ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString("RATS.prefs_StateColorRed", "AA051EFF"), out prefs_StateColorRed);
                prefs_StateLabelFontSize = EditorPrefs.GetInt("RATS.prefs_StateLabelFontSize", 12);

                prefs_NewStateWriteDefaults = EditorPrefs.GetBool("RATS.prefs_NewStateWriteDefaults", false);
                prefs_NewLayersWeight1 = EditorPrefs.GetBool("RATS.prefs_NewLayersWeight1", true);
                prefs_NewTransitionsZeroTime = EditorPrefs.GetBool("RATS.prefs_NewTransitionsZeroTime", true);
                prefs_NewTransitionsExitTime = EditorPrefs.GetBool("RATS.prefs_NewTransitionsExitTime", false);

                prefs_AnimationWindowShowActualPropertyNames = EditorPrefs.GetBool("RATS.prefs_AnimationWindowShowActualPropertyNames", false);
                prefs_AnimationWindowShowFullPath = EditorPrefs.GetBool("RATS.prefs_AnimationWindowShowFullPath", false);
                prefs_AnimationWindowTrimActualNames = EditorPrefs.GetBool("RATS.prefs_AnimationWindowTrimActualNames", false);
                prefs_AnimationWindowIndentScale = EditorPrefs.GetFloat("RATS.prefs_AnimationWindowIndentScale", 1.0f);

                hasInitializedPreferences = true;
            }
            else // Already grabbed, set them instead
            {
                EditorPrefs.SetBool("RATS.prefs_DisableAnimatorGraphFixes", prefs_DisableAnimatorGraphFixes);

                EditorPrefs.SetBool("RATS.prefs_StateMotionLabels", prefs_StateMotionLabels);
                EditorPrefs.SetBool("RATS.prefs_StateBlendtreeLabels", prefs_StateBlendtreeLabels);
                EditorPrefs.SetBool("RATS.prefs_StateAnimIsEmptyLabel", prefs_StateAnimIsEmptyLabel);
                EditorPrefs.SetBool("RATS.prefs_StateLoopedLabels", prefs_StateLoopedLabels);
                EditorPrefs.SetBool("RATS.prefs_HideOffLabels", prefs_HideOffLabels);
                EditorPrefs.SetBool("RATS.prefs_ShowWarningsTopLeft", prefs_ShowWarningsTopLeft);

                EditorPrefs.SetBool("RATS.prefs_StateExtraLabelsWD", prefs_StateExtraLabelsWD);
                EditorPrefs.SetBool("RATS.prefs_StateExtraLabelsBehavior", prefs_StateExtraLabelsBehavior);
                EditorPrefs.SetBool("RATS.prefs_StateExtraLabelsMotionTime", prefs_StateExtraLabelsMotionTime);
                EditorPrefs.SetBool("RATS.prefs_StateExtraLabelsSpeed", prefs_StateExtraLabelsSpeed);

                EditorPrefs.SetBool("RATS.prefs_GraphGridOverride", prefs_GraphGridOverride);
                EditorPrefs.SetFloat("RATS.prefs_GraphGridScalingMajor", prefs_GraphGridScalingMajor);
                EditorPrefs.SetFloat("RATS.prefs_GraphGridDivisorMinor", prefs_GraphGridDivisorMinor);
                EditorPrefs.SetString("RATS.prefs_GraphGridBackgroundColor", ColorUtility.ToHtmlStringRGBA(prefs_GraphGridBackgroundColor));
                EditorPrefs.SetString("RATS.prefs_GraphGridColorMajor", ColorUtility.ToHtmlStringRGBA(prefs_GraphGridColorMajor));
                EditorPrefs.SetString("RATS.prefs_GraphGridColorMinor", ColorUtility.ToHtmlStringRGBA(prefs_GraphGridColorMinor));
                EditorPrefs.SetBool("RATS.prefs_GraphDragNoSnap", prefs_GraphDragNoSnap);
                EditorPrefs.SetBool("RATS.prefs_GraphDragSnapToModifiedGrid", prefs_GraphDragSnapToModifiedGrid);
                EditorPrefs.SetString("RATS.prefs_StateTextColor", ColorUtility.ToHtmlStringRGBA(prefs_StateTextColor));
                EditorPrefs.SetString("RATS.prefs_StateColorGray", ColorUtility.ToHtmlStringRGBA(prefs_StateColorGray));
                EditorPrefs.SetString("RATS.prefs_StateColorOrange", ColorUtility.ToHtmlStringRGBA(prefs_StateColorOrange));
                EditorPrefs.SetString("RATS.prefs_StateColorAqua", ColorUtility.ToHtmlStringRGBA(prefs_StateColorAqua));
                EditorPrefs.SetString("RATS.prefs_StateColorGreen", ColorUtility.ToHtmlStringRGBA(prefs_StateColorGreen));
                EditorPrefs.SetString("RATS.prefs_StateColorRed", ColorUtility.ToHtmlStringRGBA(prefs_StateColorRed));
                EditorPrefs.SetInt("RATS.prefs_StateLabelFontSize", prefs_StateLabelFontSize);

                EditorPrefs.SetBool("RATS.prefs_NewStateWriteDefaults", prefs_NewStateWriteDefaults);
                EditorPrefs.SetBool("RATS.prefs_NewLayersWeight1", prefs_NewLayersWeight1);
                EditorPrefs.SetBool("RATS.prefs_NewTransitionsZeroTime", prefs_NewTransitionsZeroTime);
                EditorPrefs.SetBool("RATS.prefs_NewTransitionsExitTime", prefs_NewTransitionsExitTime);

                EditorPrefs.SetBool("RATS.prefs_AnimationWindowShowActualPropertyNames", prefs_AnimationWindowShowActualPropertyNames);
                EditorPrefs.SetBool("RATS.prefs_AnimationWindowShowFullPath", prefs_AnimationWindowShowFullPath);
                EditorPrefs.SetBool("RATS.prefs_AnimationWindowTrimActualNames", prefs_AnimationWindowTrimActualNames);
                EditorPrefs.SetFloat("RATS.prefs_AnimationWindowIndentScale", prefs_AnimationWindowIndentScale);

                RATSPreferenceHandler.Save(RATS.Prefs);
            }
        }

        // Helper Functions
        public static void SectionLabel(string label) { SectionLabel(new GUIContent(label)); }
        public static void SectionLabel(GUIContent label)
        {
            EditorGUILayout.LabelField(label, new GUIStyle("BoldLabel"));
        }


        public static void ToggleButton(ref bool param, string label, string tooltip="") { ToggleButton(ref param, new GUIContent(label, tooltip)); }
        public static void ToggleButton(ref bool param, GUIContent label)
        {
            if(ToggleButtonStyle == null)
            {
                ToggleButtonStyle = new GUIStyle("Label");
                ToggleButtonStyle.richText = true;
            }

            param = EditorGUILayout.ToggleLeft(label, param, ToggleButtonStyle);
        }

        public static void DrawUILine() { DrawUILine(new Color(0.5f, 0.5f, 0.5f, 1.0f)); }
        public static void DrawUILine(int thickness) { DrawUILine(new Color(0.5f, 0.5f, 0.5f, 1.0f), thickness); }
        public static void DrawUILine(Color color, int thickness = 1, int padding = 10)
        {
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding+thickness));
            EditorGUI.DrawRect(new Rect(r.x - 2, r.y + padding/2, r.width + 6, thickness), color);
        }

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
        public static void SetDefineSymbol(string symbol, bool active) { SetDefineSymbol(symbol, active, true); }
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