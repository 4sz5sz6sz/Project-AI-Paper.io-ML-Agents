"""
Paper.io ML-Agents Python Environment
Replicates the Unity Paper.io game environment for Google Colab training.
"""

import numpy as np
import gym
from gym import spaces
from typing import List, Tuple, Dict, Optional
import random
from collections import deque
from dataclasses import dataclass
from enum import IntEnum

class Direction(IntEnum):
    UP = 0
    RIGHT = 1
    DOWN = 2
    LEFT = 3

@dataclass
class PlayerState:
    """Player state information"""
    position: Tuple[int, int]
    direction: Direction
    trail: List[Tuple[int, int]]
    is_alive: bool
    is_in_safe_zone: bool
    player_id: int

class PaperIOEnv(gym.Env):
    """
    Paper.io environment that replicates Unity ML-Agents MyAgent.cs observation and reward system.
    
    Map size: 100x100
    Players: 4
    Observation space: 84 dimensions (matches MyAgent.cs exactly)
    Action space: 4 discrete actions (up, right, down, left)
    """
    
    def __init__(self, render_mode=None):
        super().__init__()
        
        # Environment parameters (matching Unity implementation)
        self.MAP_SIZE = 100
        self.NUM_PLAYERS = 4
        self.MAX_STEPS = 2000
        
        # Action space: 4 directions
        self.action_space = spaces.Discrete(4)
        
        # Observation space: 84 dimensions (matching MyAgent.cs CollectObservations)
        # 1. Ultra Critical 3x3 (45 dims = 9 * 5 repetitions)
        # 2. Critical Proximity (9 dims)
        # 3. Immediate Danger (10 dims)
        # 4. Enemy Threat Assessment (15 dims)
        # 5. Basic Info (5 dims)
        self.observation_space = spaces.Box(
            low=-100.0, high=100.0, shape=(84,), dtype=np.float32
        )
        
        # Game state
        self.reset()
        
    def reset(self, seed=None, options=None):
        """Reset the environment to initial state"""
        super().reset(seed=seed)
        if seed is not None:
            np.random.seed(seed)
            random.seed(seed)
            
        # Initialize map grids
        self.tile_map = np.zeros((self.MAP_SIZE, self.MAP_SIZE), dtype=np.int32)  # Tile ownership
        self.trail_map = np.zeros((self.MAP_SIZE, self.MAP_SIZE), dtype=np.int32)  # Trail ownership
        
        # Initialize players
        self.players = {}
        self._initialize_players()
        
        # Game state
        self.step_count = 0
        self.scores = {i: 0 for i in range(1, self.NUM_PLAYERS + 1)}
        
        # Initialize starting territories
        self._initialize_territories()
        
        return self._get_observations(), {}
    
    def _initialize_players(self):
        """Initialize player positions and states"""
        # Starting positions (matching Unity spawn points)
        start_positions = [
            (5, 5),    # Player 1
            (95, 95),  # Player 2  
            (5, 95),   # Player 3
            (95, 5)    # Player 4
        ]
        
        for i in range(self.NUM_PLAYERS):
            player_id = i + 1
            pos = start_positions[i]
            self.players[player_id] = PlayerState(
                position=pos,
                direction=Direction.UP,
                trail=[],
                is_alive=True,
                is_in_safe_zone=True,
                player_id=player_id
            )
    
    def _initialize_territories(self):
        """Initialize starting territories for each player"""
        territory_size = 3
        for player_id, player in self.players.items():
            x, y = player.position
            # Create 3x3 starting territory
            for dx in range(-territory_size//2, territory_size//2 + 1):
                for dy in range(-territory_size//2, territory_size//2 + 1):
                    nx, ny = x + dx, y + dy
                    if self._in_bounds(nx, ny):
                        self.tile_map[ny, nx] = player_id
            self._update_score(player_id)
    
    def _in_bounds(self, x: int, y: int) -> bool:
        """Check if position is within map bounds"""
        return 0 <= x < self.MAP_SIZE and 0 <= y < self.MAP_SIZE
    
    def _get_tile(self, x: int, y: int) -> int:
        """Get tile owner at position"""
        if not self._in_bounds(x, y):
            return -1  # Out of bounds
        return self.tile_map[y, x]
    
    def _get_trail(self, x: int, y: int) -> int:
        """Get trail owner at position"""
        if not self._in_bounds(x, y):
            return -1  # Out of bounds
        return self.trail_map[y, x]
    
    def _update_score(self, player_id: int):
        """Update player score based on owned tiles"""
        self.scores[player_id] = np.sum(self.tile_map == player_id)
    
    def step(self, actions):
        """
        Execute one environment step with actions for all players
        actions: dict or list of actions for each player
        """
        if isinstance(actions, (list, tuple)):
            # Convert list to dict
            action_dict = {i+1: actions[i] for i in range(min(len(actions), self.NUM_PLAYERS))}
        else:
            action_dict = actions
            
        # Store previous positions and states for reward calculation
        prev_states = {pid: (p.position, p.is_in_safe_zone, len(p.trail)) 
                      for pid, p in self.players.items()}
        prev_scores = self.scores.copy()
        
        rewards = {}
        dones = {}
        infos = {}
        
        # Execute actions for all players
        for player_id in range(1, self.NUM_PLAYERS + 1):
            if player_id in action_dict and self.players[player_id].is_alive:
                reward, done, info = self._execute_player_action(player_id, action_dict[player_id], prev_states)
                rewards[player_id] = reward
                dones[player_id] = done
                infos[player_id] = info
            else:
                rewards[player_id] = 0.0
                dones[player_id] = not self.players[player_id].is_alive
                infos[player_id] = {}
        
        self.step_count += 1
        
        # Check if episode should end
        alive_players = sum(1 for p in self.players.values() if p.is_alive)
        episode_done = (alive_players <= 1 or self.step_count >= self.MAX_STEPS)
        
        for player_id in rewards:
            dones[player_id] = dones[player_id] or episode_done
        
        observations = self._get_observations()
        
        return observations, rewards, dones, infos
    
    def _execute_player_action(self, player_id: int, action: int, prev_states: dict) -> Tuple[float, bool, dict]:
        """Execute action for a specific player and calculate rewards"""
        player = self.players[player_id]
        if not player.is_alive:
            return 0.0, True, {}
        
        # Convert action to direction
        directions = [
            (0, 1),   # UP
            (1, 0),   # RIGHT
            (0, -1),  # DOWN
            (-1, 0)   # LEFT
        ]
        
        if action < 0 or action >= len(directions):
            return -1.0, False, {"invalid_action": True}
        
        dx, dy = directions[action]
        current_x, current_y = player.position
        next_x, next_y = current_x + dx, current_y + dy
        
        reward = 0.0
        done = False
        info = {}
        
        # Check for collisions and calculate rewards
        
        # 1. Wall collision check
        if not self._in_bounds(next_x, next_y):
            reward += -80.0  # Wall collision penalty (matches MyAgent.cs)
            self._kill_player(player_id, death_type=1)
            done = True
            info["death_reason"] = "wall_collision"
            return reward, done, info
        
        # 2. Self trail collision check
        if self._get_trail(next_x, next_y) == player_id:
            reward += -100.0  # Self trail collision penalty (matches MyAgent.cs)
            self._kill_player(player_id, death_type=2)
            done = True
            info["death_reason"] = "self_trail_collision"
            return reward, done, info
        
        # 3. Enemy trail collision check
        trail_owner = self._get_trail(next_x, next_y)
        if trail_owner > 0 and trail_owner != player_id:
            reward += -15.0  # Enemy trail collision penalty (matches MyAgent.cs)
            self._kill_player(player_id, death_type=3)
            done = True
            info["death_reason"] = "enemy_trail_collision"
            return reward, done, info
        
        # Move player
        player.position = (next_x, next_y)
        player.direction = Direction(action)
        
        # Check if entering/leaving safe zone
        tile_owner = self._get_tile(next_x, next_y)
        was_in_safe_zone = player.is_in_safe_zone
        player.is_in_safe_zone = (tile_owner == player_id)
        
        # Trail management
        if not player.is_in_safe_zone:
            # Add current position to trail
            player.trail.append((next_x, next_y))
            self.trail_map[next_y, next_x] = player_id
            
            # Safe zone penalty (matches MyAgent.cs)
            reward += -0.1
        else:
            # Entering safe zone - complete trail loop if exists
            if len(player.trail) > 0 and not was_in_safe_zone:
                tiles_gained = self._complete_trail_loop(player_id)
                if tiles_gained > 0:
                    reward += 0.1 * tiles_gained  # Territory expansion reward (matches MyAgent.cs)
                    info["territory_gained"] = tiles_gained
        
        # Additional reward calculations (simplified versions of MyAgent.cs rewards)
        prev_score = self.scores[player_id]
        self._update_score(player_id)
        score_delta = self.scores[player_id] - prev_score
        
        if score_delta > 0:
            reward += 0.1 * score_delta  # Territory gain reward
        
        return reward, done, info
    
    def _kill_player(self, player_id: int, death_type: int = 0):
        """Kill a player and clean up their trail"""
        self.players[player_id].is_alive = False
        
        # Clear player's trail
        trail_positions = list(self.players[player_id].trail)
        for x, y in trail_positions:
            if self._in_bounds(x, y):
                self.trail_map[y, x] = 0
        
        self.players[player_id].trail.clear()
    
    def _complete_trail_loop(self, player_id: int) -> int:
        """Complete a trail loop and expand territory"""
        player = self.players[player_id]
        if len(player.trail) < 3:  # Need at least 3 points for a loop
            return 0
        
        # Simplified territory expansion - flood fill from trail
        trail_points = set(player.trail)
        
        # Clear trail from map
        for x, y in player.trail:
            if self._in_bounds(x, y):
                self.trail_map[y, x] = 0
        
        # Simple flood fill to expand territory
        tiles_gained = self._flood_fill_territory(player_id, trail_points)
        
        # Clear player trail
        player.trail.clear()
        
        return tiles_gained
    
    def _flood_fill_territory(self, player_id: int, trail_points: set) -> int:
        """Simplified flood fill for territory expansion"""
        # This is a simplified version - in the full implementation you'd want
        # proper polygon filling logic like in the Unity MapManager
        
        tiles_gained = 0
        
        # Find bounding box of trail
        if not trail_points:
            return 0
            
        min_x = min(x for x, y in trail_points)
        max_x = max(x for x, y in trail_points)
        min_y = min(y for x, y in trail_points)
        max_y = max(y for x, y in trail_points)
        
        # Simple rectangular fill (simplified approach)
        for x in range(max(0, min_x), min(self.MAP_SIZE, max_x + 1)):
            for y in range(max(0, min_y), min(self.MAP_SIZE, max_y + 1)):
                if self.tile_map[y, x] == 0:  # Neutral territory
                    self.tile_map[y, x] = player_id
                    tiles_gained += 1
        
        return tiles_gained
    
    def _get_observations(self) -> Dict[int, np.ndarray]:
        """Get observations for all players (84 dimensions each, matching MyAgent.cs)"""
        observations = {}
        
        for player_id, player in self.players.items():
            if player.is_alive:
                obs = self._get_player_observation(player_id)
                observations[player_id] = obs
            else:
                # Dead players get zero observation
                observations[player_id] = np.zeros(84, dtype=np.float32)
        
        return observations
    
    def _get_player_observation(self, player_id: int) -> np.ndarray:
        """
        Get observation for a specific player (84 dimensions total)
        Replicates MyAgent.cs CollectObservations exactly:
        1. Ultra Critical 3x3 (45 dims = 9 * 5 repetitions)
        2. Critical Proximity (9 dims)
        3. Immediate Danger (10 dims)
        4. Enemy Threat Assessment (15 dims)
        5. Basic Info (5 dims)
        """
        player = self.players[player_id]
        x, y = player.position
        
        obs = []
        
        # 1. Ultra Critical 3x3 observations (45 dims = 9 * 5 repetitions)
        for _ in range(5):  # Repeat 5 times as in MyAgent.cs
            ultra_critical = self._add_ultra_critical_3x3_observations(x, y, player_id)
            obs.extend(ultra_critical)
        
        # 2. Critical Proximity observations (9 dims)
        critical_proximity = self._add_critical_proximity_observations(x, y, player_id)
        obs.extend(critical_proximity)
        
        # 3. Immediate Danger observations (10 dims)
        immediate_danger = self._add_immediate_danger_observations(x, y, player_id)
        obs.extend(immediate_danger)
        
        # 4. Enemy Threat Assessment (15 dims)
        enemy_threat = self._add_enemy_threat_assessment(x, y, player_id)
        obs.extend(enemy_threat)
        
        # 5. Basic Info (5 dims)
        basic_info = [
            np.clip(x / 100.0, 0, 1),  # X position normalized
            np.clip(y / 100.0, 0, 1),  # Y position normalized
            1.0 if player.direction == Direction.RIGHT else -1.0 if player.direction == Direction.LEFT else 0.0,  # Direction X
            1.0 if player.direction == Direction.UP else -1.0 if player.direction == Direction.DOWN else 0.0,     # Direction Y
            self.scores[player_id] / 10000.0  # Current score normalized
        ]
        obs.extend(basic_info)
        
        return np.array(obs, dtype=np.float32)
    
    def _add_ultra_critical_3x3_observations(self, my_x: int, my_y: int, my_player_id: int) -> List[float]:
        """
        Ultra Critical 3x3 observations (9 dimensions)
        Replicates AddUltraCritical3x3Observations from MyAgent.cs
        """
        # 3x3 area positions (center to clockwise)
        positions = [
            (0, 0),   # Center (current position)
            (0, 1),   # Up
            (1, 1),   # Up-right
            (1, 0),   # Right
            (1, -1),  # Down-right
            (0, -1),  # Down
            (-1, -1), # Down-left
            (-1, 0),  # Left
            (-1, 1)   # Up-left
        ]
        
        observations = []
        
        for dx, dy in positions:
            check_x, check_y = my_x + dx, my_y + dy
            
            if not self._in_bounds(check_x, check_y):
                # Out of bounds = extreme danger
                ultra_critical_value = -100.0
            else:
                ultra_critical_value = 0.0
                
                # Tile ownership analysis
                tile_owner = self._get_tile(check_x, check_y)
                if tile_owner == my_player_id:
                    ultra_critical_value += 50.0  # My territory (safe)
                elif tile_owner == 0:
                    ultra_critical_value += 10.0  # Neutral (expandable)
                else:
                    ultra_critical_value -= 10.0  # Enemy territory
                
                # Trail analysis (critical for survival)
                trail_owner = self._get_trail(check_x, check_y)
                if trail_owner == my_player_id:
                    ultra_critical_value -= 200.0  # My trail (deadly!)
                elif trail_owner > 0:
                    ultra_critical_value -= 50.0   # Enemy trail (dangerous)
                
                # Enemy proximity analysis
                enemy_distance = self._get_nearest_enemy_distance(check_x, check_y, my_player_id)
                if enemy_distance <= 2:
                    ultra_critical_value -= 30.0 * (3 - enemy_distance)
            
            observations.append(ultra_critical_value)
        
        return observations
    
    def _add_critical_proximity_observations(self, my_x: int, my_y: int, my_player_id: int) -> List[float]:
        """
        Critical Proximity observations (9 dimensions)
        Replicates AddCriticalProximityObservations from MyAgent.cs
        """
        positions = [
            (0, 0), (0, 1), (1, 1), (1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (-1, 1)
        ]
        
        observations = []
        
        for dx, dy in positions:
            check_x, check_y = my_x + dx, my_y + dy
            
            proximity_value = 0.0
            
            if not self._in_bounds(check_x, check_y):
                proximity_value = -50.0  # Wall danger
            else:
                # Safety analysis based on tile ownership
                tile_owner = self._get_tile(check_x, check_y)
                if tile_owner == my_player_id:
                    proximity_value += 30.0  # Safe zone
                elif tile_owner == 0:
                    proximity_value += 5.0   # Neutral
                else:
                    proximity_value -= 5.0   # Enemy territory
                
                # Trail danger analysis
                trail_owner = self._get_trail(check_x, check_y)
                if trail_owner == my_player_id:
                    proximity_value -= 100.0  # Lethal
                elif trail_owner > 0:
                    proximity_value -= 20.0   # Dangerous
            
            observations.append(proximity_value)
        
        return observations
    
    def _add_immediate_danger_observations(self, my_x: int, my_y: int, my_player_id: int) -> List[float]:
        """
        Immediate Danger observations (10 dimensions)
        Replicates AddImmediateDangerObservations from MyAgent.cs
        """
        observations = []
        
        # Check 4 cardinal directions for immediate threats
        directions = [(0, 1), (1, 0), (0, -1), (-1, 0)]  # Up, Right, Down, Left
        
        for dx, dy in directions:
            check_x, check_y = my_x + dx, my_y + dy
            
            danger_level = 0.0
            
            if not self._in_bounds(check_x, check_y):
                danger_level = 100.0  # Wall = immediate death threat
            else:
                trail_owner = self._get_trail(check_x, check_y)
                if trail_owner == my_player_id:
                    danger_level = 200.0  # Self trail = death
                elif trail_owner > 0:
                    danger_level = 50.0   # Enemy trail = dangerous
                
                # Check for enemies nearby
                enemy_distance = self._get_nearest_enemy_distance(check_x, check_y, my_player_id)
                if enemy_distance <= 1:
                    danger_level += 75.0  # Very close enemy
                elif enemy_distance <= 2:
                    danger_level += 25.0  # Close enemy
            
            observations.append(danger_level)
        
        # Add 6 more dimensions for extended danger analysis
        # Distance to safe zone
        safe_distance = self._calculate_distance_to_safe_zone(my_x, my_y, my_player_id)
        observations.append(min(safe_distance / 10.0, 10.0))  # Normalized safe distance
        
        # Trail length (longer trail = more risk)
        trail_length = len(self.players[my_player_id].trail)
        observations.append(min(trail_length / 20.0, 5.0))  # Normalized trail length
        
        # Number of nearby enemies
        nearby_enemies = self._count_nearby_enemies(my_x, my_y, my_player_id, radius=5)
        observations.append(nearby_enemies * 10.0)
        
        # Escape route availability
        escape_routes = self._count_escape_routes(my_x, my_y, my_player_id)
        observations.append(escape_routes * 25.0)
        
        # Territory pressure (how much territory is contested)
        territory_pressure = self._calculate_territory_pressure(my_x, my_y, my_player_id)
        observations.append(territory_pressure)
        
        # Current advantage/disadvantage score
        score_ratio = self.scores[my_player_id] / max(1, max(self.scores.values()))
        observations.append(score_ratio * 50.0 - 25.0)
        
        return observations
    
    def _add_enemy_threat_assessment(self, my_x: int, my_y: int, my_player_id: int) -> List[float]:
        """
        Enemy Threat Assessment (15 dimensions)
        Replicates AddEnemyThreatAssessment from MyAgent.cs
        """
        observations = []
        
        # Analyze each enemy player (3 enemies = 5 dims each = 15 total)
        enemy_ids = [pid for pid in self.players.keys() if pid != my_player_id]
        
        for i in range(3):  # Always 3 enemies in 4-player game
            if i < len(enemy_ids) and self.players[enemy_ids[i]].is_alive:
                enemy_id = enemy_ids[i]
                enemy = self.players[enemy_id]
                ex, ey = enemy.position
                
                # 1. Distance to enemy
                distance = abs(ex - my_x) + abs(ey - my_y)  # Manhattan distance
                observations.append(min(distance / 20.0, 5.0))  # Normalized distance
                
                # 2. Enemy trail threat level
                enemy_trail_threat = self._calculate_trail_threat(my_x, my_y, enemy_id)
                observations.append(enemy_trail_threat)
                
                # 3. Enemy territory advantage
                enemy_territory = self.scores[enemy_id]
                my_territory = self.scores[my_player_id]
                territory_ratio = enemy_territory / max(1, my_territory)
                observations.append(min(territory_ratio, 3.0))
                
                # 4. Enemy aggression level (based on trail activity)
                enemy_trail_length = len(enemy.trail)
                aggression = min(enemy_trail_length / 10.0, 2.0)
                observations.append(aggression)
                
                # 5. Threat urgency (combination of distance and capability)
                urgency = max(0, (10 - distance) * territory_ratio / 10.0)
                observations.append(urgency)
                
            else:
                # Dead or non-existent enemy
                observations.extend([0.0, 0.0, 0.0, 0.0, 0.0])
        
        return observations
    
    def _get_nearest_enemy_distance(self, x: int, y: int, my_player_id: int) -> float:
        """Get distance to nearest enemy"""
        min_distance = float('inf')
        
        for pid, player in self.players.items():
            if pid != my_player_id and player.is_alive:
                ex, ey = player.position
                distance = abs(ex - x) + abs(ey - y)
                min_distance = min(min_distance, distance)
        
        return min_distance if min_distance != float('inf') else 100.0
    
    def _calculate_distance_to_safe_zone(self, x: int, y: int, player_id: int) -> int:
        """Calculate shortest distance to player's safe zone"""
        # Simple BFS to find nearest owned tile
        if self._get_tile(x, y) == player_id:
            return 0
        
        queue = deque([(x, y, 0)])
        visited = set([(x, y)])
        
        while queue:
            cx, cy, dist = queue.popleft()
            
            if dist > 20:  # Limit search
                break
                
            for dx, dy in [(0, 1), (1, 0), (0, -1), (-1, 0)]:
                nx, ny = cx + dx, cy + dy
                
                if (nx, ny) not in visited and self._in_bounds(nx, ny):
                    visited.add((nx, ny))
                    
                    if self._get_tile(nx, ny) == player_id:
                        return dist + 1
                    
                    queue.append((nx, ny, dist + 1))
        
        return 20  # Max distance if not found
    
    def _count_nearby_enemies(self, x: int, y: int, my_player_id: int, radius: int = 5) -> int:
        """Count enemies within radius"""
        count = 0
        for pid, player in self.players.items():
            if pid != my_player_id and player.is_alive:
                ex, ey = player.position
                if abs(ex - x) + abs(ey - y) <= radius:
                    count += 1
        return count
    
    def _count_escape_routes(self, x: int, y: int, my_player_id: int) -> int:
        """Count available escape routes"""
        escape_routes = 0
        
        for dx, dy in [(0, 1), (1, 0), (0, -1), (-1, 0)]:
            nx, ny = x + dx, y + dy
            
            if (self._in_bounds(nx, ny) and 
                self._get_trail(nx, ny) != my_player_id and
                self._get_trail(nx, ny) == 0):  # Not occupied by any trail
                escape_routes += 1
        
        return escape_routes
    
    def _calculate_territory_pressure(self, x: int, y: int, my_player_id: int) -> float:
        """Calculate how much territory pressure exists around position"""
        pressure = 0.0
        
        # Check surrounding area for enemy presence
        for radius in range(1, 4):
            for dx in range(-radius, radius + 1):
                for dy in range(-radius, radius + 1):
                    if dx == 0 and dy == 0:
                        continue
                        
                    nx, ny = x + dx, y + dy
                    if self._in_bounds(nx, ny):
                        tile_owner = self._get_tile(nx, ny)
                        if tile_owner > 0 and tile_owner != my_player_id:
                            pressure += 1.0 / (radius * radius)
        
        return min(pressure, 10.0)
    
    def _calculate_trail_threat(self, my_x: int, my_y: int, enemy_id: int) -> float:
        """Calculate threat level from enemy's trail"""
        enemy = self.players[enemy_id]
        
        if not enemy.trail:
            return 0.0
        
        # Check if enemy trail is blocking escape routes
        threat_level = 0.0
        
        for tx, ty in enemy.trail:
            distance = abs(tx - my_x) + abs(ty - my_y)
            if distance <= 3:
                threat_level += (4 - distance) * 10.0
        
        return min(threat_level, 50.0)
    
    def get_action_mask(self, player_id: int) -> np.ndarray:
        """
        Get action mask for player (prevents invalid moves like 180-degree turns)
        Replicates WriteDiscreteActionMask from MyAgent.cs
        """
        mask = np.ones(4, dtype=bool)  # All actions valid by default
        
        if player_id not in self.players or not self.players[player_id].is_alive:
            return mask
        
        current_direction = self.players[player_id].direction
        
        # Prevent 180-degree turns (opposite direction)
        opposite_actions = {
            Direction.UP: Direction.DOWN,
            Direction.RIGHT: Direction.LEFT, 
            Direction.DOWN: Direction.UP,
            Direction.LEFT: Direction.RIGHT
        }
        
        if current_direction in opposite_actions:
            opposite = opposite_actions[current_direction]
            mask[opposite] = False  # Block opposite direction
        
        return mask
    
    def render(self, mode='human'):
        """Render the game state (optional)"""
        if mode == 'human':
            # Simple text representation
            print(f"Step: {self.step_count}")
            print(f"Scores: {self.scores}")
            for pid, player in self.players.items():
                if player.is_alive:
                    print(f"Player {pid}: {player.position}, Trail: {len(player.trail)}")
        
        return None
    
    def close(self):
        """Clean up resources"""
        pass