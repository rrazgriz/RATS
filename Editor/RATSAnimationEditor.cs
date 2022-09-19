#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Razgriz.RATS
{
    public class RATSAnimationEditor : EditorWindow {
        private static int columnWidth = 300;
        
        private Animator animatorObject;
        private List<AnimationClip> animationClips;
        private ArrayList pathsKeys;
        private Hashtable paths;

        Dictionary<string, string> tempPathOverrides;

		private static Texture2D editorWindowIcon;

        private Vector2 scrollPos = Vector2.zero;
        
        [MenuItem("Tools/RATS/Animation Editor")]
        static void ShowWindow() {
			if(editorWindowIcon == null)
			{
				// Decode from base64 encoded 16x16 icon
				editorWindowIcon = Razgriz.RATS.RATSGUI.TextureFromBase64PNG("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");
			}
            RATSAnimationEditor window = EditorWindow.GetWindow<RATSAnimationEditor>();

            window.titleContent = new GUIContent("  RATS Anim Editor", editorWindowIcon);
        }

        public RATSAnimationEditor(){
            animationClips = new List<AnimationClip>();
            tempPathOverrides = new Dictionary<string, string>();
        }
        
        void OnSelectionChange() {
            if (Selection.objects.Length > 1 )
            {
                Debug.Log ("Length? " + Selection.objects.Length);
                animationClips.Clear();
                foreach ( Object o in Selection.objects )
                {
                    if ( o is AnimationClip ) animationClips.Add((AnimationClip)o);
                }
            }
            else if (Selection.activeObject is AnimationClip) {
                animationClips.Clear();
                animationClips.Add((AnimationClip)Selection.activeObject);
                FillModel();
            } else {
                animationClips.Clear();
            }
            
            this.Repaint();
        }

        private string sOriginalRoot = "Root";
        private string sNewRoot = "SomeNewObject/Root";

        void OnGUI() {
            if (Event.current.type == EventType.ValidateCommand) {
                switch (Event.current.commandName) {
                case "UndoRedoPerformed":
                    FillModel();
                    break;
                }
            }
            
            if (animationClips.Count > 0 ) {
                scrollPos = GUILayout.BeginScrollView(scrollPos, GUIStyle.none);
                
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Referenced Animator (Root):", GUILayout.Width(columnWidth));

                animatorObject = ((Animator)EditorGUILayout.ObjectField(
                    animatorObject,
                    typeof(Animator),
                    true,
                    GUILayout.Width(columnWidth))
                                );
                

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Animation Clip:", GUILayout.Width(columnWidth));

                if ( animationClips.Count == 1 )
                {
                    animationClips[0] = ((AnimationClip)EditorGUILayout.ObjectField(
                        animationClips[0],
                        typeof(AnimationClip),
                        true,
                        GUILayout.Width(columnWidth))
                                    );
                }		   
                else
                {
                    GUILayout.Label("Multiple Anim Clips: " + animationClips.Count, GUILayout.Width(columnWidth));
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(20);

                EditorGUILayout.BeginHorizontal();

                sOriginalRoot = EditorGUILayout.TextField(sOriginalRoot, GUILayout.Width(columnWidth));
                sNewRoot = EditorGUILayout.TextField(sNewRoot, GUILayout.Width(columnWidth));
                if (GUILayout.Button("Replace Root")) {
                    Debug.Log("O: "+sOriginalRoot+ " N: "+sNewRoot);
                    ReplaceRoot(sOriginalRoot, sNewRoot);
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Reference path:", GUILayout.Width(columnWidth));
                GUILayout.Label("Animated properties:", GUILayout.Width(columnWidth*0.5f));
                GUILayout.Label("(Count)", GUILayout.Width(60));
                GUILayout.Label("Object:", GUILayout.Width(columnWidth));
                EditorGUILayout.EndHorizontal();
                
                if (paths != null) 
                {
                    foreach (string path in pathsKeys) 
                    {
                        GUICreatePathItem(path);
                    }
                }
                
                GUILayout.Space(40);
                GUILayout.EndScrollView();
            } else {
                GUILayout.Label("Please select an Animation Clip");
            }
        }


        void GUICreatePathItem(string path) {
            string newPath = path;
            GameObject obj = FindObjectInRoot(path);
            GameObject newObj;
            ArrayList properties = (ArrayList)paths[path];

            string pathOverride = path;

            if ( tempPathOverrides.ContainsKey(path) ) pathOverride = tempPathOverrides[path];
            
            EditorGUILayout.BeginHorizontal();
            
            pathOverride = EditorGUILayout.TextField(pathOverride, GUILayout.Width(columnWidth));
            if ( pathOverride != path ) tempPathOverrides[path] = pathOverride;

            if (GUILayout.Button("Change", GUILayout.Width(60))) {
                newPath = pathOverride;
                tempPathOverrides.Remove(path);
            }
            
            EditorGUILayout.LabelField(
                properties != null ? properties.Count.ToString() : "0",
                GUILayout.Width(60)
                );
            
            Color standardColor = GUI.color;
            
            if (obj != null) {
                GUI.color = Color.green;
            } else {
                GUI.color = Color.red;
            }
            
            newObj = (GameObject)EditorGUILayout.ObjectField(
                obj,
                typeof(GameObject),
                true,
                GUILayout.Width(columnWidth)
                );
            
            GUI.color = standardColor;
            
            EditorGUILayout.EndHorizontal();
            
            try {
                if (obj != newObj) {
                    UpdatePath(path, ChildPath(newObj));
                }
                
                if (newPath != path) {
                    UpdatePath(path, newPath);
                }
            } catch (UnityException ex) {
                Debug.LogError(ex.Message);
            }
        }
        
        void OnInspectorUpdate() {
            this.Repaint();
        }
        
        void FillModel() {
            paths = new Hashtable();
            pathsKeys = new ArrayList();

            foreach ( AnimationClip animationClip in animationClips )
            {
                FillModelWithCurves(AnimationUtility.GetCurveBindings(animationClip));
                FillModelWithCurves(AnimationUtility.GetObjectReferenceCurveBindings(animationClip));
            }
        }
        
        private void FillModelWithCurves(EditorCurveBinding[] curves) {
            foreach (EditorCurveBinding curveData in curves) {
                string key = curveData.path;
                
                if (paths.ContainsKey(key)) {
                    ((ArrayList)paths[key]).Add(curveData);
                } else {
                    ArrayList newProperties = new ArrayList();
                    newProperties.Add(curveData);
                    paths.Add(key, newProperties);
                    pathsKeys.Add(key);
                }
            }
        }

        string sReplacementOldRoot;
        string sReplacementNewRoot;

        void ReplaceRoot(string oldRoot, string newRoot)
        {
            float fProgress = 0.0f;
            sReplacementOldRoot = oldRoot;
            sReplacementNewRoot = newRoot;

            AssetDatabase.StartAssetEditing();
            
            for ( int iCurrentClip = 0; iCurrentClip < animationClips.Count; iCurrentClip++ )
            {
                AnimationClip animationClip =  animationClips[iCurrentClip];
                Undo.RecordObject(animationClip, "Animation Hierarchy Root Change");
                
                for ( int iCurrentPath = 0; iCurrentPath < pathsKeys.Count; iCurrentPath ++)
                {
                    string path = pathsKeys[iCurrentPath] as string;
                    ArrayList curves = (ArrayList)paths[path];

                    for (int i = 0; i < curves.Count; i++) 
                    {
                        EditorCurveBinding binding = (EditorCurveBinding)curves[i];

                        if ( path.Contains(sReplacementOldRoot) )
                        {
                            if ( !path.Contains(sReplacementNewRoot) )
                            {
                                string sNewPath = Regex.Replace(path, "^"+sReplacementOldRoot, sReplacementNewRoot );												

                                AnimationCurve curve = AnimationUtility.GetEditorCurve(animationClip, binding);
                                if ( curve != null )
                                {
                                    AnimationUtility.SetEditorCurve(animationClip, binding, null);				
                                    binding.path = sNewPath;
                                    AnimationUtility.SetEditorCurve(animationClip, binding, curve);
                                }
                                else
                                {
                                    ObjectReferenceKeyframe[] objectReferenceCurve = AnimationUtility.GetObjectReferenceCurve(animationClip, binding);
                                    AnimationUtility.SetObjectReferenceCurve(animationClip, binding, null);
                                    binding.path = sNewPath;
                                    AnimationUtility.SetObjectReferenceCurve(animationClip, binding, objectReferenceCurve);
                                }
                            }
                        }
                    }
                    
                    // Update the progress meter
                    float fChunk = 1f / animationClips.Count;
                    fProgress = (iCurrentClip * fChunk) + fChunk * ((float) iCurrentPath / (float) pathsKeys.Count);				
                    
                    EditorUtility.DisplayProgressBar(
                        "Animation Hierarchy Progress", 
                        "How far along the animation editing has progressed.",
                        fProgress);
                }

            }
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
            
            FillModel();
            this.Repaint();
        }
        
        void UpdatePath(string oldPath, string newPath) 
        {
            if (paths[newPath] != null) {
                throw new UnityException("Path " + newPath + " already exists in that animation!");
            }
            AssetDatabase.StartAssetEditing();
            for ( int iCurrentClip = 0; iCurrentClip < animationClips.Count; iCurrentClip++ )
            {
                AnimationClip animationClip =  animationClips[iCurrentClip];
                Undo.RecordObject(animationClip, "Animation Hierarchy Change");
                
                //recreating all curves one by one
                //to maintain proper order in the editor - 
                //slower than just removing old curve
                //and adding a corrected one, but it's more
                //user-friendly
                for ( int iCurrentPath = 0; iCurrentPath < pathsKeys.Count; iCurrentPath ++)
                {
                    string path = pathsKeys[iCurrentPath] as string;
                    ArrayList curves = (ArrayList)paths[path];
                    
                    for (int i = 0; i < curves.Count; i++) {
                        EditorCurveBinding binding = (EditorCurveBinding)curves[i];
                        AnimationCurve curve = AnimationUtility.GetEditorCurve(animationClip, binding);
                        ObjectReferenceKeyframe[] objectReferenceCurve = AnimationUtility.GetObjectReferenceCurve(animationClip, binding);


                            if ( curve != null )
                                AnimationUtility.SetEditorCurve(animationClip, binding, null);
                            else
                                AnimationUtility.SetObjectReferenceCurve(animationClip, binding, null);

                            if (path == oldPath) 
                                binding.path = newPath;

                            if ( curve != null )
                                AnimationUtility.SetEditorCurve(animationClip, binding, curve);
                            else
                                AnimationUtility.SetObjectReferenceCurve(animationClip, binding, objectReferenceCurve);

                        float fChunk = 1f / animationClips.Count;
                        float fProgress = (iCurrentClip * fChunk) + fChunk * ((float) iCurrentPath / (float) pathsKeys.Count);				
                        
                        EditorUtility.DisplayProgressBar(
                            "Animation Hierarchy Progress", 
                            "How far along the animation editing has progressed.",
                            fProgress);
                    }
                }
            }
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
            FillModel();
            this.Repaint();
        }
        
        GameObject FindObjectInRoot(string path) {
            if (animatorObject == null) {
                return null;
            }
            
            Transform child = animatorObject.transform.Find(path);
            
            if (child != null) {
                return child.gameObject;
            } else {
                return null;
            }
        }
        
        string ChildPath(GameObject obj, bool sep = false) {
            if (animatorObject == null) {
                throw new UnityException("Please assign Referenced Animator (Root) first!");
            }
            
            if (obj == animatorObject.gameObject) {
                return "";
            } else {
                if (obj.transform.parent == null) {
                    throw new UnityException("Object must belong to " + animatorObject.ToString() + "!");
                } else {
                    return ChildPath(obj.transform.parent.gameObject, true) + obj.name + (sep ? "/" : "");
                }
            }
        }
    }

    #endif
}