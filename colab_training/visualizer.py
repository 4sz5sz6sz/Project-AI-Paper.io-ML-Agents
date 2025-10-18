"""
Paper.io Game State Visualizer
Real-time visualization and analysis tools for the Paper.io environment
"""

import numpy as np
import matplotlib.pyplot as plt
import matplotlib.patches as patches
from matplotlib.animation import FuncAnimation
import seaborn as sns
from typing import Dict, List, Tuple, Optional
import io
import base64
import time

# Optional IPython imports for Colab support
try:
    from IPython.display import HTML, display, clear_output
    IPYTHON_AVAILABLE = True
except ImportError:
    IPYTHON_AVAILABLE = False
    # Mock functions for non-Colab environments
    def display(*args, **kwargs):
        pass
    def clear_output(*args, **kwargs):
        pass

from paper_io_env import PaperIOEnv, PlayerState

class PaperIOVisualizer:
    """
    Visualization tools for Paper.io environment
    """
    
    def __init__(self, env: PaperIOEnv):
        self.env = env
        self.player_colors = {
            0: '#FFFFFF',  # Neutral - white
            1: '#FF6B6B',  # Player 1 - red
            2: '#4ECDC4',  # Player 2 - teal
            3: '#45B7D1',  # Player 3 - blue
            4: '#96CEB4'   # Player 4 - green
        }
        self.trail_colors = {
            0: '#FFFFFF',  # No trail - white
            1: '#FF9999',  # Player 1 trail - light red
            2: '#7FDDDD',  # Player 2 trail - light teal
            3: '#7AC8E8',  # Player 3 trail - light blue
            4: '#B8E6C1'   # Player 4 trail - light green
        }
        
        # Visualization settings
        self.map_zoom = 0.3  # Show central portion of map for better visibility
        self.center_x = self.env.MAP_SIZE // 2
        self.center_y = self.env.MAP_SIZE // 2
        self.zoom_size = int(self.env.MAP_SIZE * self.map_zoom)
        
    def render_game_state(self, show_trails=True, show_observations=False, target_player=1, figsize=(15, 10)):
        """
        Render current game state with optional observation visualization
        """
        fig, axes = plt.subplots(1, 3 if show_observations else 1, figsize=figsize)
        
        if not isinstance(axes, np.ndarray):
            axes = [axes]
        
        # Main game view
        self._render_main_view(axes[0], show_trails)
        
        if show_observations and target_player in self.env.players:
            # Player observation visualization
            self._render_observations(axes[1], target_player)
            
            # Game statistics
            self._render_statistics(axes[2])
        
        plt.tight_layout()
        plt.show()
        
        return fig
    
    def _render_main_view(self, ax, show_trails=True):
        """Render the main game view"""
        # Determine view area
        view_size = min(50, self.env.MAP_SIZE // 2)  # Show central area
        
        # Find active area (where players are)
        active_x = []
        active_y = []
        for player in self.env.players.values():
            if player.is_alive:
                active_x.append(player.position[0])
                active_y.append(player.position[1])
        
        if active_x:
            center_x = int(np.mean(active_x))
            center_y = int(np.mean(active_y))
        else:
            center_x, center_y = self.env.MAP_SIZE // 2, self.env.MAP_SIZE // 2
        
        # Clamp to map bounds
        start_x = max(0, center_x - view_size // 2)
        end_x = min(self.env.MAP_SIZE, start_x + view_size)
        start_y = max(0, center_y - view_size // 2)  
        end_y = min(self.env.MAP_SIZE, start_y + view_size)
        
        # Extract view area
        tile_view = self.env.tile_map[start_y:end_y, start_x:end_x]
        trail_view = self.env.trail_map[start_y:end_y, start_x:end_x] if show_trails else None
        
        # Create RGB image
        height, width = tile_view.shape
        rgb_image = np.ones((height, width, 3))
        
        # Color tiles
        for player_id in range(1, 5):
            mask = tile_view == player_id
            color = np.array([int(self.player_colors[player_id][i:i+2], 16) for i in (1, 3, 5)]) / 255.0
            rgb_image[mask] = color
        
        # Overlay trails if enabled
        if show_trails and trail_view is not None:
            for player_id in range(1, 5):
                trail_mask = trail_view == player_id
                if np.any(trail_mask):
                    trail_color = np.array([int(self.trail_colors[player_id][i:i+2], 16) for i in (1, 3, 5)]) / 255.0
                    # Blend trail color with existing color
                    rgb_image[trail_mask] = rgb_image[trail_mask] * 0.5 + trail_color * 0.5
        
        # Display image
        ax.imshow(rgb_image, origin='lower', extent=[start_x, end_x, start_y, end_y])
        
        # Add player positions and info
        for player_id, player in self.env.players.items():
            if player.is_alive:
                x, y = player.position
                if start_x <= x < end_x and start_y <= y < end_y:
                    # Player marker
                    ax.plot(x, y, 'o', color='black', markersize=8, markeredgewidth=2)
                    ax.plot(x, y, 'o', color=self.player_colors[player_id], markersize=6)
                    
                    # Player info
                    info_text = f"P{player_id}\\nScore: {self.env.scores[player_id]}\\nTrail: {len(player.trail)}"
                    ax.annotate(info_text, (x, y), xytext=(5, 5), textcoords='offset points',
                               bbox=dict(boxstyle='round,pad=0.3', facecolor='white', alpha=0.8),
                               fontsize=8)
        
        ax.set_title('Paper.io Game State')
        ax.set_xlabel('X Position')
        ax.set_ylabel('Y Position')
        ax.grid(True, alpha=0.3)
        
        # Add legend
        legend_elements = []
        for player_id in range(1, 5):
            if player_id in self.env.players and self.env.players[player_id].is_alive:
                legend_elements.append(patches.Patch(color=self.player_colors[player_id], 
                                                    label=f'Player {player_id}'))
        ax.legend(handles=legend_elements, loc='upper right')
    
    def _render_observations(self, ax, player_id):
        """Render player's observation space"""
        if player_id not in self.env.players:
            ax.text(0.5, 0.5, f'Player {player_id} not found', ha='center', va='center')
            return
        
        # Get player observation
        obs = self.env._get_player_observation(player_id)
        
        # Reshape observation into components
        ultra_critical = obs[:45].reshape(5, 9)  # 5 repetitions of 3x3
        critical_proximity = obs[45:54].reshape(3, 3)
        immediate_danger = obs[54:64]
        enemy_threat = obs[64:79].reshape(3, 5)  # 3 enemies, 5 dims each
        basic_info = obs[79:84]
        
        # Create visualization grid
        grid_height = 4
        grid_width = 2
        
        # Ultra critical observations (average across repetitions)
        avg_ultra_critical = np.mean(ultra_critical, axis=0).reshape(3, 3)
        
        # Plot heatmaps
        im1 = ax.imshow(avg_ultra_critical, cmap='RdYlBu_r', aspect='equal', 
                       extent=[0, 3, 0, 3], vmin=-100, vmax=100)
        ax.set_title(f'Player {player_id} Ultra-Critical 3x3 Observations\\n(Average across 5 repetitions)')
        
        # Add value annotations
        for i in range(3):
            for j in range(3):
                text = ax.text(j+0.5, i+0.5, f'{avg_ultra_critical[i, j]:.1f}', 
                             ha="center", va="center", color="black", fontsize=8)
        
        ax.set_xticks([0.5, 1.5, 2.5])
        ax.set_yticks([0.5, 1.5, 2.5])
        ax.set_xticklabels(['Left', 'Center', 'Right'])
        ax.set_yticklabels(['Down', 'Center', 'Up'])
        
        plt.colorbar(im1, ax=ax, shrink=0.8)
    
    def _render_statistics(self, ax):
        """Render game statistics"""
        # Prepare data
        players = list(range(1, 5))
        scores = [self.env.scores[pid] for pid in players]
        trail_lengths = [len(self.env.players[pid].trail) if self.env.players[pid].is_alive else 0 
                        for pid in players]
        alive_status = [self.env.players[pid].is_alive for pid in players]
        
        # Create bar plot
        x = np.arange(len(players))
        width = 0.35
        
        bars1 = ax.bar(x - width/2, scores, width, label='Territory Score', 
                      color=[self.player_colors[pid] for pid in players], alpha=0.7)
        bars2 = ax.bar(x + width/2, trail_lengths, width, label='Trail Length',
                      color=[self.player_colors[pid] for pid in players], alpha=0.5)
        
        # Add status indicators
        for i, (pid, alive) in enumerate(zip(players, alive_status)):
            status = "Alive" if alive else "Dead"
            ax.text(i, max(scores[i], trail_lengths[i]) + 1, status, 
                   ha='center', va='bottom', fontweight='bold',
                   color='green' if alive else 'red')
        
        ax.set_xlabel('Player')
        ax.set_ylabel('Count')
        ax.set_title('Player Statistics')
        ax.set_xticks(x)
        ax.set_xticklabels([f'Player {pid}' for pid in players])
        ax.legend()
        
        # Add value labels on bars
        for bar in bars1:
            height = bar.get_height()
            ax.text(bar.get_x() + bar.get_width()/2., height + 0.5,
                   f'{int(height)}', ha='center', va='bottom', fontsize=8)
        
        for bar in bars2:
            height = bar.get_height()
            ax.text(bar.get_x() + bar.get_width()/2., height + 0.5,
                   f'{int(height)}', ha='center', va='bottom', fontsize=8)
    
    def analyze_observations(self, player_id=1, display_components=True):
        """
        Detailed analysis of player observations
        """
        if player_id not in self.env.players:
            print(f"Player {player_id} not found")
            return
        
        obs = self.env._get_player_observation(player_id)
        player = self.env.players[player_id]
        
        print(f"=== Observation Analysis for Player {player_id} ===")
        print(f"Position: {player.position}")
        print(f"Direction: {player.direction}")
        print(f"Score: {self.env.scores[player_id]}")
        print(f"Trail Length: {len(player.trail)}")
        print(f"In Safe Zone: {player.is_in_safe_zone}")
        print(f"Total Observation Dims: {len(obs)}")
        
        if display_components:
            # Break down observation components
            ultra_critical = obs[:45]
            critical_proximity = obs[45:54]
            immediate_danger = obs[54:64]
            enemy_threat = obs[64:79]
            basic_info = obs[79:84]
            
            print(f"\\n--- Ultra Critical 3x3 (45 dims) ---")
            ultra_reshaped = ultra_critical.reshape(5, 9)
            for rep in range(5):
                print(f"Repetition {rep+1}: {ultra_reshaped[rep]}")
            
            print(f"\\n--- Critical Proximity (9 dims) ---")
            print(f"3x3 Grid: {critical_proximity.reshape(3, 3)}")
            
            print(f"\\n--- Immediate Danger (10 dims) ---")
            print(f"Danger levels: {immediate_danger}")
            
            print(f"\\n--- Enemy Threat Assessment (15 dims) ---")
            enemy_reshaped = enemy_threat.reshape(3, 5)
            for enemy_idx in range(3):
                print(f"Enemy {enemy_idx+1}: {enemy_reshaped[enemy_idx]}")
            
            print(f"\\n--- Basic Info (5 dims) ---")
            print(f"[x_norm, y_norm, dir_x, dir_y, score_norm]: {basic_info}")
        
        return obs
    
    def create_training_dashboard(self, metrics_history):
        """
        Create a comprehensive training dashboard
        """
        fig, axes = plt.subplots(2, 3, figsize=(18, 12))
        fig.suptitle('Paper.io Training Dashboard', fontsize=16)
        
        # 1. Episode Rewards
        ax = axes[0, 0]
        for player_id in range(1, 5):
            if player_id in metrics_history:
                rewards = metrics_history[player_id]['episode_rewards']
                if rewards:
                    ax.plot(rewards, label=f'Player {player_id}', 
                           color=self.player_colors[player_id], alpha=0.7)
        ax.set_title('Episode Rewards')
        ax.set_xlabel('Episode')
        ax.set_ylabel('Reward')
        ax.legend()
        ax.grid(True, alpha=0.3)
        
        # 2. Episode Scores
        ax = axes[0, 1]
        for player_id in range(1, 5):
            if player_id in metrics_history:
                scores = metrics_history[player_id]['episode_scores']
                if scores:
                    ax.plot(scores, label=f'Player {player_id}',
                           color=self.player_colors[player_id], alpha=0.7)
        ax.set_title('Episode Scores (Territory)')
        ax.set_xlabel('Episode')
        ax.set_ylabel('Score')
        ax.legend()
        ax.grid(True, alpha=0.3)
        
        # 3. Win Rate Distribution
        ax = axes[0, 2]
        win_counts = {pid: metrics_history[pid].get('wins', 0) if pid in metrics_history else 0 
                     for pid in range(1, 5)}
        colors = [self.player_colors[pid] for pid in range(1, 5)]
        wedges, texts, autotexts = ax.pie(win_counts.values(), labels=[f'Player {pid}' for pid in range(1, 5)],
                                         colors=colors, autopct='%1.1f%%')
        ax.set_title('Win Distribution')
        
        # 4. Moving Average Rewards
        ax = axes[1, 0]
        window = 10
        for player_id in range(1, 5):
            if player_id in metrics_history:
                rewards = metrics_history[player_id]['episode_rewards']
                if len(rewards) >= window:
                    moving_avg = [np.mean(rewards[max(0, i-window+1):i+1]) for i in range(len(rewards))]
                    ax.plot(moving_avg, label=f'Player {player_id}',
                           color=self.player_colors[player_id], linewidth=2)
        ax.set_title(f'Moving Average Rewards (window={window})')
        ax.set_xlabel('Episode')
        ax.set_ylabel('Average Reward')
        ax.legend()
        ax.grid(True, alpha=0.3)
        
        # 5. Episode Lengths
        ax = axes[1, 1]
        for player_id in range(1, 5):
            if player_id in metrics_history:
                lengths = metrics_history[player_id]['episode_lengths']
                if lengths:
                    ax.plot(lengths, label=f'Player {player_id}',
                           color=self.player_colors[player_id], alpha=0.7)
        ax.set_title('Episode Lengths')
        ax.set_xlabel('Episode')
        ax.set_ylabel('Steps')
        ax.legend()
        ax.grid(True, alpha=0.3)
        
        # 6. Performance Summary Table
        ax = axes[1, 2]
        ax.axis('tight')
        ax.axis('off')
        
        # Create summary table
        table_data = []
        headers = ['Player', 'Avg Reward', 'Avg Score', 'Episodes', 'Win Rate']
        
        for player_id in range(1, 5):
            if player_id in metrics_history:
                data = metrics_history[player_id]
                avg_reward = np.mean(data['episode_rewards']) if data['episode_rewards'] else 0
                avg_score = np.mean(data['episode_scores']) if data['episode_scores'] else 0
                num_episodes = len(data['episode_rewards'])
                win_rate = data.get('wins', 0) / max(1, num_episodes) * 100
                
                table_data.append([
                    f'Player {player_id}',
                    f'{avg_reward:.2f}',
                    f'{avg_score:.1f}',
                    str(num_episodes),
                    f'{win_rate:.1f}%'
                ])
        
        table = ax.table(cellText=table_data, colLabels=headers, 
                        cellLoc='center', loc='center')
        table.auto_set_font_size(False)
        table.set_fontsize(10)
        table.scale(1, 2)
        ax.set_title('Performance Summary')
        
        plt.tight_layout()
        return fig
    
    def animate_game(self, max_steps=200, interval=200, save_gif=False, gif_filename='paperio_game.gif'):
        """
        Create an animated visualization of a game episode
        """
        # Reset environment
        self.env.reset()
        
        # Storage for animation frames
        frames = []
        
        fig, ax = plt.subplots(figsize=(10, 10))
        
        def animate_frame(frame):
            ax.clear()
            
            # Get random actions for demonstration
            actions = {pid: np.random.randint(0, 4) for pid in range(1, 5)}
            
            # Step environment
            self.env.step(actions)
            
            # Render frame
            self._render_main_view(ax, show_trails=True)
            ax.set_title(f'Paper.io Animation - Step {frame}')
            
            return ax.artists
        
        # Create animation
        anim = FuncAnimation(fig, animate_frame, frames=max_steps, 
                           interval=interval, blit=False, repeat=False)
        
        if save_gif:
            anim.save(gif_filename, writer='pillow', fps=5)
            print(f"Animation saved as {gif_filename}")
        
        plt.show()
        return anim
    
    def export_game_state(self, filename='game_state.png', dpi=300):
        """Export current game state as high-resolution image"""
        fig = self.render_game_state(figsize=(20, 12))
        fig.savefig(filename, dpi=dpi, bbox_inches='tight')
        print(f"Game state exported as {filename}")
        return filename

def demo_visualization():
    """
    Demo function showing visualization capabilities
    """
    print("=== Paper.io Visualization Demo ===")
    
    # Create environment and visualizer
    env = PaperIOEnv()
    visualizer = PaperIOVisualizer(env)
    
    # Reset environment
    env.reset()
    
    # Run a few random steps
    for step in range(20):
        actions = {pid: np.random.randint(0, 4) for pid in range(1, 5)}
        env.step(actions)
    
    # Show basic visualization
    print("\\n1. Basic Game State Visualization:")
    visualizer.render_game_state()
    
    # Show with observations
    print("\\n2. Game State with Player 1 Observations:")
    visualizer.render_game_state(show_observations=True, target_player=1)
    
    # Analyze observations
    print("\\n3. Detailed Observation Analysis:")
    visualizer.analyze_observations(player_id=1)
    
    # Create mock training dashboard
    print("\\n4. Training Dashboard (with mock data):")
    mock_metrics = {}
    for pid in range(1, 5):
        mock_metrics[pid] = {
            'episode_rewards': np.random.normal(10, 5, 50).tolist(),
            'episode_scores': np.random.randint(20, 100, 50).tolist(),
            'episode_lengths': np.random.randint(50, 200, 50).tolist(),
            'wins': np.random.randint(5, 15)
        }
    
    visualizer.create_training_dashboard(mock_metrics)
    
    print("\\nVisualization demo completed!")

if __name__ == "__main__":
    demo_visualization()