using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace MiniPokerSolver
{
    public static class Constants
    {
        public const int Ante = 2;
        public const int Bet = 1;
        public const int Raise = 3;
        public static string[] Descriptions => new[] { "       Bet", 
                                                       "  Continue", 
                                                       "     Raise", 
                                                       "Call Raise" };
        public static string CardNames =>  "            A       K       Q       J       10      9       8       7       6       5       4       3       2" ;
    }

    public struct Strategy
    {
        public double[][] Table;

        public Strategy(double[][] Table)
        {
            this.Table = Table;
        }

        public Strategy DeepCopy()
        {
            var newTable = new double[Table.Length][];
            for (int i = 0; i < Table.Length; i++)
            {
                newTable[i] = new double[Table[i].Length];
                Array.Copy(Table[i], newTable[i], Table[i].Length);
            }
            return new Strategy(newTable);
        }

        public static Strategy Uniform(int hands, int spots)
        {
            var tab = new double[hands][];
            foreach (var h in Enumerable.Range(0, hands))
            {
                tab[h] = new double[spots];
                foreach (var s in Enumerable.Range(0, spots))
                {
                    tab[h][s] = 0.5;
                }
            }

            return new Strategy(tab);
        }
        public static Strategy Random(int hands, int spots)
        {
            var tab = new double[hands][];
            foreach (var h in Enumerable.Range(0, hands))
            {
                tab[h] = new double[spots];
                foreach (var s in Enumerable.Range(0, spots))
                {
                    tab[h][s] = System.Random.Shared.NextDouble();
                }
            }

            return new Strategy(tab);
        }

        public Strategy Twiddle(int hands, int spots = 4)
        {
            var tab = new double[hands][];
            foreach (var h in Enumerable.Range(0, hands))
            {
                tab[h] = new double[spots];
                foreach (var s in Enumerable.Range(0, spots))
                {
                    tab[h][s] = Table[h][s];
                }
            }
            int hand = System.Random.Shared.Next(0, hands);
            int spot = System.Random.Shared.Next(0, spots);

            double delta = (System.Random.Shared.NextDouble() - 0.5) * 0.01;
            var v = tab[hand][spot];
            v += delta;
            v = Math.Max(v, 0.0001);
            v = Math.Min(v, 0.9999);
            tab[hand][spot] = v;

            return new Strategy(tab);
        }

        

        public override string ToString()
        {
            var hands = Table.Length;
            var spots = 4;
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(Constants.CardNames);

            foreach (var s in Enumerable.Range(0, spots))
            {
                sb.Append(Constants.Descriptions[s] + ": ");
                foreach (var h in Enumerable.Range(0, hands))
                {
                    sb.Append(Table[h][s].ToString("0.000") + " , ");
                }
                sb.Append("\n");
            }

            return sb.ToString();
        }

    }
    public class Solver
    {
        private (Strategy, double, bool) KeepIfImprovementA(Strategy strategyA, Strategy strategyB, int hand, int spot, double delta, double initialValue)
        {
            Strategy trialA = strategyA.DeepCopy();
            trialA.Table[hand][spot] = Math.Min(1.0, Math.Max(0.0, trialA.Table[hand][spot] + delta));
            double newValue = Evaluate(trialA, strategyB);
            
            if (newValue > initialValue)
            {
                return (trialA, newValue, true);
            }
            
            return (strategyA, initialValue, false);
        }

        private (Strategy, double, bool) KeepIfImprovementB(Strategy strategyA, Strategy strategyB, int hand, int spot, double delta, double initialValue)
        {
            Strategy trialB = strategyB.DeepCopy();
            trialB.Table[hand][spot] = Math.Min(1.0, Math.Max(0.0, trialB.Table[hand][spot] + delta));
            double newValue = Evaluate(strategyA, trialB);
            
            if (newValue < initialValue)  // Note the < comparison since B wants to minimize A's payoff
            {
                return (trialB, newValue, true);
            }
            
            return (strategyB, initialValue, false);
        }

        public (Strategy A, Strategy B) Solve_RandomWalk(int iterations = 1001 , int cards = 3, int spots = 4)
        {
            Strategy A = Strategy.Random(cards, spots);
            Strategy B = Strategy.Random(cards, spots);
            double value = Evaluate(A, B);

            for (int i = 0; i < iterations; i++)
            {
                int hand = System.Random.Shared.Next(0, cards);
                int spot = System.Random.Shared.Next(0, spots);
                double delta = (System.Random.Shared.NextDouble() - 0.5) * 0.01;

                // Try improving A's strategy
                (A, value, _) = KeepIfImprovementA(A, B, hand, spot, delta, value);

                // Try improving B's strategy
                (B, value, _) = KeepIfImprovementB(A, B, hand, spot, delta, value);

                if (i % 1000 == 0)
                {
                    Console.WriteLine("Best Strategy for A (First to act):\n" + A.ToString());
                    Console.WriteLine("Best Strategy for B (Last to act):\n" + B.ToString());
                    Console.WriteLine("Expected Value for A:" + value);
                }
            }

            Console.WriteLine("Best Strategy for A (First to act):\n" + A.ToString());
            Console.WriteLine("Best Strategy for B (Last to act):\n" + B.ToString());
            Console.WriteLine("Expected Value for A:" + value);

            return (A, B);
        }

        public (Strategy A, Strategy B) Solve_Iterative(int iterations = 1000, int cards = 3, int spots = 4, double temperature = 0.1, double coolingRate = 0.997, double minimumTemperature = 0.00001)
        {
            Strategy A = Strategy.Uniform(cards, spots);
            Strategy B = Strategy.Uniform(cards, spots);
            double currentValue = Evaluate(A, B);

            for (int i = 0; i < iterations; i++)
            {
                if (temperature < minimumTemperature)
                {
                    Console.WriteLine($"Stopping early at iteration {i} - temperature {temperature} below minimum {minimumTemperature}");
                    break;
                }

                int entriesUpdatedThisIteration = 0;
                double startingValueForA = currentValue;

                // Try to improve A's strategy for each hand and spot
                foreach (var h in Enumerable.Range(0, cards))
                {
                    foreach (var s in Enumerable.Range(0, spots))
                    {
                        // Try both positive and negative temperature changes
                        (Strategy newA, double newValue, bool improved) = KeepIfImprovementA(A, B, h, s, temperature, currentValue);
                        if (improved)
                        {
                            A = newA;
                            currentValue = newValue;
                            entriesUpdatedThisIteration++;
                        }

                        (newA, newValue, improved) = KeepIfImprovementA(A, B, h, s, -temperature, currentValue);
                        if (improved)
                        {
                            A = newA;
                            currentValue = newValue;
                            entriesUpdatedThisIteration++;
                        }
                    }
                }

                double endingValueForA = currentValue;

                // Try to improve B's strategy for each hand and spot
                foreach (var h in Enumerable.Range(0, cards))
                {
                    foreach (var s in Enumerable.Range(0, spots))
                    {
                        // Try both positive and negative temperature changes
                        (Strategy newB, double newValue, bool improved) = KeepIfImprovementB(A, B, h, s, temperature, currentValue);
                        if (improved)
                        {
                            B = newB;
                            currentValue = newValue;
                            entriesUpdatedThisIteration++;
                        }

                        (newB, newValue, improved) = KeepIfImprovementB(A, B, h, s, -temperature, currentValue);
                        if (improved)
                        {
                            B = newB;
                            currentValue = newValue;
                            entriesUpdatedThisIteration++;
                        }
                    }
                }

                if (i % 100 == 0)
                {
                    Console.WriteLine("Iteration " + i + ", Temperature: " + temperature + ", Entries Updated: " + entriesUpdatedThisIteration);
                    Console.WriteLine("     A strategy starting value: " + startingValueForA + ", ending value: " + endingValueForA + ", change: " + (endingValueForA - startingValueForA));
                }

                if (entriesUpdatedThisIteration == 0)
                {
                    Console.WriteLine("No improvements this iteration (" + i + "). Temperature: " + temperature);
                    temperature = Math.Pow(temperature, 10);
                }
                else 
                {
                    temperature *= coolingRate;
                }
            }

            return (A, B);
        }

        public (Strategy A, Strategy B) Solve_MonteCarlo(int iterations = 1000, int cards = 3, int spots = 4)
        {
            Strategy A = Strategy.Random(cards, spots);
            Strategy B = Strategy.Random(cards, spots);
            double value = 0;

            for (int i = 0; i < iterations; i++)
            {
                var trialA = Strategy.Random(cards, spots);
                var trialVal = Evaluate(trialA, B);
                if (trialVal > value)
                {
                    value = trialVal;
                    A = trialA;
                    Console.WriteLine("Updated A, value is now " + value);
                }

                var trialB = Strategy.Random(cards, spots);
                trialVal = Evaluate(A, trialB);
                if (trialVal < value)
                {
                    value = trialVal;
                    B = trialB;
                    Console.WriteLine("Updated B, value is now " + value);
                }


            }

            Console.WriteLine("Best Strategy for A (First to act):\n" + A.ToString());
            Console.WriteLine("Best Strategy for B (Last to act):\n" + B.ToString());
            Console.WriteLine("Expected Value for A:" + value);

            return (A, B);
        }


        public double ChanceToReachState(double[] StrategyA, double[] StrategyB, (Func<double, double>[] pathA, Func<double, double>[] pathB) path)
        {
            return StrategyA.Zip(path.pathA, (s, f) => f(s) ).Aggregate((double x, double y) => x * y)
                * StrategyB.Zip(path.pathB, (s, f) => f(s)).Aggregate((double x, double y) => x * y);
        }

        public static readonly Func<double, double> Yes = (d => d);
        public static readonly Func<double, double> No  = (d => 1 - d);
        public static readonly Func<double, double> __   = (_ => 1.0);

        public double ShowdownValue(int handA, int handB, int ante, double calledAmount)
        {
            if (handA < handB)
            {
                return ante + calledAmount;
            } else
            {
                return -calledAmount;
            }
        }


        public double Evaluate(Strategy A, Strategy B) {

            // Odds of reaching each outcome, for each player
            (Func<double, double>[] pathA, Func<double, double>[] pathB) S0 = (new Func<double, double>[] { No, __, __, __ },    new Func<double, double>[] { No, __, __, __ });
            (Func<double, double>[] pathA, Func<double, double>[] pathB) S1 = (new Func<double, double>[] { No, No, __, __ },   new Func<double, double>[] { Yes, __, __, __ });
            (Func<double, double>[] pathA, Func<double, double>[] pathB) S2 = (new Func<double, double>[] { No, Yes, No, __ },    new Func<double, double>[] { Yes, __, __, __ });
            (Func<double, double>[] pathA, Func<double, double>[] pathB) S3 = (new Func<double, double>[] { No, Yes, Yes, __ },new Func<double, double>[] { Yes, __, __, No });
            (Func<double, double>[] pathA, Func<double, double>[] pathB) S4 = (new Func<double, double>[] { No, Yes, Yes, __ },new Func<double, double>[] { Yes, __, __, Yes });
            (Func<double, double>[] pathA, Func<double, double>[] pathB) S5 = (new Func<double, double>[] { Yes, __, __, __ },   new Func<double, double>[] { __, No, __, __ });
            (Func<double, double>[] pathA, Func<double, double>[] pathB) S6 = (new Func<double, double>[] { Yes, __, __, __ }, new Func<double, double>[] { __, Yes, No, __ });
            (Func<double, double>[] pathA, Func<double, double>[] pathB) S7 = (new Func<double, double>[] { Yes, __, __, No }, new Func<double, double>[] { __, Yes, Yes, __ });
            (Func<double, double>[] pathA, Func<double, double>[] pathB) S8 = (new Func<double, double>[] { Yes, __, __, Yes }, new Func<double, double>[] { __, Yes, Yes, __ });

            var paths = new[] { S0, S1, S2, S3, S4, S5, S6, S7, S8};

            double totalValue = 0.0;
            for (int handA = 0; handA < A.Table.Length; handA++)
            {
                for (int handB = 0; handB < B.Table.Length; handB++)
                {
                    if (handA == handB) continue;

                    //Console.WriteLine($"---I have:{handA}, Opponent has:{handB}");

                    //Value of each state, for player A
                    var values = new[]
                    {
                        ShowdownValue(handA, handB, Constants.Ante, 0),// X, X
                        0,                             // X, B, F
                        ShowdownValue(handA, handB, Constants.Ante, Constants.Bet),// X, B, C
                        Constants.Ante + Constants.Bet,                             // X, B, R, F
                        ShowdownValue(handA, handB, Constants.Ante, Constants.Raise),// X, B, R, C
                        Constants.Ante,                             // B, F
                        ShowdownValue(handA, handB, Constants.Ante, Constants.Bet),// B, C
                        -Constants.Bet,                            // B, R, F
                        ShowdownValue(handA, handB, Constants.Ante, Constants.Raise) // B, R, C
                    };

                    for(int si = 0; si < paths.Length; si++)
                    {
                        var chanceToReachSi = ChanceToReachState(A.Table[handA], B.Table[handB], paths[si]);
                        var valueOfSi = values[si];
                        //Console.WriteLine($"  State {i}: Chance to reach={chanceToReachSi}, Value={valueOfSi}");
                        totalValue += chanceToReachSi * valueOfSi;

                    }


                }
            }

            return totalValue / (A.Table.Length * (A.Table.Length - 1));
        }


    }
}
