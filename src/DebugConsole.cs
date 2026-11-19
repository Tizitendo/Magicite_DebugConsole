using BepInEx;
using Logger;

namespace DebugConsole;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public class DebugConsole : BaseUnityPlugin
{
	public const string PluginGUID = "com." + PluginAuthor + "." + PluginName;
	public const string PluginAuthor = "Onyx";
	public const string PluginName = "DebugConsole";
	public const string PluginVersion = "1.0.0";

	public void Awake()
	{
		Log.Init(Logger);
	}
}
