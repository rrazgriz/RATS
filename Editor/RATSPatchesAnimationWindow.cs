// RATS - Raz's Animator Tweaks'n Stuff
// Original AnimatorExtensions by Dj Lukis.LT, under MIT License

// Copyright (c) 2023 Razgriz
// SPDX-License-Identifier: MIT

#if UNITY_EDITOR && RATS_HARMONY
using System;
using System.Reflection;
using System.Linq;
using UnityEngine;
using HarmonyLib;

namespace Razgriz.RATS
{
    public partial class RATS
    {
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

                if(!string.IsNullOrEmpty(propertyName) && RATS.Prefs.AnimationWindowTrimActualNames) propertyName = propertyName.Replace("m_", "");

                if(animatableObjectType != null)
                {
                    componentPrefix = (animatableObjectType).ToString().Split('.').Last() + ".";
                }

                string displayNameString = RATS.Prefs.AnimationWindowShowActualPropertyNames ? componentPrefix + propertyName : displayName;
                NodeTypeIndent.SetValue(node, (int)(RATS.Prefs.AnimationWindowIndentScale * ((float)propertyPath.Split('/').Length)) );						
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
                if(RATS.Prefs.AnimationWindowShowFullPath)
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
    }
}
#endif