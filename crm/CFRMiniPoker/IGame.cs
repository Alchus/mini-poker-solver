using System;
using System.Collections.Generic;

/// <summary>
/// Interface for abstract strategy games.
/// </summary>
/// <typeparam name="T">The type of actions</typeparam>
public interface IGame<T>
{
    /// <summary>
    /// Starts a new game
    /// </summary>
    void BeginGame();

    /// <summary>
    /// The player to act makes the given move
    /// </summary>
    /// <param name="move">The move to make</param>
    void MakeMove(T move);

    /// <summary>
    /// Computes the payouts for a game. Can only be called if the game has finished.
    /// </summary>
    /// <returns>A list whose i-th value is the payout to the i-th player</returns>
    IReadOnlyList<double> Payout();

    /// <summary>
    /// Returns a deep copy of this object
    /// </summary>
    /// <returns>A deep copy</returns>
    IGame<T> DeepCopy();

    /// <summary>
    /// Returns a unique id of current player's information set.
    /// An information set consists of all the information that player knows at a given point.
    /// Every action node in the game tree corresponds to exactly one information set.
    /// 
    /// The unique id is a string representation of the information set. The string
    /// representations must be in one-to-one correspondence with the information sets.
    /// 
    /// The unique id must not contain any '\n' or '\t' characters
    /// </summary>
    /// <returns>The current player's information set</returns>
    string InformationSet();

    /// <summary>
    /// Checks if the game has ended
    /// </summary>
    /// <returns>true if the game has ended</returns>
    bool IsTerminalState();

    /// <summary>
    /// Returns the number of players in the game.
    /// </summary>
    /// <returns>The number of players in the game</returns>
    int NumPlayers();

    /// <summary>
    /// Determines which player's turn it is. Players are numbered using zero based indexing.
    /// </summary>
    /// <returns>The player whose turn it is</returns>
    int PlayerToAct();

    /// <summary>
    /// Computes the possible actions that the current player can make.
    /// The order of the actions returned must be deterministic.
    /// </summary>
    /// <returns>A list of possible actions that the player can make</returns>
    IReadOnlyList<T> Actions();
}