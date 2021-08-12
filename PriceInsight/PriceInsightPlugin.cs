using System.Reflection;
using Dalamud.Game.Command;
using Dalamud.Plugin;

namespace PriceInsight
{
    public class PriceInsightPlugin : IDalamudPlugin
    {
        public string Name => "PriceInsight";

        public DalamudPluginInterface PluginInterface { get; private set; }
        
        public Configuration Configuration { get; private set; }
        public ItemPriceTooltip ItemPriceTooltip { get; private set;  }
        public Hooks Hooks { get; private set; }
        public ItemPriceLookup ItemPriceLookup { get; private set; }
        public UniversalisClient UniversalisClient { get; private set; }
        
        private ConfigUI ui;
        
        public string AssemblyLocation { get => assemblyLocation; set => assemblyLocation = value; }

        private string assemblyLocation = Assembly.GetExecutingAssembly().Location;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            PluginInterface = pluginInterface;
            
            Configuration = Configuration.Get(pluginInterface);

            UniversalisClient = new UniversalisClient();
            ItemPriceLookup = new ItemPriceLookup(this);
            ItemPriceTooltip = new ItemPriceTooltip(this);
            Hooks = new Hooks(this);

            ui = new ConfigUI(Configuration);

            PluginInterface.CommandManager.AddHandler("/priceinsight", new CommandInfo((_, _) => OpenConfigUI())
            {
                HelpMessage = "Price Insight Configuration Menu"
            });

            PluginInterface.UiBuilder.OnBuildUi += () => ui.Draw();
            PluginInterface.UiBuilder.OnOpenConfigUi += (_, _) => OpenConfigUI();
        }

        public void Dispose()
        {
            Hooks.Dispose();
            ui.Dispose();

            PluginInterface.CommandManager.RemoveHandler("/priceinsight");
            PluginInterface.Dispose();
        }

        private void OpenConfigUI()
        {
            ui.SettingsVisible = true;
        }
    }
}
