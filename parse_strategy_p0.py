import numpy as np
import pandas as pd

def parse_strategy_file(filename):
    # Initialize a dictionary to store probabilities for each card
    strategies = {}
    
    with open(filename, 'r') as f:
        lines = f.readlines()
    
    # Process only player 0's section
    player_0_lines = []
    in_player_0 = False
    for line in lines:
        if line.strip() == "PLAYER: 0":
            in_player_0 = True
            continue
        if line.strip() == "END":
            in_player_0 = False
            continue
        if in_player_0 and line.strip():
            player_0_lines.append(line.strip())
    
    # Process each card's strategies
    for line in player_0_lines:
        if not line.startswith("[Player: 0"):
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
        
        strategies[card][f"{history}|{actions[0]},{actions[1]}" if len(actions) == 2 else f"{history}|{actions[0]},{actions[1]},{actions[2]}"] = probs
    
    # Create the result table
    # Define card order (A, K, Q, J, T, 9, 8, 7, 6, 5, 4, 3, 2)
    card_order = "AKQJT98765432"
    cards = sorted(strategies.keys(), key=lambda x: card_order.index(x))
    result = np.zeros((5, len(cards)))
    
    for i, card in enumerate(cards):
        card_data = strategies[card]
        
        # Initial check probability
        check_prob = card_data.get("|BET,CHECK", [0, 1])[1]
        
        # Initial bet probability
        bet_prob = card_data.get("|BET,CHECK", [1, 0])[0]
        
        # After check probabilities
        check_actions = card_data.get("CHECK, BET|CALLBET,FOLD,RAISE", [0, 0, 0])
        
        # After bet probabilities
        bet_actions = card_data.get("BET, RAISE|CALLRAISE,FOLD", [0, 0])
        
        # Calculate compound probabilities
        result[0, i] = check_prob * check_actions[1]  # CHECK+FOLD
        result[1, i] = check_prob * check_actions[0]  # CHECK+CALLBET
        result[2, i] = check_prob * check_actions[2]  # CHECK+RAISE
        result[3, i] = bet_prob * bet_actions[1]      # BET+FOLD
        result[4, i] = bet_prob * bet_actions[0]      # BET+CALLRAISE

    # Create DataFrame with strategies in the desired order
    strategies = ['CHECK+FOLD', 'CHECK+CALLBET', 'CHECK+RAISE', 'BET+FOLD', 'BET+CALLRAISE']
    df = pd.DataFrame(
        result,
        columns=cards,
        index=strategies
    )
    
    return df

if __name__ == "__main__":
    strategy_table = parse_strategy_file("crm/CFRMiniPoker/kuhn_poker13_strategy.txt")
    
    # Print header
    print("            ", end="")
    for card in strategy_table.columns:
        print(f"   {card:3}", end="")
    print()
    
    # Print each strategy row in order
    for strategy in ['CHECK+FOLD', 'CHECK+CALLBET', 'CHECK+RAISE', 'BET+FOLD', 'BET+CALLRAISE']:
        print(f"{strategy:<13}", end="")
        for val in strategy_table.loc[strategy]:
            print(f" {val:5.2f}", end="")
        print() 