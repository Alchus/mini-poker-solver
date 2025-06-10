import numpy as np
import pandas as pd

def parse_strategy_file(filename):
    # Initialize a dictionary to store probabilities for each card
    strategies = {}
    
    with open(filename, 'r') as f:
        lines = f.readlines()
    
    # Process only player 1's section
    player_1_lines = []
    in_player_1 = False
    for line in lines:
        if line.strip() == "PLAYER: 1":
            in_player_1 = True
            continue
        if line.strip() == "END":
            in_player_1 = False
            continue
        if in_player_1 and line.strip():
            player_1_lines.append(line.strip())
    
    # Process each card's strategies
    for line in player_1_lines:
        if not line.startswith("[Player: 1"):
            continue
            
        # Parse the line
        info, probs = line.split("\t")
        probs = [float(p) for p in probs.split()]
        
        # Extract card and betting history
        card = info.split("Card: ")[1].split(",")[0]
        history = info.split("History: ")[1].split(" Actions:")[0].strip()
        actions = info.split("Actions: ")[1].split("]")[0].split(", ")
        
        # Store in dictionary
        if card not in strategies:
            strategies[card] = {}
        
        strategies[card][f"{history}|{','.join(actions)}"] = probs
    
    # Create the result tables
    # Define card order (A, K, Q, J, T, 9, 8, 7, 6, 5, 4, 3, 2)
    card_order = "AKQJT98765432"
    cards = sorted(strategies.keys(), key=lambda x: card_order.index(x))
    
    # Table 1: What to do if checked to
    result_checked_to = np.zeros((3, len(cards)))
    
    # Table 2: What to do if bet into
    result_bet_into = np.zeros((3, len(cards)))
    
    for i, card in enumerate(cards):
        card_data = strategies[card]
        
        # If checked to probabilities
        checked_to_probs = card_data.get("CHECK|BET,CHECK", [0, 1])
        bet_prob = checked_to_probs[0]  # Probability of betting
        check_prob = checked_to_probs[1]  # Probability of checking
        
        # After betting, if raised
        raise_response_probs = card_data.get("CHECK, BET, RAISE|CALLRAISE,FOLD", [0, 1])
        if raise_response_probs:
            callraise_prob = raise_response_probs[0]  # Probability of calling raise
            fold_to_raise_prob = raise_response_probs[1]  # Probability of folding to raise
        else:
            callraise_prob = 0
            fold_to_raise_prob = 1
            
        # Calculate compound probabilities
        result_checked_to[0, i] = bet_prob * callraise_prob  # BET+CALLRAISE
        result_checked_to[1, i] = bet_prob * fold_to_raise_prob  # BET+FOLD
        result_checked_to[2, i] = check_prob  # CHECK
        
        # If bet into probabilities (CALLBET vs FOLD vs RAISE)
        bet_into_probs = card_data.get("BET|CALLBET,FOLD,RAISE", [0, 0, 0])
        result_bet_into[0, i] = bet_into_probs[0]  # CALLBET
        result_bet_into[1, i] = bet_into_probs[1]  # FOLD
        result_bet_into[2, i] = bet_into_probs[2]  # RAISE

    # Create DataFrames with strategies in the desired order
    df_checked_to = pd.DataFrame(
        result_checked_to,
        columns=cards,
        index=['BET+CALLRAISE', 'BET+FOLD', 'CHECK']
    )
    
    df_bet_into = pd.DataFrame(
        result_bet_into,
        columns=cards,
        index=['CALLBET', 'FOLD', 'RAISE']
    )
    
    return df_checked_to, df_bet_into

def print_table(df, title):
    print(f"\n{title}")
    print("-" * 80)
    
    # Print header
    print("            ", end="")
    for card in df.columns:
        print(f"   {card:3}", end="")
    print()
    
    # Print each strategy row
    for strategy in df.index:
        print(f"{strategy:<13}", end="")
        for val in df.loc[strategy]:
            print(f" {val:5.2f}", end="")
        print()

if __name__ == "__main__":
    df_checked_to, df_bet_into = parse_strategy_file("crm/CFRMiniPoker/kuhn_poker13_strategy.txt")
    
    print_table(df_checked_to, "Player 1's Strategy When Checked To")
    print_table(df_bet_into, "Player 1's Strategy When Bet Into") 