using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace PriceInsight;

#pragma warning disable 8618
// ReSharper disable UnusedAutoPropertyAccessor.Local
internal class Service {
    [PluginService] internal static ICommandManager CommandManager { get; private set; }
    [PluginService] internal static IClientState ClientState { get; private set; }
    [PluginService] internal static IDataManager DataManager { get; private set; }
    [PluginService] internal static IFramework Framework { get; private set; }
    [PluginService] internal static IGameGui GameGui { get; private set; }
    [PluginService] internal static IKeyState KeyState { get; private set; }
    [PluginService] internal static IPluginLog PluginLog { get; private set; }
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; }
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; }
    
    internal static void Initialize(DalamudPluginInterface pluginInterface) {
        pluginInterface.Create<Service>();
    }
}
#pragma warning restore 8618
