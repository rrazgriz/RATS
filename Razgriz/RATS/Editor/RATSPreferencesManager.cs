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

		public EditorPreferenceGroup() { }
		public EditorPreferenceGroup(string prefix) { _prefix = prefix; }

		readonly Dictionary<string, Type> prefsNameList = new Dictionary<string, Type>();
		readonly Dictionary<string, IEditorPreference> preferences = new Dictionary<string, IEditorPreference>();
		
		readonly Dictionary<string, EditorPreference<bool>> boolPrefs;
		readonly Dictionary<string, EditorPreference<float>> floatPrefs;
		readonly Dictionary<string, EditorPreference<int>> intPrefs;
		readonly Dictionary<string, EditorPreference<string>> stringPrefs;
		readonly Dictionary<string, EditorPreference<Color>> colorPrefs;

		public T GetValue<T>(string name) { return GetPref<T>(name).Value; }
		public T GetDefaultValue<T>(string name) { return GetPref<T>(name).DefaultValue; }
		public EditorPreference<T> GetPref<T>(string name)
		{
			return preferences[name] as EditorPreference<T>;

			// if(typeof(T) == typeof(bool)) return boolPrefs[name] as EditorPreference<T>;
			// else if(typeof(T) == typeof(float)) return floatPrefs[name] as EditorPreference<T>;
			// else if(typeof(T) == typeof(int)) return intPrefs[name] as EditorPreference<T>;
			// else if(typeof(T) == typeof(string)) return stringPrefs[name] as EditorPreference<T>;
			// else if(typeof(T) == typeof(Color)) return colorPrefs[name] as EditorPreference<T>;
			// else throw new ArgumentException("[RATS] Prefs manager: Tried to get pref of invalid type: " + typeof(T).ToString(), nameof(T));
		}

		public List<string> PrefsList() { return preferences.Keys.ToList<string>(); }
		// public List<string> PrefsList<T>() { return PrefsList(typeof(T)); }
		public List<string> PrefsList(Type type)
		{
			var list = new List<string>();
			foreach(var item in preferences)
			{
				if(item.Value.Type() == type) list.Add(item.Key);
			}

			return list;
		}

        // public List<IEditorPreference> GetAllPrefs()
        // {
		// 	return boolPrefs.Values.ToList().Cast<IEditorPreference>()
		// 	.Concat(floatPrefs.Values.ToList().Cast<IEditorPreference>())
		// 	.Concat(intPrefs.Values.ToList().Cast<IEditorPreference>())
		// 	.Concat(stringPrefs.Values.ToList().Cast<IEditorPreference>())
		// 	.Concat(colorPrefs.Values.ToList().Cast<IEditorPreference>())
		// 	.ToList();
        // }

		public void InitializeAll() { preferences.Values.ToList().ForEach(pref => pref.Initialize()); }

		public void SaveAll() { preferences.Values.ToList().ForEach(pref => pref.Save()); }

        public void ResetToDefault() { preferences.Values.ToList().ForEach(pref => pref.ResetToDefault());}


		// public void InitializeAll() { GetAllPrefs().ForEach(pref => pref.Initialize()); }

		// public void SaveAll() { GetAllPrefs().ForEach(pref => pref.Save()); }

        // public void ResetToDefault() { GetAllPrefs().ForEach(pref => pref.ResetToDefault());}

		// public void Create<T>(string name, T defaultValue)
		// {
		// 	if(typeof(T) == typeof(bool)) 			this.Add(new EditorPreference<bool>(name, (bool)(object)defaultValue, _prefix));
		// 	else if(typeof(T) == typeof(float)) 	this.Add(new EditorPreference<float>(name, (float)(object)defaultValue, _prefix));
		// 	else if(typeof(T) == typeof(int)) 		this.Add(new EditorPreference<int>(name, (int)(object)defaultValue, _prefix));
		// 	else if(typeof(T) == typeof(string)) 	this.Add(new EditorPreference<string>(name, (string)(object)defaultValue, _prefix));
		// 	else if(typeof(T) == typeof(Color)) 	this.Add(new EditorPreference<Color>(name, (Color)(object)defaultValue, _prefix));
		// 	else throw new ArgumentException("[RATS] Prefs manager: Tried to get prefs list of invalid type: " + typeof(T).ToString(), nameof(defaultValue));
		// }

		public void Create(string name, bool 	defaultValue) { preferences.Add(name, new EditorPreference<bool>(name, (bool)(object)defaultValue, _prefix)); }
		public void Create(string name, float 	defaultValue) { preferences.Add(name, new EditorPreference<float>(name, (float)(object)defaultValue, _prefix)); }
		public void Create(string name, int  	defaultValue) { preferences.Add(name, new EditorPreference<int>(name, (int)(object)defaultValue, _prefix)); }
		public void Create(string name, string 	defaultValue) { preferences.Add(name, new EditorPreference<string>(name, (string)(object)defaultValue, _prefix)); }
		public void Create(string name, Color 	defaultValue) { preferences.Add(name, new EditorPreference<Color>(name, (Color)(object)defaultValue, _prefix)); }

        public void Add(EditorPreference<bool> 		pref) { preferences.Add(pref.Name, pref); }
		public void Add(EditorPreference<int> 		pref) { preferences.Add(pref.Name, pref); }
		public void Add(EditorPreference<float> 	pref) { preferences.Add(pref.Name, pref); }
		public void Add(EditorPreference<string> 	pref) { preferences.Add(pref.Name, pref); }
		public void Add(EditorPreference<Color> 	pref) { preferences.Add(pref.Name, pref); }

        // public void Add(EditorPreference<bool> 		pref) { boolPrefs.Add(pref.Name, pref); prefsNameList.Add(pref.Name, typeof(bool)); 	preferences.Add(pref.Name, pref as IEditorPreference); }
		// public void Add(EditorPreference<int> 		pref) { intPrefs.Add(pref.Name, pref); prefsNameList.Add(pref.Name, typeof(int)); 		preferences.Add(pref.Name, pref as IEditorPreference); }
		// public void Add(EditorPreference<float> 	pref) { floatPrefs.Add(pref.Name, pref); prefsNameList.Add(pref.Name, typeof(float)); 	preferences.Add(pref.Name, pref as IEditorPreference); }
		// public void Add(EditorPreference<string> 	pref) { stringPrefs.Add(pref.Name, pref); prefsNameList.Add(pref.Name, typeof(string)); preferences.Add(pref.Name, pref as IEditorPreference); }
		// public void Add(EditorPreference<Color> 	pref) { colorPrefs.Add(pref.Name, pref); prefsNameList.Add(pref.Name, typeof(Color)); 	preferences.Add(pref.Name, pref as IEditorPreference); }
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

		public string Prefix
		{
			get { return _prefix; }
			set
			{
				_prefix = value;
				_editorPrefsID = CreateEditorPrefsID(value, _name);
			}
		}

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