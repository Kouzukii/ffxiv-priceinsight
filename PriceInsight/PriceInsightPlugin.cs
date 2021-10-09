using System.Reflection;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Plugin;

namespace PriceInsight
{
    public class PriceInsightPlugin : IDalamudPlugin
    {
        public string Name => "PriceInsight";

        public DalamudPluginInterface PluginInterface { get; private set; }
        public CommandManager CommandManager { get; private set; }
        public ClientState ClientState { get; private set; }
        public DataManager DataManager { get; private set; }
        public SigScanner SigScanner { get; private set; }
        public Framework Framework { get; private set; }
        public GameGui GameGui { get; private set; }
        
        public Configuration Configuration { get; private set; }
        public ItemPriceTooltip ItemPriceTooltip { get; private set;  }
        public Hooks Hooks { get; private set; }
        public ItemPriceLookup ItemPriceLookup { get; private set; }
        public UniversalisClient UniversalisClient { get; private set; }
        
        private ConfigUI ui;
        
        public string AssemblyLocation { get => assemblyLocation; set => assemblyLocation = value; }

        private string assemblyLocation = Assembly.GetExecutingAssembly().Location;

        public PriceInsightPlugin(
	        DalamudPluginInterface pluginInterface,
	        CommandManager commandManager,
	        ClientState clientState,
	        DataManager dataManager,
	        SigScanner sigScanner,
		    Framework framework,
			GameGui gameGui
	    ) {
            this.PluginInterface = pluginInterface;
            this.CommandManager = commandManager;
            this.ClientState = clientState;
            this.DataManager = dataManager;
            this.SigScanner = sigScanner;
            this.Framework = framework;
            this.GameGui = gameGui;
            
            Configuration = Configuration.Get(pluginInterface);
            
            UniversalisClient = new UniversalisClient();
            ItemPriceLookup = new ItemPriceLookup(this);
            ItemPriceTooltip = new ItemPriceTooltip(this);
            Hooks = new Hooks(this);
            
            ui = new ConfigUI(Configuration);
            
            CommandManager.AddHandler("/priceinsight", new CommandInfo((_, _) => OpenConfigUI())
            {
                HelpMessage = "Price Insight Configuration Menu"
            });
            
            PluginInterface.UiBuilder.Draw += () => ui.Draw();
            PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUI;
        }

        public void Dispose()
        {
            Hooks.Dispose();
            ui.Dispose();

            CommandManager.RemoveHandler("/priceinsight");
            PluginInterface.Dispose();
        }

        private void OpenConfigUI()
        {
            ui.SettingsVisible = true;
        }
    }
}
