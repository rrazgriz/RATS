// Some Harmony based Unity animator window patches to help workflow
// Original by Dj Lukis.LT, under MIT License

// Copyright (c) 2022 Razgriz
// SPDX-License-Identifier: MIT

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using ReorderableList = UnityEditorInternal.ReorderableList;
using HarmonyLib;

namespace Razgriz.AnimatorExtensions
{
	[InitializeOnLoad]
	public class AnimatorExtensions
	{
		public static Harmony harmonyInstance = new Harmony("Razgriz.AnimatorExtensions");
		private static int wait = 0;

		static AnimatorExtensions()
		{
			AnimatorExtensionsGUI.HandlePreferences();
			// Register our patch delegate
			EditorApplication.update += DoPatches;
			InitTextures();
		}

		static void DoPatches()
		{
			// Wait a couple cycles to patch to let static initializers run
			wait++;
			if(wait > 2)
			{
				harmonyInstance.PatchAll();
				// Unregister our delegate so it doesn't run again
				EditorApplication.update -= DoPatches;
				Debug.Log("AnimatorExtensions: Patching");
			}
		}

		#region AnimatorWindowPatches

		// CEditor Compatibility
		#if !RAZGRIZ_AEXTENSIONS_NOANIMATOR
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
												Debug.Log("AnimatorExtensions: Restored transition condition mode");
											}
											// int->float has restrictions
											else if ((ConditionParamType_pre == AnimatorControllerParameterType.Int) && (param.type == AnimatorControllerParameterType.Float))
											{
												AnimatorConditionMode premode = (AnimatorConditionMode)ConditionMode_pre;
												if ((premode != AnimatorConditionMode.Equals) && (premode != AnimatorConditionMode.NotEqual))
												{
													m_ConditionMode.intValue = ConditionMode_pre;
													Debug.Log("AnimatorExtensions: Restored transition condition mode 2");
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
						layer.defaultWeight = AnimatorExtensionsGUI.prefs_NewLayersWeight1 ? 1.0f : 0.0f;
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

			#endregion GraphFeatures

		#endif //RAZGRIZ_AEXTENSIONS_NOANIMATOR

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
						if(AnimatorExtensionsGUI.prefs_GraphGridOverride)
						{
							GL.PushMatrix();

							// Draw Background
							GL.Begin(GL.QUADS);
							Color backgroundColor = AnimatorExtensionsGUI.prefs_GraphGridBackgroundColor;
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
							GL.Color(Color.Lerp(Color.clear, AnimatorExtensionsGUI.prefs_GraphGridColorMajor, tMajor));
							gridSize = AnimatorExtensionsGUI.prefs_GraphGridScalingMajor * 100f;
							for (float x = gridRect.xMin - gridRect.xMin % gridSize; x < gridRect.xMax; x += gridSize)
							{
								GL.Vertex(new Vector3(x, gridRect.yMin)); GL.Vertex(new Vector3(x, gridRect.yMax));
							}
							for (float y = gridRect.yMin - gridRect.yMin % gridSize; y < gridRect.yMax; y += gridSize)
							{
								GL.Vertex(new Vector3(gridRect.xMin, y)); GL.Vertex(new Vector3(gridRect.xMax, y));
							}

							// Minor
							GL.Color(Color.Lerp(Color.clear, AnimatorExtensionsGUI.prefs_GraphGridColorMinor, tMinor));
							gridSize = AnimatorExtensionsGUI.prefs_GraphGridScalingMajor * (100f / AnimatorExtensionsGUI.prefs_GraphGridDivisorMinor);
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
						bool doDesnap = Event.current.control ^ AnimatorExtensionsGUI.prefs_GraphDragNoSnap;
						if(doDesnap)
						{
							__result = position;
							return false;
						}
						else if(AnimatorExtensionsGUI.prefs_GraphDragSnapToModifiedGrid && AnimatorExtensionsGUI.prefs_GraphGridOverride) // Enforce Minor Grid Spacing Snapping
						{
							float minorGridSpacing = AnimatorExtensionsGUI.prefs_GraphGridScalingMajor * (100f / AnimatorExtensionsGUI.prefs_GraphGridDivisorMinor);
							__result = new Rect(Mathf.Round(position.x / minorGridSpacing) * minorGridSpacing, Mathf.Round(position.y / minorGridSpacing) * minorGridSpacing, position.width, position.height);
							return false;
						}

						return !doDesnap;
					}
				}
				
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
						if(styleName == "node") // Regular state node
						{
							__result.normal.textColor = AnimatorExtensionsGUI.prefs_StateTextColor;
							__result.fontSize = AnimatorExtensionsGUI.prefs_StateLabelFontSize;

							switch(color)
							{
								case 6: // Red
									__result.normal.background = on ? nodeBackgroundImageRedActive : nodeBackgroundImageRed;
									break;
								case 5: // Orange
									__result.normal.background = on ? nodeBackgroundImageOrangeActive : nodeBackgroundImageOrange;
									break;
								case 4: // Yellow
									__result.normal.background = on ? nodeBackgroundImageYellowActive : nodeBackgroundImageYellow;
									break;
								case 3: // Green
									__result.normal.background = on ? nodeBackgroundImageGreenActive : nodeBackgroundImageGreen;
									break;
								case 2: // Aqua
									__result.normal.background = on ? nodeBackgroundImageAquaActive : nodeBackgroundImageAqua;
									break;
								case 1: // Blue
									__result.normal.background = on ? nodeBackgroundImageBlueActive : nodeBackgroundImageBlue;
									break;
								default: // Anything Else
									__result.normal.background = on ? nodeBackgroundImageActive : nodeBackgroundImage;
									break;
							}
						}
						else if(styleName == "node hex") // SubStateMachine node
						{
							__result.fontSize = AnimatorExtensionsGUI.prefs_StateLabelFontSize;
							__result.normal.textColor = AnimatorExtensionsGUI.prefs_StateTextColor;
							__result.normal.background = on ? stateMachineBackgroundImageActive : stateMachineBackgroundImage;
						}
					}
				}

				// Show motion name and extra details on state graph nodes
				static bool prefs_DimOffLabels_last = false;
				static Color lastTextColor = AnimatorExtensionsGUI.prefs_StateTextColor;
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
						if (StateMotionStyle == null || (prefs_DimOffLabels_last != AnimatorExtensionsGUI.prefs_HideOffLabels) || lastTextColor != AnimatorExtensionsGUI.prefs_StateTextColor)
						{
							StateExtrasStyle = new GUIStyle(EditorStyles.label);
							StateExtrasStyle.alignment = TextAnchor.UpperRight;
							StateExtrasStyle.fontStyle = FontStyle.Bold;
							StateExtrasStyle.normal.textColor = AnimatorExtensionsGUI.prefs_StateTextColor;

							StateExtrasStyleActive = new GUIStyle(EditorStyles.label);
							StateExtrasStyleActive.alignment = TextAnchor.UpperRight;
							StateExtrasStyleActive.fontStyle = FontStyle.Bold;
							StateExtrasStyleActive.normal.textColor = AnimatorExtensionsGUI.prefs_StateTextColor;

							StateExtrasStyleInactive = new GUIStyle(EditorStyles.label);
							StateExtrasStyleInactive.alignment = TextAnchor.UpperRight;
							StateExtrasStyleInactive.fontStyle = FontStyle.Bold;
							float inactiveExtrasTextAlpha = AnimatorExtensionsGUI.prefs_HideOffLabels ? 0.0f : 0.5f;
							StateExtrasStyleInactive.normal.textColor = new Color(AnimatorExtensionsGUI.prefs_StateTextColor.r, AnimatorExtensionsGUI.prefs_StateTextColor.g, AnimatorExtensionsGUI.prefs_StateTextColor.b, inactiveExtrasTextAlpha);

							StateMotionStyle = new GUIStyle(EditorStyles.miniBoldLabel);
							StateMotionStyle.fontSize = 9;
							StateMotionStyle.alignment = TextAnchor.LowerCenter;
							StateMotionStyle.normal.textColor = AnimatorExtensionsGUI.prefs_StateTextColor;

							StateBlendtreeStyle = new GUIStyle(EditorStyles.label);
							StateBlendtreeStyle.alignment = TextAnchor.UpperLeft;
							StateBlendtreeStyle.fontStyle = FontStyle.Bold;
						}

						prefs_DimOffLabels_last = AnimatorExtensionsGUI.prefs_HideOffLabels;
						Rect stateRect = GUILayoutUtility.GetLastRect();

						

						bool debugShowLabels = Event.current.alt;

						var renameOverlay = Traverse.Create(RenameOverlayType);
						renameOverlay.Field("editFieldRect").SetValue(stateRect);

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
							
							// Empty Animation/State Warning, top left (option)
							if(AnimatorExtensionsGUI.prefs_ShowWarningsTopLeft)
							{
								
								Rect emptyWarningRect = new Rect(stateRect.x + iconOffset + 1, stateRect.y - 28, 14, 14);
								if((debugShowLabels || AnimatorExtensionsGUI.prefs_StateAnimIsEmptyLabel))
								{
									if(isEmptyAnim) EditorGUI.LabelField(emptyWarningRect, new GUIContent(EditorGUIUtility.IconContent("Warning@2x").image, "Animation Clip has no Keyframes"));
									else if(isEmptyState) EditorGUI.LabelField(emptyWarningRect, new GUIContent(EditorGUIUtility.IconContent("Error@2x").image, "State has no Motion assigned"));
								}
							}

							#if !RAZGRIZ_AEXTENSIONS_CECOMPAT
								if(hasMotion && (debugShowLabels || AnimatorExtensionsGUI.prefs_StateExtraLabelsWD)) EditorGUI.LabelField(wdLabelRect, "WD", (isWD ? StateExtrasStyleActive : StateExtrasStyleInactive));
								if(				(debugShowLabels || AnimatorExtensionsGUI.prefs_StateExtraLabelsBehavior)) EditorGUI.LabelField(behaviorLabelRect, "B", (hasBehavior ? StateExtrasStyleActive : StateExtrasStyleInactive));
								if(hasMotion && (debugShowLabels || AnimatorExtensionsGUI.prefs_StateExtraLabelsMotionTime)) EditorGUI.LabelField(motionTimeLabelRect, "M", (hasMotionTime ? StateExtrasStyleActive : StateExtrasStyleInactive));
								if(hasMotion && (debugShowLabels || AnimatorExtensionsGUI.prefs_StateExtraLabelsSpeed)) EditorGUI.LabelField(speedLabelRect, "S", (hasSpeedParam ? StateExtrasStyleActive : StateExtrasStyleInactive));

								if (hasMotion && (debugShowLabels || AnimatorExtensionsGUI.prefs_StateMotionLabels))
								{
									string motionName = "  [none]";
									if (aState.motion) motionName = "  " + aState.motion.name;

									float iconSize = 13f;

									Texture labelIcon = (Texture)(new Texture2D(1,1));
									string labelTooltip = "";
									if(hasblendtree)
									{
										labelIcon = EditorGUIUtility.IconContent("d_BlendTree Icon").image;
										labelTooltip = "State contains a Blendtree";
										iconSize = 14;
									}
									else if(isEmptyAnim && !AnimatorExtensionsGUI.prefs_ShowWarningsTopLeft)
									{
										labelIcon = EditorGUIUtility.IconContent("Warning@2x").image;
										labelTooltip = "Animation Clip has no Keyframes";
									}
									else if(isEmptyState && !AnimatorExtensionsGUI.prefs_ShowWarningsTopLeft)
									{
										labelIcon = EditorGUIUtility.IconContent("Error@2x").image;
										labelTooltip = "State has no Motion assigned";
									}
									else 
									{
										labelIcon = EditorGUIUtility.IconContent("AnimationClip On Icon").image;
										labelTooltip = "State contains an Animation Clip";
										iconSize = 16;
									}

									GUIContent motionLabel = new GUIContent(motionName);
									float width = EditorStyles.label.CalcSize(motionLabel).x;
									float height = EditorStyles.label.CalcSize(motionLabel).y;

									Rect motionLabelRect = new Rect(stateRect.x + stateRect.width/2 - width/2, stateRect.y - height/2, width, height);
									Rect motionIconRect = new Rect(motionLabelRect.x - iconSize/2 - 0.5f, motionLabelRect.y + height/2 - iconSize/2, iconSize, iconSize);

									EditorGUI.LabelField(motionLabelRect, motionLabel, StateMotionStyle);
									EditorGUI.LabelField(motionIconRect, new GUIContent(labelIcon, labelTooltip));
								}
							#endif
						}
					}
				}

			#endregion GraphVisuals

		#endregion

		#region AnimationWindowPatches

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

						string displayNameString = AnimatorExtensionsGUI.prefs_AnimationWindowShowActualPropertyNames ? componentPrefix + propertyName : displayName;

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

			#endregion AnimationWindowFeatures

		#endregion AnimationWindowPatches

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
				private static AnimatorControllerLayer _layerClipboard = null;
				private static AnimatorController _controllerClipboard = null;

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

			#endregion HelperFunctions

			#region ReflectionCache
				// Animator Window
				private static readonly Type AnimatorWindowType = AccessTools.TypeByName("UnityEditor.Graphs.AnimatorControllerTool");
				private static readonly MethodInfo AnimatorControllerGetter = AccessTools.PropertyGetter(AnimatorWindowType, "animatorController");

				private static readonly Type AnimatorWindowGraphGUIType = AccessTools.TypeByName("UnityEditor.Graphs.GraphGUI");
				private static readonly FieldInfo AnimatorWindowGraphGridColorMajor = AccessTools.Field(AnimatorWindowGraphGUIType, "gridMajorColor");
				private static readonly FieldInfo AnimatorWindowGraphGridColorMinor = AccessTools.Field(AnimatorWindowGraphGUIType, "gridMinorColor");
				private static readonly Type AnimatorWindowStylesType = AccessTools.TypeByName("UnityEditor.Graphs.Styles");
				private static readonly FieldInfo AnimatorWindowGraphStyleBackground = AccessTools.Field(AnimatorWindowStylesType, "graphBackground");

				private static readonly FieldInfo AnimatorWindowGraphGraph = AccessTools.Field(AnimatorWindowGraphGUIType, "m_Graph");

				private static readonly Type GraphStylesType = AccessTools.TypeByName("UnityEditor.Graphs.Styles");

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

			#endregion ReflectionCache

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
				static void InitTextures()
				{
					byte[] nodeBackgroundBytes = System.IO.File.ReadAllBytes(Path.Combine(Directory.GetCurrentDirectory(), AssetDatabase.GUIDToAssetPath("780a9e3efb8a1ca42b44c98c5e972f2d")).Replace("/", "\\"));
					byte[] nodeBackgroundActiveBytes = System.IO.File.ReadAllBytes(Path.Combine(Directory.GetCurrentDirectory(), AssetDatabase.GUIDToAssetPath("4fb6ef4881973e24cbcf73cff14ae0c8")).Replace("/", "\\"));
					nodeBackgroundImageMask = LoadPNG(Path.Combine(Directory.GetCurrentDirectory(), AssetDatabase.GUIDToAssetPath("81dcb3a363364ea4f9a475b4cebb0eaf")).Replace("/", "\\"));
					
					stateMachineBackgroundImage = LoadPNG(Path.Combine(Directory.GetCurrentDirectory(), AssetDatabase.GUIDToAssetPath("160541e301c89e644a9c10fb82f74f88")).Replace("/", "\\"));
					stateMachineBackgroundPixels = stateMachineBackgroundImage.GetPixels();
					stateMachineBackgroundImageActive = LoadPNG(Path.Combine(Directory.GetCurrentDirectory(), AssetDatabase.GUIDToAssetPath("c430ad55db449494aa1caefe9dccdc2d")).Replace("/", "\\"));
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

					UpdateGraphTextures();
				}

				public static void UpdateGraphTextures()
				{
					Color glowTint = new Color(44/255f, 119/255f, 212/255f, 1f);
					Texture2D glowState = new Texture2D(nodeBackgroundImageActive.width, nodeBackgroundImageActive.height);
					Texture2D glowStateMachine = new Texture2D(stateMachineBackgroundImageActive.width, stateMachineBackgroundImageActive.height);
					glowState.SetPixels(nodeBackgroundActivePixels);
					glowStateMachine.SetPixels(stateMachineBackgroundPixelsActive);
					TintTexture2D(ref glowState, glowTint);
					TintTexture2D(ref glowStateMachine, glowTint);
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
					TintTexture2D(ref stateMachineBackgroundImage, AnimatorExtensionsGUI.prefs_StateColorGray);
					TintTexture2D(ref nodeBackgroundImage, AnimatorExtensionsGUI.prefs_StateColorGray);
					TintTexture2D(ref nodeBackgroundImageAqua, AnimatorExtensionsGUI.prefs_StateColorAqua);
					TintTexture2D(ref nodeBackgroundImageGreen, AnimatorExtensionsGUI.prefs_StateColorGreen);
					TintTexture2D(ref nodeBackgroundImageOrange, AnimatorExtensionsGUI.prefs_StateColorOrange);
					TintTexture2D(ref nodeBackgroundImageRed, AnimatorExtensionsGUI.prefs_StateColorRed); 

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

				private static byte[] GetFileBytes(string filePath)
				{
					byte[] fileData = System.IO.File.ReadAllBytes(filePath);
					return fileData;
				}

				private static Texture2D LoadPNG(string filePath)
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

				private static Texture2D LoadPNG(byte[] fileData)
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
					Parallel.For(0, pixels.Length, (j, state) => { pixels[j] += pixelsToAdd[j];});

					texture.SetPixels(pixels); 
					texture.Apply(recalculateMips, makeNoLongerReadable);
				}

			#endregion TextureHandling

		#endregion Utility

	}
}
#endif
