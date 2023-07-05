
using System.Text;

namespace LIZa
{
    internal class Program
    {
        private static Random RNG = new Random();

        static void Main(string[] args)
        {
            var Elise = ELISE.ELIZA.Eliza.FromScript(@".\TikTokDoctor.txt");

            if (ELISE.ELIZA.ScriptReader.Errors.Any())
            {
                Console.WriteLine("Found errors in script -->");

                foreach(string line in ELISE.ELIZA.ScriptReader.Errors)
                {
                    Console.WriteLine(line);
                }

                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey(true);
            }

            TikTok.Weilbyte.API.Initialize();
            ELISE.ELIZA.Hollerith.Initialize();

            string? input = null;

            int index = 0;
            string[] inputs = GetTestInputs();

            ConsoleKeyInfo? key = null;

            while (key == null || (key is ConsoleKeyInfo k && k.KeyChar != 'b'))
            {
                key = Console.ReadKey(true);
            }

            while (input == null || !ELISE.ELIZA.Eliza.QuitterTalk.Any(x => input.Contains(x)))
            {
                StringBuilder explainer = new();

                string response = input == null
                    ? Elise.ProduceGreeting()
                    : Elise.CreateClassicalResponse(input, out explainer);

                if (true)
                {
                    Console.WriteLine();
                    Console.WriteLine(explainer.ToString());
                }

                int voiceDuration = 1000;

                try
                {
                    Task.Run(() =>
                    {
                        if (TikTok.Weilbyte.API.TextWithinLimit(response))
                        {
                            TikTok.Weilbyte.API.SpeakText(response, out voiceDuration);
                        }
                    });
                }
                catch
                {
                    Console.WriteLine("<Error retrieving voice message>");
                }

                SlowPrint(response.ToUpper());
                //Console.WriteLine(response.ToUpper());

                Console.Write("> ");

                System.Threading.Thread.Sleep(500);
                //Console.WriteLine(inputs[index]);
                SlowPrint(inputs[index], 100, true);
                System.Threading.Thread.Sleep(250);

                input = inputs[index]; // Console.ReadLine();

                index++;
            }

            Console.ReadKey(true);
        }

        private static void SlowPrint(string output, int pauseDuration = 1000 / 15, bool useRandom = false)
        {
            //apparently the original hardware eliza ran on could only print 15 characters per second

            //const int pauseDuration = 1000 / 15;

            foreach(char c in output)
            {
                if (useRandom)
                {
                    System.Threading.Thread.Sleep(pauseDuration + RNG.Next(-1 * pauseDuration / 10, pauseDuration / 5));
                }
                else
                {
                    System.Threading.Thread.Sleep(pauseDuration);
                }

                Console.Write(c);
            }
            System.Threading.Thread.Sleep(pauseDuration);

            Console.WriteLine();
        }

        private static string[] GetTestInputs()
        {
            return new string[]
            {
                //"Good morning.",
                "Men are all alike.",
                "They're always bugging us about something or other.",
                "Well, my boyfriend made me come here.",
                "He says I'm depressed much of the time.",
                "It's true. I am unhappy.",
                "I need some help, that much seems certain.",
                "Perhaps I could learn to get along with my mother.",
                "My mother takes care of me.",
                "My father.",
                "You are like my father in some ways.",
                "You are not very aggressive, but I think you don't want me to notice that.",
                "You don't argue with me.",
                "You are afraid of me.",
                "My father is afraid of everybody.",
                "Bullies.",
                "That's enough for now, goodbye.",
            };
        }
    }
}