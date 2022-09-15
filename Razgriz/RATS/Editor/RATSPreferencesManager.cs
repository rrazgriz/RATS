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

        static RATSPreferencesManager()
        {
            var Prefs = new EditorPreferenceManager(PreferencePrefix);

            var testFloat1 = new EditorPreference<float>("aTestFloat", 1.0f, PreferencePrefix);
            var testFloat2 = new EditorPreference<float>("aTestFloat2", 2.0f, PreferencePrefix);
            var testInt1 = new EditorPreference<int>("aTestInt", 1, PreferencePrefix);
            var testInt2 = new EditorPreference<int>("aTestInt2", 3, PreferencePrefix);
            var testBool1 = new EditorPreference<bool>("aTestBool2", true, PreferencePrefix);

            Prefs.Add(testFloat1);
            Prefs.Add(testInt1);
            Prefs.Add(testFloat2);
            Prefs.Add(testInt2);

            Prefs.Create("aTestBool", true);
            Prefs.Create("aTestColor", Color.green);

            Prefs.InitializeAll();

            foreach (IEditorPreference pref in Prefs.GetPrefs())
            {
                Debug.Log($"{pref.GetName()} : {pref.GetPrefType().ToString()}");
            }
        }
    }

    public class EditorPreferenceManager
    {
        private string _prefix = MethodBase.GetCurrentMethod().DeclaringType.Namespace;
        private string _id = "";
        private Dictionary<string, IEditorPreference> _preferences = new Dictionary<string, IEditorPreference>();

        public EditorPreferenceManager(string id = "") => _id = id;
        public EditorPreferenceManager(string prefix, string id = "")
        {
            _id = id;
            _prefix = prefix;
        }

        public T GetValue<T>(string name) => GetPreferenceValidated<T>(name).Value;
        public T GetDefaultValue<T>(string name) => GetPreferenceValidated<T>(name).DefaultValue;
        public EditorPreference<T> GetPref<T>(string name) => GetPreferenceValidated<T>(name);
        public List<IEditorPreference> GetPrefs() => _preferences.Values.ToList();

        public bool Contains(string name) => _preferences.ContainsKey(name);
        public List<string> PrefsList() => _preferences.Keys.ToList<string>();
        public List<string> PrefsList(Type type) => _preferences.Where( pref => pref.Value.GetPrefType() == type ).Select( pref => pref.Key ).ToList();
        
        public void InitializeAll() => _preferences.Values.ToList().ForEach( pref => pref.Initialize() );
        public void SaveAll() => _preferences.Values.ToList().ForEach( pref => pref.Save() );

        public void ResetAll() => _preferences.Values.ToList().ForEach( pref => pref.ResetToDefault() );
        public void ResetAll(string tag) => ResetAll(new List<string>{tag});
        public void ResetAll(IEnumerable<string> tags)
        {
            foreach (IEditorPreference pref in _preferences.Values)
            {
                List<string> prefTags = pref.GetTags();
                if(prefTags.Intersect(tags).Any())
                    pref.ResetToDefault();
            }
        }

        public void PreferenceAddTag(string name, string tag) => _preferences[name].AddTag(tag);
        public void PreferenceAddTags(string name, IEnumerable<string> tags) => _preferences[name].AddTags(tags);

        public void Create(string name, bool   defaultValue, IEnumerable<string> tags = null) => this.Add(new EditorPreference<bool>   (name, defaultValue, _prefix, tags));
        public void Create(string name, float  defaultValue, IEnumerable<string> tags = null) => this.Add(new EditorPreference<float>  (name, defaultValue, _prefix, tags));
        public void Create(string name, int    defaultValue, IEnumerable<string> tags = null) => this.Add(new EditorPreference<int>    (name, defaultValue, _prefix, tags));
        public void Create(string name, string defaultValue, IEnumerable<string> tags = null) => this.Add(new EditorPreference<string> (name, defaultValue, _prefix, tags));
        public void Create(string name, Color  defaultValue, IEnumerable<string> tags = null) => this.Add(new EditorPreference<Color>  (name, defaultValue, _prefix, tags));

        public void Add(EditorPreference<bool>   pref) => _preferences.Add(pref.Name, pref);
        public void Add(EditorPreference<int>    pref) => _preferences.Add(pref.Name, pref);
        public void Add(EditorPreference<float>  pref) => _preferences.Add(pref.Name, pref);
        public void Add(EditorPreference<string> pref) => _preferences.Add(pref.Name, pref);
        public void Add(EditorPreference<Color>  pref) => _preferences.Add(pref.Name, pref);

        private EditorPreference<T> GetPreferenceValidated<T>(string name)
        {
            if (!_preferences.Keys.Contains(name))
            {
                Debug.LogError($"[RATS] Preferences Manager: Preference {name} doesn't exist in preference group {_id}");
                return default(EditorPreference<T>);
            }
            else if (_preferences[name].GetPrefType() != typeof(T))
            {
                Debug.LogError($"[RATS] Preferences Manager: Tried to access preference {name} of type {_preferences[name].GetPrefType().ToString()} with {typeof(T).ToString()}");
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
        private string _name = "";
        private List<string> _tags = new List<string>();

        public T Value { get; set; }
        public T DefaultValue { get; set; }
        public string Name { get; set; }

        public EditorPreference(string name, T defaultValue, string prefix, IEnumerable<string> tags = null)
        {
            if (!s_validTypes.Contains(typeof(T)))
                Debug.LogError($"[RATS] Preference Manager: Tried to create EditorPreference of type {typeof(T).ToString()} - EditorPreference can only be of types: {string.Join(",", s_validTypes)}");

            Name = name;
            DefaultValue = defaultValue;
            Value = defaultValue;
            _prefix = prefix;
            _type = typeof(T);
            if (tags != null)
                _tags.AddRange(tags);
        }

        public string GetName() => Name;
        public string GetEditorPrefsID() => $"{_prefix}.{_name}";

        public Type GetPrefType() => _type;
        public List<Type> GetValidTypes() => s_validTypes;

        public T GetValue() => Value;
        public T GetDefaultValue() => DefaultValue;

        public void AddTag(string tag) => _tags.Add(tag);
        public void AddTags(IEnumerable<string> tags) => _tags.AddRange(tags);
        public List<string> GetTags() => _tags;

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