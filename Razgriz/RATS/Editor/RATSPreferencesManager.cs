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
	[InitializeOnLoad]
	public static class RATSPreferencesManager
	{
		public static string PreferencePrefix = MethodBase.GetCurrentMethod().DeclaringType.Namespace;

		static RATSPreferencesManager()
		{
            var Prefs = new EditorPreferenceGroup(PreferencePrefix);
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

			foreach(var name in Prefs.PrefsList(typeof(float)))
			{
				Debug.Log(name);
				Debug.Log(Prefs.GetPref<float>(name).EditorPrefsID);
			}
		}
	}

	public class EditorPreferenceGroup
	{
		private string _prefix = MethodBase.GetCurrentMethod().DeclaringType.Namespace;
		private string _id = "";
		readonly Dictionary<string, IEditorPreference> _preferences = new Dictionary<string, IEditorPreference>();

		public EditorPreferenceGroup(string id = "") 
		{
			_id = id;
		}

		public EditorPreferenceGroup(string prefix, string id = "") 
		{
			_id = id;
			_prefix = prefix;
		}

		public T GetValue<T>(string name) 
		{
			return GetPref<T>(name).Value;
		}

		public T GetDefaultValue<T>(string name) 
		{ 
			return GetPref<T>(name).DefaultValue;
		}

		bool ValidatePreferenceAccess<T>(string name)
		{
			if (!_preferences.Keys.Contains(name))
			{
				Debug.LogError($"[RATS] Preferences Manager: Preference {name} doesn't exist in preference group {_id}");
				return false;
			}
			else if (_preferences[name].Type() != typeof(T))
			{
				Debug.LogError($"[RATS] Preferences Manager: Tried to access preference {name} of type {_preferences[name].Type().ToString()} with {typeof(T).ToString()}");
				return false;
			}
			else
			{
				return true;
			}
		}

		public EditorPreference<T> GetPref<T>(string name)
		{
			if (!ValidatePreferenceAccess<T>(name))
				return default(EditorPreference<T>);
			else
				return _preferences[name] as EditorPreference<T>;
		}

		public bool Contains(string name)
		{
			return _preferences.ContainsKey(name);
		}

		public List<string> PrefsList() 
		{
			return _preferences.Keys.ToList<string>();
		}

		public List<string> PrefsList(Type type)
		{
			return _preferences.Where(pref => pref.Value.Type() == type).Select(p => p.Key).ToList();
		}

		public void InitializeAll() 
		{
			_preferences.Values.ToList().ForEach(pref => pref.Initialize());
		}

		public void SaveAll()
		{
			_preferences.Values.ToList().ForEach(pref => pref.Save());
		}

        public void ResetAll()
		{
			_preferences.Values.ToList().ForEach(pref => pref.ResetToDefault());
		}

		public void Create(string name, bool 	defaultValue) { this.Add(new EditorPreference<bool>		(name, defaultValue, _prefix)); }
		public void Create(string name, float 	defaultValue) { this.Add(new EditorPreference<float>	(name, defaultValue, _prefix)); }
		public void Create(string name, int  	defaultValue) { this.Add(new EditorPreference<int>		(name, defaultValue, _prefix)); }
		public void Create(string name, string 	defaultValue) { this.Add(new EditorPreference<string>	(name, defaultValue, _prefix)); }
		public void Create(string name, Color 	defaultValue) { this.Add(new EditorPreference<Color>	(name, defaultValue, _prefix)); }

        public void Add(EditorPreference<bool> 		pref) { _preferences.Add(pref.Name, pref); }
		public void Add(EditorPreference<int> 		pref) { _preferences.Add(pref.Name, pref); }
		public void Add(EditorPreference<float> 	pref) { _preferences.Add(pref.Name, pref); }
		public void Add(EditorPreference<string> 	pref) { _preferences.Add(pref.Name, pref); }
		public void Add(EditorPreference<Color> 	pref) { _preferences.Add(pref.Name, pref); }
	}

	public interface IEditorPreference
	{
		void ResetToDefault();
		void Initialize();
		void Save();
		Type Type();
	}

	public interface IEditorPreference<T> : IEditorPreference
	{
		T GetValue();
		T GetDefaultValue();
	}

	public class EditorPreference<T> : IEditorPreference<T>
	{
		string _prefix;
		string _name;
		string _editorPrefsID;
		readonly Type _type;
		public T Value { get; set; }
		public T DefaultValue { get; set; }
		public readonly List<Type> ValidTypes = new List<Type> {typeof(bool), typeof(float), typeof(int), typeof(string), typeof(Color)};

		public Type Type() { return _type; }
		public T GetValue() { return Value; }
		public T GetDefaultValue() { return DefaultValue; }

		public string EditorPrefsID
		{
			get { return _editorPrefsID; }
		}

		// public string Prefix
		// {
		// 	get { return _prefix; }
		// 	set
		// 	{
		// 		_prefix = value;
		// 		_editorPrefsID = CreateEditorPrefsID(value, _name);
		// 	}
		// }

		public string Name
		{
			get { return _name; }
			set
			{
				_name = value;
				_editorPrefsID = CreateEditorPrefsID(_prefix, value);
			}
		}

		private string CreateEditorPrefsID(string prefix, string name)
		{
			return prefix + "." + name;
		}
		
		public EditorPreference(string name, T defaultValue, string prefix)
		{
			if(!ValidTypes.Contains(typeof(T)))
				Debug.LogError("[RATS] Tried to create EditorPreference of type " + typeof(T).ToString() + " - EditorPreference can only be of types: " + string.Join( ",", ValidTypes));

			Name = name;
			DefaultValue = defaultValue;
			Value = defaultValue;
			_prefix = prefix;
			_editorPrefsID = CreateEditorPrefsID(prefix, name);
			_type = typeof(T);
		}

		public void ResetToDefault()
		{
			Value = DefaultValue;
		}

		string ColorToHex(Color color, bool numberSign = false) { return (numberSign ? "#" : "") + ColorUtility.ToHtmlStringRGBA(color); }
		Color HexToColor(string hexColor) { ColorUtility.TryParseHtmlString(hexColor, out Color color); return color; }

		public void Initialize()
		{
			if(!EditorPrefs.HasKey(_editorPrefsID)) this.Save();
			else if(_type == typeof(bool)) Value = (T)(object) EditorPrefs.GetBool(_editorPrefsID, (bool)(object) DefaultValue);
			else if(_type == typeof(float)) Value = (T)(object) EditorPrefs.GetFloat(_editorPrefsID, (float)(object) DefaultValue);
			else if(_type == typeof(int)) Value = (T)(object) EditorPrefs.GetInt(_editorPrefsID, (int)(object) DefaultValue);
			else if(_type == typeof(string)) Value = (T)(object) EditorPrefs.GetString(_editorPrefsID, (string)(object) DefaultValue);
			else if(_type == typeof(Color)) Value = (T)(object) HexToColor(EditorPrefs.GetString(_editorPrefsID, ColorToHex((Color)(object) DefaultValue)));
		}

		public void Save()
		{
			if(_type == typeof(bool)) EditorPrefs.SetBool(_editorPrefsID, (bool)(object) Value);
			else if(_type == typeof(float)) EditorPrefs.SetFloat(_editorPrefsID, (float)(object) Value);
			else if(_type == typeof(int)) EditorPrefs.SetInt(_editorPrefsID, (int)(object) Value);
			else if(_type == typeof(string)) EditorPrefs.SetString(_editorPrefsID, (string)(object) Value);
			else if(_type == typeof(Color)) EditorPrefs.SetString(_editorPrefsID, ColorToHex((Color)(object) Value));
		}
	}
}
#endif