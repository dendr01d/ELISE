
namespace LIZa
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string? input = null;

            while (input != "quit")
            {
                if (input != null)
                {
                    if (TikTok.Weilbyte.API.TextWithinLimit(input))
                    {
                        TikTok.Weilbyte.API.SpeakText(input);
                    }
                    else
                    {
                        Console.WriteLine("(That text is too long!)");
                    }
                }

                Console.Write("> ");
                input = Console.ReadLine();
            }

            System.Console.ReadKey(true);
        }
    }
}