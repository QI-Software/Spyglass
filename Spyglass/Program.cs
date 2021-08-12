using System.Threading.Tasks;

namespace Spyglass
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var bot = new Bot();
            await bot.MainAsync(args);
        }
    }
}