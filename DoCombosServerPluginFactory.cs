using Photon.Hive.Plugin;

namespace DoCombosServerPlugin
{
    class DoCombosServerPluginFactory : PluginFactoryBase
    {
        public override IGamePlugin CreatePlugin(string pluginName)
        {
            return new DoCombosServerPlugin();
        }
    }
}
