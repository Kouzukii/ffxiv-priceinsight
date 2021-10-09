using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Plugin;

namespace PriceInsight {
    public class PriceInsightPlugin : IDalamudPlugin {
        public string Name => "PriceInsight";

        public DalamudPluginInterface PluginInterface { get; }
        public CommandManager CommandManager { get; }
        public ClientState ClientState { get; }
        public DataManager DataManager { get; }
        public SigScanner SigScanner { get; }
        public Framework Framework { get; }
        public GameGui GameGui { get; }

        public Configuration Configuration { get; }
        public ItemPriceTooltip ItemPriceTooltip { get; }
        public Hooks Hooks { get; }
        public ItemPriceLookup ItemPriceLookup { get; }
        public UniversalisClient UniversalisClient { get; }

        private readonly ConfigUI ui;

        public PriceInsightPlugin(DalamudPluginInterface pluginInterface, CommandManager commandManager, ClientState clientState, DataManager dataManager,
            SigScanner sigScanner, Framework framework, GameGui gameGui) {
            PluginInterface = pluginInterface;
            CommandManager = commandManager;
            ClientState = clientState;
            DataManager = dataManager;
            SigScanner = sigScanner;
            Framework = framework;
            GameGui = gameGui;

            Configuration = Configuration.Get(pluginInterface);

            UniversalisClient = new UniversalisClient();
            ItemPriceLookup = new ItemPriceLookup(this);
            ItemPriceTooltip = new ItemPriceTooltip(this);
            Hooks = new Hooks(this);

            ui = new ConfigUI(Configuration);

            CommandManager.AddHandler("/priceinsight", new CommandInfo((_, _) => OpenConfigUI()) { HelpMessage = "Price Insight Configuration Menu" });

            PluginInterface.UiBuilder.Draw += () => ui.Draw();
            PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUI;
        }

        public void Dispose() {
            Hooks.Dispose();
            ui.Dispose();

            CommandManager.RemoveHandler("/priceinsight");
            PluginInterface.Dispose();
        }

        private void OpenConfigUI() {
            ui.SettingsVisible = true;
        }
    }
}