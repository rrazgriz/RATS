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

// Design
// Want to be able to: init a preference with a string, type, and default value
// It should retrieve the current value with EditorPrefs.GetBool etc.
// It should be settable 


namespace Razgriz.RATS
{
	[InitializeOnLoad]
	public static class RATSPrefs
	{
		// public static Dictionary<string, IEditorPrefsHandler> Prefs = new Dictionary<string, IEditorPrefsHandler>();
		public static string PreferencePrefix = "RATS.prefs_";

		static RATSPrefs()
		{
			EditorPrefsHandler<float> testPref = new EditorPrefsHandler<float>("aTestFloat", 1.0f, PreferencePrefix);
			EditorPrefsHandler<float> testPref22 = new EditorPrefsHandler<float>("aTestFloat2", 2.0f, PreferencePrefix);
			EditorPrefsHandler<int> testPref2 = new EditorPrefsHandler<int>("aTestInt", 1, PreferencePrefix);
			EditorPrefsHandler<int> testPref3 = new EditorPrefsHandler<int>("aTestInt2", 1, PreferencePrefix);
			EditorPreferenceGroup Prefs = new EditorPreferenceGroup();
			Prefs.AddPreference(testPref);
			Prefs.AddPreference(testPref2);
			Prefs.AddPreference(testPref22);
			Prefs.AddPreference(testPref3);

			foreach(var name in Prefs.GetPrefsList<float>())
			{
				Debug.Log(name);
				Debug.Log(Prefs.Get<float>(name).Value);
			}
		}

		// public static void AddPref(string name, bool defaultValue) { AddPrefGeneric(name, defaultValue, EditorPrefsType.Bool, PreferencePrefix); }
		// public static void AddPref(string name, float defaultValue) { AddPrefGeneric(name, defaultValue, EditorPrefsType.Float, PreferencePrefix); }
		// public static void AddPref(string name, int defaultValue) { AddPrefGeneric(name, defaultValue, EditorPrefsType.Int, PreferencePrefix); }
		// public static void AddPref(string name, string defaultValue) { AddPrefGeneric(name, defaultValue, EditorPrefsType.Str, PreferencePrefix, false); }
		// public static void AddPref(string name, Color defaultValue) { AddPrefGeneric(name, defaultValue, EditorPrefsType.Color, PreferencePrefix, true); }

		// private static void AddPrefGeneric<T>(string name, T defaultValue, EditorPrefsType type, string prefix, bool isColorValue = false)
		// {
		// 	Prefs.Add(name, new EditorPrefsHandler<T>(name, defaultValue, type, isColorValue));
		// }
	}

	public enum EditorPrefsType : int
	{
		Bool = 0,
		Float = 1,
		Int = 2,
		Str = 3,
		Color = 4
	}

	// public interface IEditorPrefsHandler
	// {
	// 	Type Type { get; }
	// 	string Name { get; set; }
	// }

	public class EditorPreferenceGroup
	{
		readonly Dictionary<string, Type> prefNameList = new Dictionary<string, Type>();
		readonly Dictionary<string, EditorPrefsHandler<bool>> boolList = new Dictionary<string, EditorPrefsHandler<bool>>();
		readonly Dictionary<string, EditorPrefsHandler<int>> intList = new Dictionary<string, EditorPrefsHandler<int>>();
		readonly Dictionary<string, EditorPrefsHandler<float>> floatList = new Dictionary<string, EditorPrefsHandler<float>>();
		readonly Dictionary<string, EditorPrefsHandler<string>> stringList = new Dictionary<string, EditorPrefsHandler<string>>();
		readonly Dictionary<string, EditorPrefsHandler<Color>> colorList = new Dictionary<string, EditorPrefsHandler<Color>>();

		public List<string> GetPrefsList() { return prefNameList.Keys.ToList<string>(); }
		public List<string> GetPrefsList<T>()
		{
			if(typeof(T) == typeof(bool))
				return boolList.Keys.ToList<string>();
			if(typeof(T) == typeof(float))
				return floatList.Keys.ToList<string>();
			if(typeof(T) == typeof(int))
				return intList.Keys.ToList<string>();
			if(typeof(T) == typeof(string))
				return stringList.Keys.ToList<string>();
			if(typeof(T) == typeof(Color))
				return colorList.Keys.ToList<string>();
			else
				throw new ArgumentException("[RATS] Prefs manager: Tried to get prefs list of invalid type", typeof(T).ToString());
		}

		public EditorPrefsHandler<T> Get<T>(string name)
		{
			if(typeof(T) == typeof(bool))
				return boolList[name] as EditorPrefsHandler<T>;
			if(typeof(T) == typeof(float))
				return floatList[name] as EditorPrefsHandler<T>;
			if(typeof(T) == typeof(int))
				return intList[name] as EditorPrefsHandler<T>;
			if(typeof(T) == typeof(string))
				return stringList[name] as EditorPrefsHandler<T>;
			if(typeof(T) == typeof(Color))
				return colorList[name] as EditorPrefsHandler<T>;
			else
				throw new ArgumentException("[RATS] Prefs manager: Tried to get pref of invalid type", typeof(T).ToString());
			return null;
		}

		public void InitPrefs()
		{
			foreach(string prefName in prefNameList.Keys)
			{
				InitPref(prefName);
			}
		}

		public void InitPref(string prefName)
		{
			Type type = prefNameList[prefName];
			if(type == typeof(bool))
				EditorPrefs.GetBool(boolList[prefName].EditorPrefsID, boolList[prefName].DefaultValue);
			if(type == typeof(float))
				EditorPrefs.GetFloat(floatList[prefName].EditorPrefsID, floatList[prefName].DefaultValue);
			if(type == typeof(int))
				EditorPrefs.GetInt(intList[prefName].EditorPrefsID, intList[prefName].DefaultValue);
			if(type == typeof(string))
				EditorPrefs.GetString(stringList[prefName].EditorPrefsID, stringList[prefName].DefaultValue);
			if(type == typeof(Color))
			{
				string colorHexCode = "#" + ColorUtility.ToHtmlStringRGBA(colorList[prefName].DefaultValue);
				EditorPrefs.GetString(colorList[prefName].EditorPrefsID, colorHexCode);
			}
		}

		public void ApplyPrefs()
		{
			foreach(string prefName in prefNameList.Keys)
			{
				ApplyPref(prefName);
			}
		}

		public void ApplyPref(string prefName)
		{
			Type type = prefNameList[prefName];
			if(type == typeof(bool))
				EditorPrefs.SetBool(boolList[prefName].EditorPrefsID, boolList[prefName].Value);
			if(type == typeof(float))
				EditorPrefs.SetFloat(floatList[prefName].EditorPrefsID, floatList[prefName].Value);
			if(type == typeof(int))
				EditorPrefs.SetInt(intList[prefName].EditorPrefsID, intList[prefName].Value);
			if(type == typeof(string))
				EditorPrefs.SetString(stringList[prefName].EditorPrefsID, stringList[prefName].Value);
			if(type == typeof(Color))
			{
				string colorHexCode = "#" + ColorUtility.ToHtmlStringRGBA(colorList[prefName].Value);
				EditorPrefs.SetString(colorList[prefName].EditorPrefsID, colorHexCode);
			}
		}

        public void AddPreference(EditorPrefsHandler<bool> pref) { boolList.Add(pref.Name, pref); prefNameList.Add(pref.Name, typeof(bool)); }
		public void AddPreference(EditorPrefsHandler<int> pref) { intList.Add(pref.Name, pref); prefNameList.Add(pref.Name, typeof(int)); }
		public void AddPreference(EditorPrefsHandler<float> pref) { floatList.Add(pref.Name, pref); prefNameList.Add(pref.Name, typeof(float)); }
		public void AddPreference(EditorPrefsHandler<string> pref) { stringList.Add(pref.Name, pref); prefNameList.Add(pref.Name, typeof(string)); }
		public void AddPreference(EditorPrefsHandler<Color> pref) { colorList.Add(pref.Name, pref); prefNameList.Add(pref.Name, typeof(Color)); }
	}

	public class EditorPrefsHandler<T>
	{
		public string _prefix;
		public String Name { get; set; }
		public string EditorPrefsID {get; set;}
		public T Value { get; set; }
		public T DefaultValue { get; set; }
		private bool isColor { get; set; }
		private Type type {get; set;}

		public EditorPrefsHandler(string name, T defaultValue, string prefix)
		{
			Name = name;
			DefaultValue = defaultValue;
			Value = defaultValue;
			_prefix = prefix;
			EditorPrefsID = prefix + name;
			type = typeof(T);
			isColor = typeof(T) == typeof(Color);
		}

		public void ResetValue()
		{
			Value = DefaultValue;
		}
	}
}
#endif