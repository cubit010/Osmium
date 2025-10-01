using System;
using System.Collections.Generic;
using System.IO;

namespace Osmium
{
    public static class EngineApi
    {
        private static readonly Board board = new Board();
        private static readonly TranspositionTable tt = new TranspositionTable(16); // pick size you want

        /// <summary>
        /// Send a UCI command to the engine and get back all lines it produced.
        /// </summary>
        public static string[] SendCommand(string command)
        {
            var outputs = new List<string>();

            // Redirect Console.WriteLine temporarily
            var originalOut = Console.Out;
            using (var writer = new StringWriter())
            {
                Console.SetOut(writer);

                // Use your existing parser
                UCI.ParseRunCommand(command, board, tt);

                Console.Out.Flush();
                var text = writer.ToString();

                // Split into lines, trim empty
                foreach (var line in text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
                {
                    outputs.Add(line);
                }
            }

            // Restore Console
            Console.SetOut(originalOut);

            return outputs.ToArray();
        }

        /// <summary>
        /// Convenience method to run a series of UCI commands in order.
        /// </summary>
        public static IEnumerable<string> SendCommands(params string[] commands)
        {
            foreach (var cmd in commands)
            {
                foreach (var line in SendCommand(cmd))
                    yield return line;
            }
        }
    }
}
