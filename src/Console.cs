using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using Logger;
using MonoMod.RuntimeDetour;
using Onyx_API;
using UnityEngine;

public class Console : MonoBehaviour
{
    [Initializer]
    static void Init()
    {
        GameObject Console = Instantiate(new GameObject("Console"));
        DontDestroyOnLoad(Console);
        Console.AddComponent<Console>();

        {
            int backgroundWidth = 10;
            int backgroundHeight = 10;
            inputBackground = new(backgroundWidth, backgroundHeight);
            string path = System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Images"), "InputBackground.png");
            inputBackground.LoadImage(File.ReadAllBytes(path));
            inputBackground.wrapMode = TextureWrapMode.Clamp;
            inputBackground.filterMode = FilterMode.Point;
        }

        {
            int backgroundWidth = 10;
            int backgroundHeight = 10;
            autocompleteBackground = new(backgroundWidth, backgroundHeight);
            string path = System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Images"), "AutocompleteBackground.png");
            autocompleteBackground.LoadImage(File.ReadAllBytes(path));
            autocompleteBackground.wrapMode = TextureWrapMode.Clamp;
            autocompleteBackground.filterMode = FilterMode.Point;
        }
        

        new Hook(typeof(Screen).GetMethod(nameof(Screen.SetResolution), [typeof(int), typeof(int), typeof(bool)]), Screen_SetResolution);
        On.PlayerControllerN.Update += PlayerControllerN_Update;
        // Application.logMessageReceived += LogWriter;
    }

    // private static void LogWriter(string condition, string stackTrace, LogType type)
    // {
    //     consoleLog.Add(new(type, condition));
    // }

    private static void PlayerControllerN_Update(On.PlayerControllerN.orig_Update orig, PlayerControllerN self)
    {
        if (!consoleOpen)
            orig(self);
    }

    private static void Screen_SetResolution(Action<int, int, bool> orig, int width, int height, bool fullscreen)
    {
        orig(width, height, fullscreen);
        API_Manager.nextFixedUpdate.AddListener(SetWindowSize);
    }

    public static float fixedAge;
    static int fontSize;
    static string inputString = "";
    static List<string> autocompleteStrings = [];
    static float backspaceTimer = 0; 
    static readonly Dictionary<string, Action<object[]>> _commands = [];
    static readonly Dictionary<string, HashSet<string>[]> _autoCompletes = [];
    static readonly Dictionary<string, string> _autoCompletesToUpper = [];
    static bool consoleOpen = false;
    static int _autocompleteIndex = 0;
    static List<KeyValuePair<LogType, string>> consoleLog = [];

    //Visuals
    static Texture2D inputBackground = null;
    static Texture2D autocompleteBackground = null;
    static Rect inputTextBox;
    static Rect consoleHistoryBox;
    static Rect[] autocompleteSuggestionBoxes = new Rect[20];
    static GUIStyle inputBoxStyle;
    static GUIStyle autoCompleteBoxStyle;

    void Start()
    {
        SetWindowSize();
    }

    static void SetWindowSize()
    {
        fontSize = Screen.width / 50;

        inputTextBox = new Rect(0,Screen.height - Screen.height / 25, Screen.width, Screen.height / 25);
        for(int i = 0; i < autocompleteSuggestionBoxes.Length; i++)
        {
            autocompleteSuggestionBoxes[i] = new Rect(20,Screen.height - Screen.height / 25 - (Screen.height / 25 * (i + 1)), Screen.width, Screen.height / 25);
        }

        consoleHistoryBox = new Rect(0, 0, Screen.width, Screen.height - Screen.height / 25);
        
        inputBoxStyle = new GUIStyle
        {
            border = new RectOffset(1, 1, 1, 1)
        };
        inputBoxStyle.normal.background = inputBackground;
        inputBoxStyle.fontSize = fontSize;
        inputBoxStyle.alignment = TextAnchor.MiddleLeft;
        inputBoxStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);

        autoCompleteBoxStyle = new GUIStyle(inputBoxStyle);
        autoCompleteBoxStyle.normal.background = autocompleteBackground;
    }

    void FixedUpdate()
    {
        fixedAge += Time.fixedDeltaTime;
    }

    void Update()
    {
        if (UnityEngine.Input.GetKeyDown(KeyCode.BackQuote))
        {
            consoleOpen = !consoleOpen;
            UpdateInputText();
            return;
        }
        if (!consoleOpen)
            return;
        bool updatedString = false;
        string newTyped = Regex.Replace(UnityEngine.Input.inputString, @"[^\x20-\x7F]", "");
        if (newTyped != string.Empty)
        {
            inputString += newTyped;
            updatedString = true;
        }

        if (UnityEngine.Input.GetKeyDown(KeyCode.Backspace) && inputString.Length > 0)
        {
            inputString= inputString.Remove(inputString.Length - 1);
            updatedString = true;
        }
        if (UnityEngine.Input.GetKey(KeyCode.Backspace) && inputString.Length > 0)
        {
            if (backspaceTimer < 0)
            {
                inputString= inputString.Remove(inputString.Length - 1);
                updatedString = true;
                backspaceTimer += 0.05f;
            }
            backspaceTimer -= Time.deltaTime;
        } else
        {
            backspaceTimer = 0.3f;
        }

        if (UnityEngine.Input.GetKey(KeyCode.Tab))
        {
            if (autocompleteStrings.Count > 0)
                inputString = autocompleteStrings[0];
        }
        
        if (UnityEngine.Input.GetKey(KeyCode.Return))
        {
            string[] commandstring = inputString.Split(' ');
            if (_commands.TryGetValue(commandstring[0], out Action<object[]> command))
            {
                object[] commandParameters = new object[commandstring.Length-1];
                for(int i = 0; i < commandParameters.Length; i++)
                {
                    commandParameters[i] = commandstring[i+1];
                    if (int.TryParse(commandstring[i+1], out int intParam))
                    {
                        commandParameters[i] = intParam;
                    }
                }
                try
                {
                    command.Invoke(commandParameters);
                } catch (Exception e)
                {
                    Log.Error(e);
                }
            } else
            {
                if (!commandstring[0].IsNullOrWhiteSpace())
                {
                    inputString = "";
                    throw new ArgumentException(commandstring[0] + " is not a valid command");
                }
            }
            inputString = "";
            updatedString = true;
        }

        if (updatedString)
        {
            UpdateInputText();
        }
    }

    private void UpdateInputText()
    {
        string[] commandstring = inputString.Split(' ');
        autocompleteStrings = [];
        if (commandstring.Length < 1)
            return;
        if (commandstring.Length == 1)
        {
            foreach(var autocomplete in _autoCompletes)
            {
                if (autocomplete.Key.Contains(commandstring[0].ToLower()))
                {
                    autocompleteStrings.Add(_autoCompletesToUpper[autocomplete.Key]);
                }
            }
        }
        if(!_autoCompletes.TryGetValue(commandstring[0].ToLower(), out var commandParams) || _autoCompletes[commandstring[0].ToLower()].Length < commandstring.Length-1)
            return;
        string previousCommand = "";
        if (commandstring.Length > 1)
        {
            for(int i = 0; i < commandstring.Length - 1; i++)
                previousCommand += commandstring[i] + " ";
            foreach(var autocomplete in commandParams[commandstring.Length-2])
            {
                if (autocomplete.Contains(commandstring[commandstring.Length-1].ToLower()))
                {
                    autocompleteStrings.Add(previousCommand + _autoCompletesToUpper[autocomplete]);
                }
            }
        }
    }

    public static void RegisterCommand(string name, Action<object[]> action, HashSet<string>[] autocomplete = null)
    {
        autocomplete ??= [];
        _commands.Add(name, action);
        HashSet<string>[] toLower = new HashSet<string>[autocomplete.Length];

        for(int i = 0; i < autocomplete.Length; i++)
        {
            toLower[i] = [];
            foreach(var entry in autocomplete[i])
            {
                _autoCompletesToUpper.Add(entry.ToLower(), entry);
                toLower[i].Add(entry.ToLower());
            }
        }
        _autoCompletes.Add(name.ToLower(), toLower);
        _autoCompletesToUpper.Add(name.ToLower(), name);
    }

    void OnGUI()
    {
        if (!consoleOpen)
            return;
        GUIStyle style = GUI.skin.GetStyle ("label");
        style.fontSize = fontSize;

        GUI.Box(consoleHistoryBox, "");
        if (fixedAge % 1 < 0.5)
        {
            GUI.Box(inputTextBox, " > " + inputString, inputBoxStyle);
        } else
        {
            GUI.Box(inputTextBox, " > " + inputString + "|", inputBoxStyle);
        }

        for(int i = 0; i < autocompleteSuggestionBoxes.Length; i++)
        {
            if (i >= autocompleteStrings.Count)
                break;
            GUI.Box(autocompleteSuggestionBoxes[i], " " + autocompleteStrings[i], autoCompleteBoxStyle);
        }
    }
}
