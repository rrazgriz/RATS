// Some Harmony based Unity animator window patches to help workflow
// Original by Dj Lukis.LT, under MIT License

// Copyright (c) 2021 Razgriz
// SPDX-License-Identifier: MIT

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using ReorderableList = UnityEditorInternal.ReorderableList;
using Razgriz.AnimatorExtensions.HarmonyLib; // using HarmonyLib; // if using release 0Harmony.dll

namespace Razgriz.AnimatorExtensions
{
	public class AnimatorExtensionsGUI : EditorWindow
	{
		public const string version = "2022.09.01";

		public static bool prefs_DisableAnimatorGraphFixes;

		public static bool prefs_StateMotionLabels;
		public static bool prefs_StateBlendtreeLabels;
		public static bool prefs_StateAnimIsEmptyLabel;
		public static bool prefs_StateLoopedLabels;
		public static bool prefs_HideOffLabels;

		public static bool prefs_StateExtraLabelsWD;
		public static bool prefs_StateExtraLabelsBehavior;
		public static bool prefs_StateExtraLabelsMotionTime;
		public static bool prefs_StateExtraLabelsSpeed;

		public static bool prefs_NewStateWriteDefaults;
		public static bool prefs_NewLayersWeight1;
		public static bool prefs_NewTransitionsZeroTime;
		public static bool prefs_NewTransitionsExitTime;

		public static bool prefs_AnimationWindowShowActualPropertyNames;
		public static float prefs_AnimationWindowIndentScale;
		
		public static bool prefs_AnimationWindowShowFullPath;
		public static bool prefs_AnimationWindowTrimActualNames;

		public static bool hasInitializedPreferences = false;

		private static Texture2D githubIcon;
		static GUIStyle ToggleButtonStyle;

		[MenuItem("Tools/AnimatorExtensions")]
		public static void ShowWindow()
		{
			EditorWindow.GetWindow<AnimatorExtensionsGUI>("Animator Extensions");
		}

		void OnGUI()
		{
			if(!hasInitializedPreferences) HandlePreferences();

			// Graph Labels
			using(new GUILayout.VerticalScope())
			{
				SectionLabel(new GUIContent("  Animator Graph Labels", EditorGUIUtility.IconContent("d_AnimatorController Icon").image));

				using(new GUILayout.HorizontalScope())
				{
					ToggleButton(ref prefs_StateLoopedLabels, new GUIContent("   Loop Time", EditorGUIUtility.IconContent("d_preAudioLoopOff@2x").image, "Show an icon when a state's animation is set to Loop Time"));
					ToggleButton(ref prefs_StateBlendtreeLabels, new GUIContent("   Blendtrees", EditorGUIUtility.IconContent("d_BlendTree Icon").image, "Show an icon when a state's motion is a Blendtree"));
				}
				using(new GUILayout.HorizontalScope())
				{
					ToggleButton(ref prefs_StateMotionLabels, "<b>Tt</b>    Motion Names", "Show the name of the state's clip/blendtree");
					ToggleButton(ref prefs_StateAnimIsEmptyLabel, new GUIContent("   Empty Anims/States", EditorGUIUtility.IconContent("Warning").image, "Display a warning if a state's animation is empty or if a state has no motion"));
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

				ToggleButton(ref prefs_HideOffLabels, "Hide Labels Completely", "Hide Labels when condition is false, instead of dimming");
				// ToggleButton(ref prefs_StateMotionLabels, new GUIContent("  Motion Names", EditorGUIUtility.IconContent("d_Font Icon").image));

				using(new GUILayout.HorizontalScope())
				{
					EditorGUILayout.LabelField("Tip: Hold ALT to see all labels at any time", new GUIStyle("miniLabel"));
				}
			}

			// Graph/State Defaults
			using(new GUILayout.VerticalScope())
			{
				DrawUILine();
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
				prefs_AnimationWindowIndentScale = Mathf.Floor(prefs_AnimationWindowIndentScale * 10f)/10f;

				EditorGUILayout.LabelField("When disabling these options, click on a different animation to refresh", new GUIStyle("miniLabel"));
			}

			// Disable Patch Categories
			using(new GUILayout.VerticalScope())
			{
				DrawUILine();
				EditorGUI.BeginChangeCheck();
				ToggleButton(ref prefs_DisableAnimatorGraphFixes, "Disable Animator Graph Fixes (Requires tab out/in)", "Allows other utilities to patch Controller editor window");
				if(EditorGUI.EndChangeCheck())
				{
					if(prefs_DisableAnimatorGraphFixes)
					{
						prefs_StateMotionLabels = false;
						prefs_StateExtraLabelsWD = false;
						prefs_StateExtraLabelsBehavior = false;
						prefs_StateExtraLabelsSpeed = false;
						prefs_StateExtraLabelsMotionTime = false;

						prefs_NewStateWriteDefaults = false;
						prefs_NewLayersWeight1 = false;
						prefs_NewTransitionsExitTime = false;
						prefs_NewTransitionsZeroTime = false;

						HandlePreferences();
					}

					// Try to force recompilation
					UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
					AssetDatabase.Refresh();
				}
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
							Byte[] githubicon_b64_bytes = System.Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAM/aVRYdFhNTDpjb20uYWRvYmUueG1wAAAAAAA8P3hwYWNrZXQgYmVnaW49Iu+7vyIgaWQ9Ilc1TTBNcENlaGlIenJlU3pOVGN6a2M5ZCI/Pg0KPHg6eG1wbWV0YSB4bWxuczp4PSJhZG9iZTpuczptZXRhLyIgeDp4bXB0az0iQWRvYmUgWE1QIENvcmUgNS41LWMwMjEgNzkuMTU0OTExLCAyMDEzLzEwLzI5LTExOjQ3OjE2ICAgICAgICAiPg0KICA8cmRmOlJERiB4bWxuczpyZGY9Imh0dHA6Ly93d3cudzMub3JnLzE5OTkvMDIvMjItcmRmLXN5bnRheC1ucyMiPg0KICAgIDxyZGY6RGVzY3JpcHRpb24gcmRmOmFib3V0PSIiIHhtbG5zOnhtcE1NPSJodHRwOi8vbnMuYWRvYmUuY29tL3hhcC8xLjAvbW0vIiB4bWxuczpzdFJlZj0iaHR0cDovL25zLmFkb2JlLmNvbS94YXAvMS4wL3NUeXBlL1Jlc291cmNlUmVmIyIgeG1sbnM6eG1wPSJodHRwOi8vbnMuYWRvYmUuY29tL3hhcC8xLjAvIiB4bXBNTTpEb2N1bWVudElEPSJ4bXAuZGlkOkREQjFCMDlGODZDRTExRTNBQTUyRUUzMzUyRDFCQzQ2IiB4bXBNTTpJbnN0YW5jZUlEPSJ4bXAuaWlkOkREQjFCMDlFODZDRTExRTNBQTUyRUUzMzUyRDFCQzQ2IiB4bXA6Q3JlYXRvclRvb2w9IkFkb2JlIFBob3Rvc2hvcCBDUzYgKE1hY2ludG9zaCkiPg0KICAgICAgPHhtcE1NOkRlcml2ZWRGcm9tIHN0UmVmOmluc3RhbmNlSUQ9InhtcC5paWQ6RTUxNzhBMkE5OUEwMTFFMjlBMTVCQzEwNDZBODkwNEQiIHN0UmVmOmRvY3VtZW50SUQ9InhtcC5kaWQ6RTUxNzhBMkI5OUEwMTFFMjlBMTVCQzEwNDZBODkwNEQiIC8+DQogICAgPC9yZGY6RGVzY3JpcHRpb24+DQogIDwvcmRmOlJERj4NCjwveDp4bXBtZXRhPg0KPD94cGFja2V0IGVuZD0iciI/Piwf/8sAAAEGSURBVDhPnZKxSgNBFEUnErAKSkCxkjR+gAnpUvsVdgp+gP9jLwGxtxJs0tqkCSlSRWyUIBZR1nNn5oWXMcbogbNz9+3OY2d2QlVVpde4ijke49L7/kYPN2GM3xp08C+84lID4xS3cYozvMdHFGfo373C2OAm3ia6aE1/0hMbeJpYTiidoHFR0zUkPrGe4lp28CXFMPQNRniU4q/YnOAbiFoe17GPTymG2VYOxiZLuMuj6GtT3vQVjgMsN87so6eh4mHK1SUOUoxoeTbxVoWCd1wcpAdVQFkH5iRncw9L6hhsD3qov6ANbWIbPfM8Gi38iEldnOdo+PquCvCMfmnxN8ZG/yOEL3WYSWaiIjkIAAAAAElFTkSuQmCC");
							githubIcon = new Texture2D(1,1);		
							githubIcon.LoadImage(githubicon_b64_bytes);
						}

						bool githubLinkClicked = GUILayout.Button(new GUIContent("  View Repo on Github", githubIcon), new GUIStyle("Button"));
						EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link); // Lights up button with link cursor
						if (githubLinkClicked) Application.OpenURL(@"https://github.com/rrazgriz/AnimatorExtensions");
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
			HandlePreferences();
		}

		public static void HandlePreferences()
		{
			if(!hasInitializedPreferences) // Need to grab from registry
			{
				prefs_DisableAnimatorGraphFixes = EditorPrefs.GetBool("AnimatorExtensions.prefs_DisableAnimatorGraphFixes", false);

				prefs_StateMotionLabels = EditorPrefs.GetBool("AnimatorExtensions.prefs_StateMotionLabels", true);
				prefs_StateBlendtreeLabels = EditorPrefs.GetBool("AnimatorExtensions.prefs_StateBlendtreeLabels", true);
				prefs_StateAnimIsEmptyLabel = EditorPrefs.GetBool("AnimatorExtensions.prefs_StateAnimIsEmptyLabel", true);
				prefs_StateLoopedLabels = EditorPrefs.GetBool("AnimatorExtensions.prefs_StateLoopedLabels", true);
				prefs_HideOffLabels = EditorPrefs.GetBool("AnimatorExtensions.prefs_HideOffLabels", false);

				prefs_StateExtraLabelsWD = EditorPrefs.GetBool("AnimatorExtensions.prefs_StateExtraLabelsWD", true);
				prefs_StateExtraLabelsBehavior = EditorPrefs.GetBool("AnimatorExtensions.prefs_StateExtraLabelsBehavior", true);
				prefs_StateExtraLabelsMotionTime = EditorPrefs.GetBool("AnimatorExtensions.prefs_StateExtraLabelsMotionTime", false);
				prefs_StateExtraLabelsSpeed = EditorPrefs.GetBool("AnimatorExtensions.prefs_StateExtraLabelsSpeed", false);

				prefs_NewStateWriteDefaults = EditorPrefs.GetBool("AnimatorExtensions.prefs_NewStateWriteDefaults", false);
				prefs_NewLayersWeight1 = EditorPrefs.GetBool("AnimatorExtensions.prefs_NewLayersWeight1", true);
				prefs_NewTransitionsZeroTime = EditorPrefs.GetBool("AnimatorExtensions.prefs_NewTransitionsZeroTime", true);
				prefs_NewTransitionsExitTime = EditorPrefs.GetBool("AnimatorExtensions.prefs_NewTransitionsExitTime", false);

				prefs_AnimationWindowShowActualPropertyNames = EditorPrefs.GetBool("AnimatorExtensions.prefs_AnimationWindowShowActualPropertyNames", false);
				prefs_AnimationWindowShowFullPath = EditorPrefs.GetBool("AnimatorExtensions.prefs_AnimationWindowShowFullPath", false);
				prefs_AnimationWindowTrimActualNames = EditorPrefs.GetBool("AnimatorExtensions.prefs_AnimationWindowTrimActualNames", false);
				prefs_AnimationWindowIndentScale = EditorPrefs.GetFloat("AnimatorExtensions.prefs_AnimationWindowIndentScale", 1.0f);

				hasInitializedPreferences = true;
			}
			else // Already grabbed, set them instead
			{
				EditorPrefs.SetBool("AnimatorExtensions.prefs_DisableAnimatorGraphFixes", prefs_DisableAnimatorGraphFixes);

				EditorPrefs.SetBool("AnimatorExtensions.prefs_StateMotionLabels", prefs_StateMotionLabels);
				EditorPrefs.SetBool("AnimatorExtensions.prefs_StateBlendtreeLabels", prefs_StateBlendtreeLabels);
				EditorPrefs.SetBool("AnimatorExtensions.prefs_StateAnimIsEmptyLabel", prefs_StateAnimIsEmptyLabel);
				EditorPrefs.SetBool("AnimatorExtensions.prefs_StateLoopedLabels", prefs_StateLoopedLabels);
				EditorPrefs.SetBool("AnimatorExtensions.prefs_HideOffLabels", prefs_HideOffLabels);

				EditorPrefs.SetBool("AnimatorExtensions.prefs_StateExtraLabelsWD", prefs_StateExtraLabelsWD);
				EditorPrefs.SetBool("AnimatorExtensions.prefs_StateExtraLabelsBehavior", prefs_StateExtraLabelsBehavior);
				EditorPrefs.SetBool("AnimatorExtensions.prefs_StateExtraLabelsMotionTime", prefs_StateExtraLabelsMotionTime);
				EditorPrefs.SetBool("AnimatorExtensions.prefs_StateExtraLabelsSpeed", prefs_StateExtraLabelsSpeed);

				EditorPrefs.SetBool("AnimatorExtensions.prefs_NewStateWriteDefaults", prefs_NewStateWriteDefaults);
				EditorPrefs.SetBool("AnimatorExtensions.prefs_NewLayersWeight1", prefs_NewLayersWeight1);
				EditorPrefs.SetBool("AnimatorExtensions.prefs_NewTransitionsZeroTime", prefs_NewTransitionsZeroTime);
				EditorPrefs.SetBool("AnimatorExtensions.prefs_NewTransitionsExitTime", prefs_NewTransitionsExitTime);

				EditorPrefs.SetBool("AnimatorExtensions.prefs_AnimationWindowShowActualPropertyNames", prefs_AnimationWindowShowActualPropertyNames);
				EditorPrefs.SetBool("AnimatorExtensions.prefs_AnimationWindowShowFullPath", prefs_AnimationWindowShowFullPath);
				EditorPrefs.SetBool("AnimatorExtensions.prefs_AnimationWindowTrimActualNames", prefs_AnimationWindowTrimActualNames);
				EditorPrefs.SetFloat("AnimatorExtensions.prefs_AnimationWindowIndentScale", prefs_AnimationWindowIndentScale);
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
	}

	[InitializeOnLoad]
	public class AnimatorExtensions
	{
		public static Harmony harmonyInstance = new Harmony("Razgriz.AnimatorExtensions");

		static AnimatorExtensions()
		{
			AnimatorExtensionsGUI.HandlePreferences();			
			harmonyInstance.PatchAll();
		}

	#region Patches

		#region BugFixes
			// Prevent scroll position reset when rearranging or editing layers
			private static Vector2 _layerScrollCache;
			[HarmonyPatch]
			class PatchLayerScrollReset
			{
				[HarmonyPrepare] static bool Prepare(MethodBase original) { return (!AnimatorExtensionsGUI.prefs_DisableAnimatorGraphFixes); } 

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
				[HarmonyPrepare] static bool Prepare(MethodBase original) { return (!AnimatorExtensionsGUI.prefs_DisableAnimatorGraphFixes); } 

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
				[HarmonyPrepare] static bool Prepare(MethodBase original) { return (!AnimatorExtensionsGUI.prefs_DisableAnimatorGraphFixes); } 

				[HarmonyTargetMethod]
				static MethodBase TargetMethod() => typeof(Unsupported).GetMethod("PasteToStateMachineFromPasteboard", BindingFlags.Static | BindingFlags.Public);//AccessTools.Method(typeof(Unsupported), "PasteToStateMachineFromPasteboard");

				[HarmonyPostfix]
				static void Postfix(
					AnimatorStateMachine sm,
					AnimatorController controller,
					int layerIndex,
					Vector3 position)
				{
					//Debug.Log("Yeeet: "+Undo.GetCurrentGroupName());
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
				[HarmonyPrepare] static bool Prepare(MethodBase original) { return (!AnimatorExtensionsGUI.prefs_DisableAnimatorGraphFixes); } 

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
											Debug.Log("Restored transition condition mode");
										}
										// int->float has restrictions
										else if ((ConditionParamType_pre == AnimatorControllerParameterType.Int) && (param.type == AnimatorControllerParameterType.Float))
										{
											AnimatorConditionMode premode = (AnimatorConditionMode)ConditionMode_pre;
											if ((premode != AnimatorConditionMode.Equals) && (premode != AnimatorConditionMode.NotEqual))
											{
												m_ConditionMode.intValue = ConditionMode_pre;
												Debug.Log("Restored transition condition mode 2");
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

		#endregion ExtraStuff

		#region GraphFeatures
			// Set Default Transition Duration/Exit Time
			[HarmonyPatch]
			class PatchAnimatorNewTransitionDefaults
			{
				[HarmonyPrepare] static bool Prepare(MethodBase original) { return (!AnimatorExtensionsGUI.prefs_DisableAnimatorGraphFixes); } 

				[HarmonyTargetMethod]
				static MethodBase TargetMethod() => AccessTools.Method(AnimatorStateType, "CreateTransition");

				[HarmonyPostfix]
				static void Postfix(ref AnimatorStateTransition __result)
				{
					if(AnimatorExtensionsGUI.prefs_NewTransitionsZeroTime)
					{
						__result.duration = 0.0f;
						__result.exitTime = 0.0f;
					}
		
					__result.hasExitTime = AnimatorExtensionsGUI.prefs_NewTransitionsExitTime;
				}
			}

			// Write Defaults Default State
			[HarmonyPatch]
			class PatchAnimatorNewStateDefaults
			{
				[HarmonyPrepare] static bool Prepare(MethodBase original) { return (!AnimatorExtensionsGUI.prefs_DisableAnimatorGraphFixes); } 

				[HarmonyTargetMethod]
				static MethodBase TargetMethod() => AccessTools.Method(AnimatorStateMachineType, "AddState", new Type[] {typeof(AnimatorState), typeof(Vector3)});

				[HarmonyPrefix]
				static void Prefix(ref AnimatorState state, Vector3 position)
				{
					if(!AnimatorExtensionsGUI.prefs_NewStateWriteDefaults) state.writeDefaultValues = false;
				}
			}

			// Controller asset pinging/selection via bottom bar
			[HarmonyPatch]
			class PatchAnimatorBottomBarPingAsset
			{
				[HarmonyPrepare] static bool Prepare(MethodBase original) { return (!AnimatorExtensionsGUI.prefs_DisableAnimatorGraphFixes); } 

				[HarmonyTargetMethod]
				static MethodBase TargetMethod() => AccessTools.Method(AnimatorWindowType, "DoGraphBottomBar");

				[HarmonyPostfix]
				static void Postfix(object __instance, Rect nameRect)
				{
					UnityEngine.Object ctrl = (UnityEngine.Object)AnimatorControllerGetter.Invoke(__instance, null);
					if (ctrl != (UnityEngine.Object)null)
					{
						EditorGUIUtility.AddCursorRect(nameRect, MouseCursor.Link); // "I'm clickable!"

						Event current = Event.current;
						if (((current.type == EventType.MouseDown) && (current.button == 0)) && nameRect.Contains(current.mousePosition))
						{
							EditorGUIUtility.PingObject(ctrl);
							if (current.clickCount == 2) // Adhere to the 'select only on double click' convention
								Selection.activeObject = ctrl;
							current.Use();
						}
					}
				}
			}

			// Show motion name and extra details on state graph nodes
			static bool prefs_DimOffLabels_last = false;
			[HarmonyPatch]
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
					if (StateMotionStyle == null || (prefs_DimOffLabels_last != AnimatorExtensionsGUI.prefs_HideOffLabels))
					{
						StateExtrasStyle = new GUIStyle(EditorStyles.label);
						StateExtrasStyle.alignment = TextAnchor.UpperRight;
						StateExtrasStyle.fontStyle = FontStyle.Bold;

						StateExtrasStyleActive = new GUIStyle(EditorStyles.label);
						StateExtrasStyleActive.alignment = TextAnchor.UpperRight;
						StateExtrasStyleActive.fontStyle = FontStyle.Bold;
						StateExtrasStyleActive.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1f);

						StateExtrasStyleInactive = new GUIStyle(EditorStyles.label);
						StateExtrasStyleInactive.alignment = TextAnchor.UpperRight;
						StateExtrasStyleInactive.fontStyle = FontStyle.Bold;
						float inactiveExtrasTextAlpha = AnimatorExtensionsGUI.prefs_HideOffLabels ? 0.0f : 0.5f;
						StateExtrasStyleInactive.normal.textColor = new Color(0.5f, 0.5f, 0.5f, inactiveExtrasTextAlpha);

						StateMotionStyle = new GUIStyle(EditorStyles.miniBoldLabel);
						StateMotionStyle.fontSize = 10;
						StateMotionStyle.alignment = TextAnchor.LowerCenter;
						StateMotionStyle.margin = new RectOffset(0,0,0,50);

						StateBlendtreeStyle = new GUIStyle(EditorStyles.label);
						StateBlendtreeStyle.alignment = TextAnchor.UpperLeft;
						StateBlendtreeStyle.fontStyle = FontStyle.Bold;
					}

					prefs_DimOffLabels_last = AnimatorExtensionsGUI.prefs_HideOffLabels;
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

						int off1 = (debugShowLabels || (AnimatorExtensionsGUI.prefs_StateExtraLabelsWD && AnimatorExtensionsGUI.prefs_StateExtraLabelsBehavior)) ? 15 : 0;
						int off2 = (debugShowLabels || (AnimatorExtensionsGUI.prefs_StateExtraLabelsMotionTime && AnimatorExtensionsGUI.prefs_StateExtraLabelsSpeed)) ? 15 : 0;

						Rect wdLabelRect 			= new Rect(stateRect.x - off1, stateRect.y - 30, stateRect.width, 15);
						Rect behaviorLabelRect 		= new Rect(stateRect.x, 	   stateRect.y - 30, stateRect.width, 15);

						Rect motionTimeLabelRect 	= new Rect(stateRect.x, 	   stateRect.y - 15, stateRect.width, 15);
						Rect speedLabelRect 		= new Rect(stateRect.x - off2, stateRect.y - 15, stateRect.width, 15);

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
						if(isLoopTime && (debugShowLabels || AnimatorExtensionsGUI.prefs_StateLoopedLabels))
						{
							Rect loopingLabelRect = new Rect(stateRect.x + 1, stateRect.y - 29, 16, 16);
							EditorGUI.LabelField(loopingLabelRect, new GUIContent(EditorGUIUtility.IconContent("d_preAudioLoopOff@2x").image, "Animation Clip is Looping"));
							iconOffset += 14;
						}

						// Blendtree label
						if(hasblendtree && (debugShowLabels || AnimatorExtensionsGUI.prefs_StateBlendtreeLabels))
						{
							Rect blendtreeLabelRect = new Rect(stateRect.x + 1 + iconOffset, stateRect.y - 29, 14, 14);
							EditorGUI.LabelField(blendtreeLabelRect, new GUIContent(EditorGUIUtility.IconContent("d_BlendTree Icon").image, "State Contains a Blendtree"));
							iconOffset += 14;
						}

						// Empty Animation/State Warning
						Rect emptyWarningRect = new Rect(stateRect.x + iconOffset + 1, stateRect.y - 28, 14, 14);
						if((debugShowLabels || AnimatorExtensionsGUI.prefs_StateAnimIsEmptyLabel))
						{
							if(isEmptyAnim) EditorGUI.LabelField(emptyWarningRect, new GUIContent(EditorGUIUtility.IconContent("Warning@2x").image, "Animation Clip has no Keyframes"));
							else if(isEmptyState) EditorGUI.LabelField(emptyWarningRect, new GUIContent(EditorGUIUtility.IconContent("Error@2x").image, "State has no Motion assigned"));
						}

						if(hasMotion && (debugShowLabels || AnimatorExtensionsGUI.prefs_StateExtraLabelsWD)) EditorGUI.LabelField(wdLabelRect, "WD", (isWD ? StateExtrasStyleActive : StateExtrasStyleInactive));
						if(				(debugShowLabels || AnimatorExtensionsGUI.prefs_StateExtraLabelsBehavior)) EditorGUI.LabelField(behaviorLabelRect, "B", (hasBehavior ? StateExtrasStyleActive : StateExtrasStyleInactive));
						if(hasMotion && (debugShowLabels || AnimatorExtensionsGUI.prefs_StateExtraLabelsMotionTime)) EditorGUI.LabelField(motionTimeLabelRect, "M", (hasMotionTime ? StateExtrasStyleActive : StateExtrasStyleInactive));
						if(hasMotion && (debugShowLabels || AnimatorExtensionsGUI.prefs_StateExtraLabelsSpeed)) EditorGUI.LabelField(speedLabelRect, "S", (hasSpeedParam ? StateExtrasStyleActive : StateExtrasStyleInactive));
					}

					if(hasMotion)
					{

					}

					// Name of Motion (btree or animation clip) at bottom
					// TODO? overlaps progress bar in play mode
					if (hasMotion && (debugShowLabels || AnimatorExtensionsGUI.prefs_StateMotionLabels))
					{
						string motionname = "[None]";
						if (aState.motion) motionname = "[" + aState.motion.name + "]";
						// if (astate.motion) motionname = "<" + astate.motion.name + ">";
						Rect motionlabelrect = new Rect(stateRect.x, stateRect.y - 10, stateRect.width, 20);
						EditorGUI.LabelField(motionlabelrect, motionname, StateMotionStyle);
					}

					Event current = Event.current;
					if (current.type == EventType.KeyDown && current.keyCode == KeyCode.F2)
					{
						Debug.Log("TODO: Rename");
					}
				}
			}

		#endregion Graphstuff

		#region LayerFeatures
			// Default Layer Weight = 1
			[HarmonyPatch]
			class PatchLayerWeightDefault
			{
				[HarmonyPrepare] static bool Prepare(MethodBase original) { return (!AnimatorExtensionsGUI.prefs_DisableAnimatorGraphFixes); } 

				[HarmonyTargetMethod]
				static MethodBase TargetMethod() => AccessTools.Method(AnimatorControllerType, "AddLayer", new Type[] {typeof(AnimatorControllerLayer)});

				[HarmonyPrefix]
				static void Prefix(ref AnimatorControllerLayer layer)
				{
						layer.defaultWeight = AnimatorExtensionsGUI.prefs_NewLayersWeight1 ? 1.0f : 0.0f;
				}
			}

			// Layer copy-pasting
			private static AnimatorControllerLayer _layerClipboard = null;
			private static AnimatorController _controllerClipboard = null;
			[HarmonyPatch]
			class PatchLayerCopyPaste
			{
				[HarmonyPrepare] static bool Prepare(MethodBase original) { return (!AnimatorExtensionsGUI.prefs_DisableAnimatorGraphFixes); } 

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
							new GenericMenu.MenuFunction2(AnimatorExtensions.CopyLayer), __instance);
						if (_layerClipboard != null)
						{
							menu.AddItem(EditorGUIUtility.TrTextContent("Paste layer", null, (Texture) null), false,
								new GenericMenu.MenuFunction2(AnimatorExtensions.PasteLayer), __instance);
							menu.AddItem(EditorGUIUtility.TrTextContent("Paste layer settings", null, (Texture) null), false,
								new GenericMenu.MenuFunction2(AnimatorExtensions.PasteLayerSettings), __instance);
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
				[HarmonyPrepare] static bool Prepare(MethodBase original) { return (!AnimatorExtensionsGUI.prefs_DisableAnimatorGraphFixes); } 

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

		#endregion Layerstuff

		#region AnimationWindowFeatures
			[HarmonyPatch]
			class PatchAnimationWindowHierarchy
			{
				[HarmonyTargetMethod]
				static MethodBase TargetMethod() => AccessTools.Method(AnimationWindowHierarchyGUIType, "DoNodeGUI");

				[HarmonyPrefix]
				static void Prefix(object __instance, Rect rect, object node, bool selected, bool focused, int row)
				{

					var theNode = node;

					string propertyPath = (string)NodeTypePath.GetValue(node);
					if (string.IsNullOrEmpty(propertyPath)) propertyPath = "";
					string displayName = (string)NodeDisplayNameProp.GetValue(node);
					string propertyName = (string)NodeTypePropertyName.GetValue(node);
					Type animatableObjectType = (Type)NodeTypeAnimatableObjectType.GetValue(node);
					string componentPrefix = "";

					if(!string.IsNullOrEmpty(propertyName) && AnimatorExtensionsGUI.prefs_AnimationWindowTrimActualNames) propertyName = propertyName.Replace("m_", "");

					if(animatableObjectType != null)
					{
						componentPrefix = (animatableObjectType).ToString().Split('.').Last() + ".";
					}

					string displayNameString = AnimatorExtensionsGUI.prefs_AnimationWindowShowActualPropertyNames ? componentPrefix + propertyName : displayName;//GetUnityDisplayName(propertyName);

					NodeTypeIndent.SetValue(node, (int)(AnimatorExtensionsGUI.prefs_AnimationWindowIndentScale * ((float)propertyPath.Split('/').Length)) );
					
					NodeDisplayNameProp.SetValue(node, displayNameString);
				}
			}

			[HarmonyPatch]
			class PatchAnimationWindowPaths
			{
				[HarmonyTargetMethod]
				static MethodBase TargetMethod() => AccessTools.Method(AnimationWindowHierarchyGUIType, "GetGameObjectName");

				[HarmonyPostfix]
				static string Postfix(string __result, GameObject rootGameObject, string path)
				{
					if(AnimatorExtensionsGUI.prefs_AnimationWindowShowFullPath)
					{
						if (string.IsNullOrEmpty(path)) return rootGameObject != null ? rootGameObject.name : "";

						return path;
					}
					else
					{
						return __result;
					}
				}
			}

		#endregion

	#endregion

	#region Utility

		#region HelperFunctions

			// Recursive helper functions to gather deeply-nested parameter references
			private static void GatherBtParams(BlendTree bt, ref Dictionary<string, AnimatorControllerParameter> srcparams, ref Dictionary<string, AnimatorControllerParameter> queuedparams)
			{
				if (srcparams.ContainsKey(bt.blendParameter))
					queuedparams[bt.blendParameter] = srcparams[bt.blendParameter];
				if (srcparams.ContainsKey(bt.blendParameterY))
					queuedparams[bt.blendParameterY] = srcparams[bt.blendParameterY];

				foreach (var cmotion in bt.children)
				{
					if (srcparams.ContainsKey(cmotion.directBlendParameter))
						queuedparams[cmotion.directBlendParameter] = srcparams[cmotion.directBlendParameter];

					// Go deeper to nested BlendTrees
					var cbt = cmotion.motion as BlendTree;
					if (!(cbt is null))
						GatherBtParams(cbt, ref srcparams, ref queuedparams);
				}
			}
			
			private static void GatherSmParams(AnimatorStateMachine sm, ref Dictionary<string, AnimatorControllerParameter> srcparams, ref Dictionary<string, AnimatorControllerParameter> queuedparams)
			{
				// Go over states to check controlling or BlendTree params
				foreach (var cstate in sm.states)
				{
					var s = cstate.state;
					if (s.mirrorParameterActive && srcparams.ContainsKey(s.mirrorParameter))
						queuedparams[s.mirrorParameter] = srcparams[s.mirrorParameter];
					if (s.speedParameterActive && srcparams.ContainsKey(s.speedParameter))
						queuedparams[s.speedParameter] = srcparams[s.speedParameter];
					if (s.timeParameterActive && srcparams.ContainsKey(s.timeParameter))
						queuedparams[s.timeParameter] = srcparams[s.timeParameter];
					if (s.cycleOffsetParameterActive && srcparams.ContainsKey(s.cycleOffsetParameter))
						queuedparams[s.cycleOffsetParameter] = srcparams[s.cycleOffsetParameter];

					var bt = s.motion as BlendTree;
					if (!(bt is null))
						GatherBtParams(bt, ref srcparams, ref queuedparams);
				}

				// Go over all transitions
				var transitions = new List<AnimatorStateTransition>(sm.anyStateTransitions.Length);
				transitions.AddRange(sm.anyStateTransitions);
				foreach (var cstate in sm.states)
					transitions.AddRange(cstate.state.transitions);
				foreach (var transition in transitions)
				foreach (var cond in transition.conditions)
					if (srcparams.ContainsKey(cond.parameter))
						queuedparams[cond.parameter] = srcparams[cond.parameter];

				// Go deeper to child sate machines
				foreach (var csm in sm.stateMachines)
					GatherSmParams(csm.stateMachine, ref srcparams, ref queuedparams);
			}
			
			// Layer Copy/Paste Functions
			private static void CopyLayer(object layerControllerView)
			{
				var rlist = (ReorderableList)LayerListField.GetValue(layerControllerView);
				var ctrl = Traverse.Create(layerControllerView).Field("m_Host").Property("animatorController").GetValue<AnimatorController>();
				_layerClipboard = rlist.list[rlist.index] as AnimatorControllerLayer;
				_controllerClipboard = ctrl;
				Unsupported.CopyStateMachineDataToPasteboard(_layerClipboard.stateMachine, ctrl, rlist.index);
			}

			public static void PasteLayer(object layerControllerView)
			{
				if (_layerClipboard == null)
					return;
				var rlist = (ReorderableList)LayerListField.GetValue(layerControllerView);
				var ctrl = Traverse.Create(layerControllerView).Field("m_Host").Property("animatorController").GetValue<AnimatorController>();

				// Will paste layer right below selected one
				int targetindex = rlist.index + 1;
				string newname = ctrl.MakeUniqueLayerName(_layerClipboard.name);
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
				PasteLayerProperties(pastedlayer, _layerClipboard);

				// Move up to desired spot
				for (int i = layers.Length-1; i > targetindex; i--)
					layers[i] = layers[i - 1];
				layers[targetindex] = pastedlayer;
				ctrl.layers = layers;

					// Make layer unaffected by undo, forces user to delete manually but prevents dangling sub-assets
				Undo.ClearUndo(ctrl);

				// Pasting to different controller, sync parameters
				if (ctrl != _controllerClipboard)
				{
					Undo.IncrementCurrentGroup();
					int curgroup = Undo.GetCurrentGroup();
					Undo.RecordObject(ctrl, "Sync pasted layer parameters");

					// cache names
					// TODO: do this before pasting to workaround default values not being copied
					var destparams = new Dictionary<string, AnimatorControllerParameter>(ctrl.parameters.Length);
					foreach (var param in ctrl.parameters)
						destparams[param.name] = param;

					var srcparams = new Dictionary<string, AnimatorControllerParameter>(_controllerClipboard.parameters.Length);
					foreach (var param in _controllerClipboard.parameters)
						srcparams[param.name] = param;

					var queuedparams = new Dictionary<string, AnimatorControllerParameter>(_controllerClipboard.parameters.Length);

					// Recursively loop over all nested state machines
					GatherSmParams(pastedsm, ref srcparams, ref queuedparams);

					// Sync up whats missing
					foreach (var param in queuedparams.Values)
					{
						string pname = param.name;
						if (!destparams.ContainsKey(pname))
						{
							Debug.Log("Transferring parameter "+pname); // TODO: count or concatenate names?
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

			public static void PasteLayerSettings(object layerControllerView)
			{
				var rlist = (ReorderableList)LayerListField.GetValue(layerControllerView);
				AnimatorController ctrl = Traverse.Create(layerControllerView).Field("m_Host").Property("animatorController").GetValue<AnimatorController>();

				var layers = ctrl.layers;
				var targetlayer = layers[rlist.index];
				PasteLayerProperties(targetlayer, _layerClipboard);
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

		#endregion

		#region ReflectionCache
			// Animator Window
			private static readonly Type AnimatorWindowType = AccessTools.TypeByName("UnityEditor.Graphs.AnimatorControllerTool");
			private static readonly MethodInfo AnimatorControllerGetter = AccessTools.PropertyGetter(AnimatorWindowType, "animatorController");
			
			private static readonly Type AnimatorControllerType = AccessTools.TypeByName("UnityEditor.Animations.AnimatorController");
			private static readonly Type AnimatorStateMachineType = AccessTools.TypeByName("UnityEditor.Animations.AnimatorStateMachine");
			private static readonly Type AnimatorStateType = AccessTools.TypeByName("UnityEditor.Animations.AnimatorState");
			private static readonly Type ParameterControllerViewType = AccessTools.TypeByName("UnityEditor.Graphs.ParameterControllerView");

			private static readonly Type LayerControllerViewType = AccessTools.TypeByName("UnityEditor.Graphs.LayerControllerView");
			private static readonly FieldInfo LayerScrollField = AccessTools.Field(LayerControllerViewType, "m_LayerScroll");
			private static readonly FieldInfo LayerListField = AccessTools.Field(LayerControllerViewType, "m_LayerList");

			private static readonly Type RenameOverlayType = AccessTools.TypeByName("UnityEditor.RenameOverlay");
			private static readonly MethodInfo BeginRenameMethod = AccessTools.Method(RenameOverlayType, "BeginRename");

			private static readonly Type AnimatorTransitionInspectorBaseType = AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.AnimatorTransitionInspectorBase");
			private static readonly MethodInfo GetElementHeightMethod = AccessTools.Method(typeof(ReorderableList), "GetElementHeight", new Type[]{typeof(int)});
			private static readonly MethodInfo GetElementYOffsetMethod = AccessTools.Method(typeof(ReorderableList), "GetElementYOffset", new Type[]{typeof(int)});
			
			private static GUIStyle StateMotionStyle = null;
			private static GUIStyle StateExtrasStyle = null;
			private static GUIStyle StateExtrasStyleActive = null;
			private static GUIStyle StateExtrasStyleInactive = null;
			private static GUIStyle StateBlendtreeStyle = null;
			private static bool _refocusSelectedLayer = false;

			// Animation Window
			static readonly Assembly EditorAssembly = typeof(Editor).Assembly;
			static readonly Type AnimationWindowHierarchyGUIType = EditorAssembly.GetType("UnityEditorInternal.AnimationWindowHierarchyGUI");
			static readonly Type AnimationWindowHierarchyNodeType = EditorAssembly.GetType("UnityEditorInternal.AnimationWindowHierarchyNode");
			static readonly Type AnimationWindowUtilityType = EditorAssembly.GetType("UnityEditorInternal.AnimationWindowUtility");

			static readonly Type AnimEditorType = AccessTools.TypeByName("UnityEditor.AnimEditor");

			static readonly FieldInfo NodeTypePropertyName = AnimationWindowHierarchyNodeType.GetField("propertyName", BindingFlags.Instance | BindingFlags.Public);
			static readonly FieldInfo NodeTypePath = AnimationWindowHierarchyNodeType.GetField("path", BindingFlags.Instance | BindingFlags.Public);
			static readonly FieldInfo NodeTypeAnimatableObjectType = AnimationWindowHierarchyNodeType.GetField("animatableObjectType", BindingFlags.Instance | BindingFlags.Public);
			static readonly FieldInfo NodeTypeIndent = AnimationWindowHierarchyNodeType.GetField("indent", BindingFlags.Instance | BindingFlags.Public);

			static readonly PropertyInfo NodeDisplayNameProp = AnimationWindowHierarchyNodeType.GetProperty("displayName", BindingFlags.Instance | BindingFlags.Public);

		#endregion

	#endregion

	}
}
#endif
