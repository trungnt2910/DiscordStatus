using Tizen.Applications;
using Uno.UI.Runtime.Skia;

namespace DiscordStatus.Skia.Tizen
{
    class Program
    {
        static void Main(string[] args)
        {
            var host = new TizenHost(() => new DiscordStatus.App(), args);
            host.Run();
        }
    }
}
