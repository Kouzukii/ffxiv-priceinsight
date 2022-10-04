using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.IoC;
using Dalamud.Plugin;

namespace PriceInsight;

#pragma warning disable 8618
// ReSharper disable UnusedAutoPropertyAccessor.Local
internal class Service {
    [PluginService] internal static CommandManager CommandManager { get; private set; }
    [PluginService] internal static ClientState ClientState { get; private set; }
    [PluginService] internal static DataManager DataManager { get; private set; }
    [PluginService] internal static Framework Framework { get; private set; }
    [PluginService] internal static GameGui GameGui { get; private set; }
    [PluginService] internal static KeyState KeyState { get; private set; }
    
    internal static void Initialize(DalamudPluginInterface pluginInterface) {
        pluginInterface.Create<Service>();
    }
}
#pragma warning restore 8618
