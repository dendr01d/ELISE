
namespace LIZa
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var Elise = ELISE.ELIZA.Eliza.FromScript(@".\PseudoDoctor.txt");

            string? input = null;

            while (input != "quit")
            {
                if (input != null && Elise.Respond(input) is string response && response != null)
                {
                    Console.WriteLine(response);

                    if (TikTok.Weilbyte.API.TextWithinLimit(response))
                    {
                        TikTok.Weilbyte.API.SpeakText(response);
                    }
                }

                Console.Write("> ");
                input = Console.ReadLine();
            }
        }
    }
}