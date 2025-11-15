using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFRMiniPoker
{
    public  interface IPlayer<TAction>
    {
        public TAction GetMove(int player, string information_set, IReadOnlyList<TAction> actions);

    }

    public class RandomPlayer<TAction> : IPlayer<TAction>
    {
        private static readonly Random _random = new Random();
        public TAction GetMove(int player, string information_set, IReadOnlyList<TAction> actions)
        {
            int index = _random.Next(0, actions.Count);
            return actions[index];
        }
    }

    public class HumanPlayer<TAction> : IPlayer<TAction>
    {
        public TAction GetMove(int player, string information_set, IReadOnlyList<TAction> actions)
        {
            Console.WriteLine($"Player {player}'s turn. Information Set: {information_set}");
            Console.WriteLine("Available actions:");
            for (int i = 0; i < actions.Count; i++)
            {
                Console.WriteLine($"{i}: {actions[i]}");
            }
            int choice = -1;
            while (choice < 0 || choice >= actions.Count)
            {
                Console.Write("Enter the number of your chosen action: ");
                string input = Console.ReadLine();
                if (int.TryParse(input, out choice) && choice >= 0 && choice < actions.Count)
                {
                    break;
                }
                Console.WriteLine("Invalid choice. Please try again.");
            }
            return actions[choice];
        }
    }

    public class MultiPlayer<TAction> : IPlayer<TAction>
    {
        public readonly List<IPlayer<TAction>> _players;
        public MultiPlayer(List<IPlayer<TAction>> players)
        {
            _players = players;
        }
        public TAction GetMove(int player, string information_set, IReadOnlyList<TAction> actions)
        {
            return _players[player].GetMove(player, information_set, actions);
        }
    }

}
