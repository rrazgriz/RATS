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
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Razgriz.RATS
{
    // For testing
    [InitializeOnLoad]
    public static class RATSPreferencesManager
    {
        public static string PreferencePrefix = MethodBase.GetCurrentMethod().DeclaringType.Namespace;

        static void PrintPrefs(EditorPreferenceManager prefs)
        {
            foreach (IEditorPreference pref in prefs.GetList())
            {
                Debug.Log($"Pref: {pref.GetEditorPrefsID()}  Type: {pref.GetPrefType().ToString()}  Tags: {string.Join(",", pref.GetTags())}");
            }
        }

        static void PrintFloats(EditorPreferenceManager prefs)
        {
            foreach (EditorPreference<float> pref in prefs.GetList<float>())
            {
                Debug.Log($"Pref: {pref.GetEditorPrefsID()}  Type: {pref.GetPrefType().ToString()}  Value: {pref.Value.ToString()}  Stored Value: {EditorPrefs.GetFloat(pref.GetEditorPrefsID(), -111)})");
            }
        }

        static RATSPreferencesManager()
        {
            var RATSPrefs = RATSPreferences.GetInstance();

            RATSPrefs.Create("aTestFloat", 1.0f, "testTag");
            RATSPrefs.Create("aTestFloat2", 7.0f);
            RATSPrefs.Create("aTestInt", 1);
            RATSPrefs.Create("aTestInt2", -20);

            RATSPrefs.Create("aTestBool", true);
            RATSPrefs.Create("aTestString", "aString");
            RATSPrefs.Create("aTestColor", Color.green);

            RATSPrefs.InitializeAll();

            PrintFloats(RATSPrefs);
            RATSPrefs.Float("aTestFloat").Value = 2.0f;
            RATSPrefs.Float("aTestFloat2").Value = -5.2132348957f;
            PrintFloats(RATSPrefs);
            RATSPrefs.SaveAll();
            PrintFloats(RATSPrefs);
            RATSPrefs.ResetAll("testTag");
            PrintFloats(RATSPrefs);
            RATSPrefs.ResetAll();
            PrintFloats(RATSPrefs);
            RATSPrefs.SaveAll();
            PrintFloats(RATSPrefs);

        }
    }

    // Pseudo-singleton (global instance)
    // Access as such:
    //  var RATSPrefs = RATSPreferences.GetInstance();
    public sealed class RATSPreferences : EditorPreferenceManager
    {
        private static volatile EditorPreferenceManager _instance;

        private RATSPreferences() {}

        // Lazy init
        public static EditorPreferenceManager GetInstance()
        {
            if(_instance == null)
                _instance  = new EditorPreferenceManager();
            return _instance;
        }
    }

    public class EditorPreferenceManager
    {
        private string _prefix = MethodBase.GetCurrentMethod().DeclaringType.Namespace;
        private Dictionary<string, IEditorPreference> _preferences = new Dictionary<string, IEditorPreference>();

        // Constructor
        public EditorPreferenceManager() {}
        public EditorPreferenceManager(string prefix) => _prefix = prefix;

        // Member Handling
        public void Create(string name, bool   defaultValue, params string[] tags) => this.Add(new EditorPreference<bool>   (name, defaultValue, _prefix, tags));
        public void Create(string name, float  defaultValue, params string[] tags) => this.Add(new EditorPreference<float>  (name, defaultValue, _prefix, tags));
        public void Create(string name, int    defaultValue, params string[] tags) => this.Add(new EditorPreference<int>    (name, defaultValue, _prefix, tags));
        public void Create(string name, string defaultValue, params string[] tags) => this.Add(new EditorPreference<string> (name, defaultValue, _prefix, tags));
        public void Create(string name, Color  defaultValue, params string[] tags) => this.Add(new EditorPreference<Color>  (name, defaultValue, _prefix, tags));

        public void Add(EditorPreference<bool>   pref) => _preferences.Add(pref.Name, pref);
        public void Add(EditorPreference<int>    pref) => _preferences.Add(pref.Name, pref);
        public void Add(EditorPreference<float>  pref) => _preferences.Add(pref.Name, pref);
        public void Add(EditorPreference<string> pref) => _preferences.Add(pref.Name, pref);
        public void Add(EditorPreference<Color>  pref) => _preferences.Add(pref.Name, pref);

        // Access
        public EditorPreference<bool>   Bool(string name)   => GetPreferenceValidated<bool>(name);
        public EditorPreference<float>  Float(string name)  => GetPreferenceValidated<float>(name);
        public EditorPreference<int>    Int(string name)    => GetPreferenceValidated<int>(name);
        public EditorPreference<string> String(string name) => GetPreferenceValidated<string>(name);
        public EditorPreference<Color>  Color(string name)  => GetPreferenceValidated<Color>(name);
        public EditorPreference<T>      Get<T>(string name) => GetPreferenceValidated<T>(name);

        public List<IEditorPreference> GetList() => _preferences.Values.ToList();
        public List<EditorPreference<T>> GetList<T>() => _preferences.Where( pref => pref.Value.GetPrefType() == typeof(T) ).Select( pref => pref.Value as EditorPreference<T> ).ToList();

        public List<string> PrefNames() => _preferences.Keys.ToList<string>();
        public List<string> PrefNames<T>() => _preferences.Where( pref => pref.Value.GetPrefType() == typeof(T) ).Select( pref => pref.Key ).ToList();
        
        // Registry Key management
        public void InitializeAll() => _preferences.Values.ToList().ForEach( pref => pref.Initialize() );
        public void SaveAll() => _preferences.Values.ToList().ForEach( pref => pref.Save() );

        public void ResetAll() => _preferences.Values.ToList().ForEach( pref => pref.ResetToDefault() );
        public void ResetAll(string tag) => ResetAll(new List<string>{tag});
        public void ResetAll(IEnumerable<string> tags)
        {
            foreach (IEditorPreference pref in _preferences.Values)
            {
                if(pref.GetTags().Intersect(tags).Any())
                    pref.ResetToDefault();
            }
        }

        private EditorPreference<T> GetPreferenceValidated<T>(string name)
        {
            if (!_preferences.Keys.Contains(name))
            {
                Debug.LogError($"[{_prefix}] Preferences Manager: Preference {name} doesn't exist in preferences");
                return default(EditorPreference<T>);
            }
            else if (_preferences[name].GetPrefType() != typeof(T))
            {
                Debug.LogError($"[{_prefix}] Preferences Manager: Tried to access preference {name} of type {_preferences[name].GetPrefType().ToString()} with {typeof(T).ToString()}");
                return default(EditorPreference<T>);
            }
            else
                return _preferences[name] as EditorPreference<T>;
        }
    }

    public interface IEditorPreference
    {
        string GetName();
        void ResetToDefault();
        void Initialize();
        void Save();
        void AddTag(string tag);
        void AddTags(IEnumerable<string> tags);
        List<string> GetTags();
        Type GetPrefType();
        string GetEditorPrefsID();
        List<Type> GetValidTypes();
    }

    public interface IEditorPreference<T> : IEditorPreference
    {
        T GetValue();
        T GetDefaultValue();
    }

    public class EditorPreference<T> : IEditorPreference<T>
    {
        private static readonly List<Type> s_validTypes = new List<Type> { typeof(bool), typeof(float), typeof(int), typeof(string), typeof(Color) };

        private readonly string _prefix = "";
        private readonly Type _type;

        public T Value { get; set; }
        public T DefaultValue { get; set; }
        public string Name { get; set; }
        public List<string> Tags = new List<string>();

        public EditorPreference(string name, T defaultValue, string prefix, params string[] tags)
        {
            if (!s_validTypes.Contains(typeof(T)))
                Debug.LogError($"[{_prefix}] Preferences Manager: Tried to create EditorPreference of type {typeof(T).ToString()} - EditorPreference can only be of types: {string.Join(",", s_validTypes)}");

            Name = name;
            DefaultValue = defaultValue;
            Value = defaultValue;
            _prefix = prefix;
            _type = typeof(T);
            if (tags.Length > 0)
                Tags.AddRange(tags);
        }

        public string GetName() => Name;
        public string GetEditorPrefsID() => $"{_prefix}.{Name}";

        public Type GetPrefType() => _type;
        public List<Type> GetValidTypes() => s_validTypes;

        public T GetValue() => Value;
        public T GetDefaultValue() => DefaultValue;

        public void AddTag(string tag) => Tags.Add(tag);
        public void AddTags(IEnumerable<string> tags) => Tags.AddRange(tags);
        public List<string> GetTags() => Tags;

        public void ResetToDefault() => Value = DefaultValue;

        public void Initialize()
        {
            string editorPrefsID = GetEditorPrefsID();

            if (!EditorPrefs.HasKey(editorPrefsID)) this.Save();
            else if (_type == typeof(bool))   Value = (T)(object) EditorPrefs.GetBool(editorPrefsID, (bool)(object) DefaultValue);
            else if (_type == typeof(float))  Value = (T)(object) EditorPrefs.GetFloat(editorPrefsID, (float)(object) DefaultValue);
            else if (_type == typeof(int))    Value = (T)(object) EditorPrefs.GetInt(editorPrefsID, (int)(object) DefaultValue);
            else if (_type == typeof(string)) Value = (T)(object) EditorPrefs.GetString(editorPrefsID, (string)(object) DefaultValue);
            else if (_type == typeof(Color))  Value = (T)(object) HexToColor(EditorPrefs.GetString(editorPrefsID, ColorToHex((Color)(object)DefaultValue)));
        }

        public void Save()
        {
            string editorPrefsID = GetEditorPrefsID();
            if      (_type == typeof(bool)) EditorPrefs.SetBool(editorPrefsID, (bool)(object)Value);
            else if (_type == typeof(float)) EditorPrefs.SetFloat(editorPrefsID, (float)(object)Value);
            else if (_type == typeof(int)) EditorPrefs.SetInt(editorPrefsID, (int)(object)Value);
            else if (_type == typeof(string)) EditorPrefs.SetString(editorPrefsID, (string)(object)Value);
            else if (_type == typeof(Color)) EditorPrefs.SetString(editorPrefsID, ColorToHex((Color)(object)Value));
        }

        private string ColorToHex(Color color, bool numberSign = false)
        {
            return (numberSign ? "#" : "") + ColorUtility.ToHtmlStringRGBA(color);
        }

        private Color HexToColor(string hexColor)
        {
            ColorUtility.TryParseHtmlString(hexColor, out Color color); return color;
        }
    }
}
#endif