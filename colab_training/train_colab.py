"""
Paper.io ML-Agents Training Script for Google Colab
Implements PPO training compatible with ML-Agents framework
"""

import os
import sys
import numpy as np
import yaml
from typing import Dict, List, Tuple, Optional
import matplotlib.pyplot as plt
from collections import deque
import time
import pickle

# ML-Agents imports
try:
    from mlagents_envs.environment import UnityEnvironment
    from mlagents_envs.envs.unity_gym_env import UnityToGymWrapper
    from mlagents.trainers.ppo.trainer import PPOTrainer
    from mlagents.trainers.trainer_controller import TrainerController
    from mlagents.trainers.settings import TrainerSettings, RunOptions
    from mlagents.trainers.stats import StatsReporter
    from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
    MLAGENTS_AVAILABLE = True
except ImportError:
    print("ML-Agents not available. Installing...")
    os.system("pip install mlagents>=0.30.0")
    try:
        from mlagents_envs.environment import UnityEnvironment
        from mlagents_envs.envs.unity_gym_env import UnityToGymWrapper
        MLAGENTS_AVAILABLE = True
    except ImportError:
        print("Failed to install ML-Agents. Falling back to pure PyTorch implementation.")
        MLAGENTS_AVAILABLE = False

# Alternative: Pure PyTorch implementation for when ML-Agents is not available
import torch
import torch.nn as nn
import torch.optim as optim
import torch.nn.functional as F
from torch.distributions import Categorical

from paper_io_env import PaperIOEnv

class PPOAgent(nn.Module):
    """
    PPO Agent implementation matching ML-Agents configuration
    """
    def __init__(self, observation_dim=84, action_dim=4, hidden_units=512, num_layers=3):
        super(PPOAgent, self).__init__()
        
        self.observation_dim = observation_dim
        self.action_dim = action_dim
        
        # Build network layers
        layers = []
        input_dim = observation_dim
        
        for i in range(num_layers):
            layers.append(nn.Linear(input_dim, hidden_units))
            layers.append(nn.ReLU())
            input_dim = hidden_units
        
        self.shared_network = nn.Sequential(*layers)
        
        # Policy head (actor)
        self.policy_head = nn.Linear(hidden_units, action_dim)
        
        # Value head (critic) 
        self.value_head = nn.Linear(hidden_units, 1)
        
        # LSTM memory (simplified version of ML-Agents memory)
        self.memory_size = 256
        self.lstm = nn.LSTM(hidden_units, self.memory_size, batch_first=True)
        self.memory_policy = nn.Linear(self.memory_size, action_dim)
        self.memory_value = nn.Linear(self.memory_size, 1)
        
    def forward(self, x, memory_state=None, use_memory=False):
        """Forward pass through the network"""
        batch_size = x.shape[0]
        
        # Shared network
        features = self.shared_network(x)
        
        if use_memory and memory_state is not None:
            # Use LSTM memory
            if len(features.shape) == 2:
                features = features.unsqueeze(1)  # Add sequence dimension
            
            lstm_out, new_memory_state = self.lstm(features, memory_state)
            features = lstm_out.squeeze(1)
            
            policy_logits = self.memory_policy(features)
            value = self.memory_value(features)
        else:
            # Standard forward pass
            policy_logits = self.policy_head(features)
            value = self.value_head(features)
            new_memory_state = None
        
        return policy_logits, value, new_memory_state
    
    def get_action(self, observation, memory_state=None, deterministic=False):
        """Get action from observation"""
        with torch.no_grad():
            if isinstance(observation, np.ndarray):
                observation = torch.FloatTensor(observation).unsqueeze(0)
            
            policy_logits, value, new_memory_state = self.forward(observation, memory_state, use_memory=(memory_state is not None))
            
            # Apply action mask if provided
            # action_mask logic would go here
            
            if deterministic:
                action = torch.argmax(policy_logits, dim=-1)
            else:
                action_dist = Categorical(logits=policy_logits)
                action = action_dist.sample()
            
            log_prob = F.log_softmax(policy_logits, dim=-1)[0, action.item()]
            
        return action.item(), log_prob.item(), value.item(), new_memory_state

class TrainingMetrics:
    """Track training metrics and statistics"""
    def __init__(self):
        self.episode_rewards = deque(maxlen=100)
        self.episode_lengths = deque(maxlen=100)
        self.episode_scores = deque(maxlen=100)
        self.total_steps = 0
        self.episodes = 0
        
    def add_episode(self, reward, length, score):
        self.episode_rewards.append(reward)
        self.episode_lengths.append(length)
        self.episode_scores.append(score)
        self.episodes += 1
        
    def get_stats(self):
        if len(self.episode_rewards) == 0:
            return {"mean_reward": 0, "mean_length": 0, "mean_score": 0}
            
        return {
            "mean_reward": np.mean(self.episode_rewards),
            "mean_length": np.mean(self.episode_lengths),
            "mean_score": np.mean(self.episode_scores),
            "episodes": self.episodes,
            "total_steps": self.total_steps
        }

class PaperIOTrainer:
    """
    Main trainer class for Paper.io ML-Agents
    """
    def __init__(self, config_path="config.yaml", use_tensorboard=True):
        self.config = self._load_config(config_path)
        self.use_tensorboard = use_tensorboard
        
        # Initialize environment
        self.env = PaperIOEnv()
        
        # Initialize agents (one for each player)
        self.agents = {}
        self.optimizers = {}
        self.metrics = {}
        
        for player_id in range(1, 5):  # 4 players
            agent = PPOAgent(
                observation_dim=84,
                action_dim=4,
                hidden_units=self.config.get('hidden_units', 512),
                num_layers=self.config.get('num_layers', 3)
            )
            
            optimizer = optim.Adam(
                agent.parameters(), 
                lr=self.config.get('learning_rate', 3e-4)
            )
            
            self.agents[player_id] = agent
            self.optimizers[player_id] = optimizer
            self.metrics[player_id] = TrainingMetrics()
        
        # Training parameters
        self.batch_size = self.config.get('batch_size', 2048)
        self.buffer_size = self.config.get('buffer_size', 20480)
        self.num_epochs = self.config.get('num_epoch', 3)
        self.gamma = self.config.get('gamma', 0.99)
        self.gae_lambda = self.config.get('lambd', 0.95)
        self.clip_epsilon = self.config.get('epsilon', 0.2)
        self.value_loss_coef = 0.5
        self.entropy_coef = 0.01
        
        # Experience buffer
        self.experience_buffer = {pid: [] for pid in range(1, 5)}
        
        # Tensorboard setup
        if self.use_tensorboard:
            try:
                from torch.utils.tensorboard import SummaryWriter
                self.writer = SummaryWriter(f"runs/paperio_{int(time.time())}")
            except ImportError:
                print("Tensorboard not available. Install with: pip install tensorboard")
                self.use_tensorboard = False
    
    def _load_config(self, config_path):
        """Load training configuration"""
        default_config = {
            'batch_size': 2048,
            'buffer_size': 20480,
            'learning_rate': 3e-4,
            'beta': 5e-3,
            'epsilon': 0.2,
            'lambd': 0.95,
            'num_epoch': 3,
            'gamma': 0.99,
            'hidden_units': 512,
            'num_layers': 3,
            'max_steps': 5000000,
            'time_horizon': 20,
            'summary_freq': 2000,
            'checkpoint_interval': 50000
        }
        
        if os.path.exists(config_path):
            with open(config_path, 'r') as f:
                config = yaml.safe_load(f)
                # Extract behavior config if using ML-Agents format
                if 'behaviors' in config:
                    behavior_name = list(config['behaviors'].keys())[0]
                    behavior_config = config['behaviors'][behavior_name]
                    
                    # Map ML-Agents config to our format
                    hyperparams = behavior_config.get('hyperparameters', {})
                    network_settings = behavior_config.get('network_settings', {})
                    
                    default_config.update({
                        'batch_size': hyperparams.get('batch_size', default_config['batch_size']),
                        'buffer_size': hyperparams.get('buffer_size', default_config['buffer_size']),
                        'learning_rate': hyperparams.get('learning_rate', default_config['learning_rate']),
                        'epsilon': hyperparams.get('epsilon', default_config['epsilon']),
                        'lambd': hyperparams.get('lambd', default_config['lambd']),
                        'num_epoch': hyperparams.get('num_epoch', default_config['num_epoch']),
                        'hidden_units': network_settings.get('hidden_units', default_config['hidden_units']),
                        'num_layers': network_settings.get('num_layers', default_config['num_layers']),
                        'max_steps': behavior_config.get('max_steps', default_config['max_steps'])
                    })
                else:
                    default_config.update(config)
        
        return default_config
    
    def collect_experience(self, num_steps=1000):
        """Collect experience from environment"""
        obs = self.env.reset()[0]  # Get initial observations
        
        for step in range(num_steps):
            actions = {}
            action_log_probs = {}
            values = {}
            
            # Get actions from all agents
            for player_id in range(1, 5):
                if player_id in obs:
                    action, log_prob, value, _ = self.agents[player_id].get_action(obs[player_id])
                    actions[player_id] = action
                    action_log_probs[player_id] = log_prob
                    values[player_id] = value
                else:
                    actions[player_id] = 0  # Default action for dead/missing players
                    action_log_probs[player_id] = 0.0
                    values[player_id] = 0.0
            
            # Execute actions
            next_obs, rewards, dones, infos = self.env.step(actions)
            
            # Store experience
            for player_id in range(1, 5):
                if player_id in obs:
                    experience = {
                        'observation': obs[player_id],
                        'action': actions[player_id],
                        'reward': rewards.get(player_id, 0.0),
                        'value': values[player_id],
                        'log_prob': action_log_probs[player_id],
                        'done': dones.get(player_id, False)
                    }
                    self.experience_buffer[player_id].append(experience)
            
            obs = next_obs
            
            # Reset if episode done
            if all(dones.values()):
                obs = self.env.reset()[0]
                
                # Update metrics
                for player_id in range(1, 5):
                    player_reward = sum(exp['reward'] for exp in self.experience_buffer[player_id])
                    self.metrics[player_id].add_episode(
                        reward=player_reward,
                        length=len(self.experience_buffer[player_id]),
                        score=self.env.scores.get(player_id, 0)
                    )
    
    def compute_gae(self, rewards, values, dones, next_value=0):
        """Compute Generalized Advantage Estimation"""
        advantages = []
        advantage = 0
        
        for i in reversed(range(len(rewards))):
            if i == len(rewards) - 1:
                next_value_i = next_value
            else:
                next_value_i = values[i + 1]
            
            delta = rewards[i] + self.gamma * next_value_i * (1 - dones[i]) - values[i]
            advantage = delta + self.gamma * self.gae_lambda * (1 - dones[i]) * advantage
            advantages.insert(0, advantage)
        
        returns = [adv + val for adv, val in zip(advantages, values)]
        return advantages, returns
    
    def update_agent(self, player_id):
        """Update a specific agent using PPO"""
        if len(self.experience_buffer[player_id]) < self.batch_size:
            return
        
        agent = self.agents[player_id]
        optimizer = self.optimizers[player_id]
        
        # Prepare batch data
        experiences = self.experience_buffer[player_id]
        
        observations = torch.FloatTensor([exp['observation'] for exp in experiences])
        actions = torch.LongTensor([exp['action'] for exp in experiences])
        rewards = [exp['reward'] for exp in experiences]
        values = [exp['value'] for exp in experiences]
        old_log_probs = torch.FloatTensor([exp['log_prob'] for exp in experiences])
        dones = [exp['done'] for exp in experiences]
        
        # Compute advantages and returns
        advantages, returns = self.compute_gae(rewards, values, dones)
        advantages = torch.FloatTensor(advantages)
        returns = torch.FloatTensor(returns)
        
        # Normalize advantages
        advantages = (advantages - advantages.mean()) / (advantages.std() + 1e-8)
        
        # PPO update
        for epoch in range(self.num_epochs):
            # Forward pass
            policy_logits, current_values, _ = agent(observations)
            current_values = current_values.squeeze()
            
            # Policy loss
            action_dist = Categorical(logits=policy_logits)
            new_log_probs = action_dist.log_prob(actions)
            
            ratio = torch.exp(new_log_probs - old_log_probs)
            
            surr1 = ratio * advantages
            surr2 = torch.clamp(ratio, 1 - self.clip_epsilon, 1 + self.clip_epsilon) * advantages
            policy_loss = -torch.min(surr1, surr2).mean()
            
            # Value loss
            value_loss = F.mse_loss(current_values, returns)
            
            # Entropy loss
            entropy_loss = -action_dist.entropy().mean()
            
            # Total loss
            total_loss = policy_loss + self.value_loss_coef * value_loss + self.entropy_coef * entropy_loss
            
            # Update
            optimizer.zero_grad()
            total_loss.backward()
            torch.nn.utils.clip_grad_norm_(agent.parameters(), 0.5)
            optimizer.step()
        
        # Clear buffer
        self.experience_buffer[player_id] = []
        
        # Log metrics
        if self.use_tensorboard:
            stats = self.metrics[player_id].get_stats()
            self.writer.add_scalar(f'Player_{player_id}/Policy_Loss', policy_loss.item(), stats['episodes'])
            self.writer.add_scalar(f'Player_{player_id}/Value_Loss', value_loss.item(), stats['episodes'])
            self.writer.add_scalar(f'Player_{player_id}/Total_Loss', total_loss.item(), stats['episodes'])
            self.writer.add_scalar(f'Player_{player_id}/Mean_Reward', stats['mean_reward'], stats['episodes'])
            self.writer.add_scalar(f'Player_{player_id}/Mean_Score', stats['mean_score'], stats['episodes'])
    
    def train(self, total_steps=500000, save_interval=50000, log_interval=1000):
        """Main training loop"""
        print(f"Starting training for {total_steps} steps...")
        print(f"Configuration: {self.config}")
        
        step_count = 0
        last_save = 0
        last_log = 0
        
        while step_count < total_steps:
            # Collect experience
            collect_steps = min(self.buffer_size, total_steps - step_count)
            self.collect_experience(collect_steps)
            step_count += collect_steps
            
            # Update all agents
            for player_id in range(1, 5):
                self.update_agent(player_id)
            
            # Logging
            if step_count - last_log >= log_interval:
                self._log_progress(step_count, total_steps)
                last_log = step_count
            
            # Save checkpoints
            if step_count - last_save >= save_interval:
                self.save_models(f"checkpoint_{step_count}")
                last_save = step_count
        
        # Final save
        self.save_models("final_model")
        print("Training completed!")
    
    def _log_progress(self, current_step, total_steps):
        """Log training progress"""
        print(f"\\nStep {current_step}/{total_steps} ({100*current_step/total_steps:.1f}%)")
        
        for player_id in range(1, 5):
            stats = self.metrics[player_id].get_stats()
            print(f"Player {player_id}: Reward={stats['mean_reward']:.2f}, Score={stats['mean_score']:.1f}, Episodes={stats['episodes']}")
        
        # Create progress plot
        self.plot_training_progress()
    
    def plot_training_progress(self):
        """Plot training progress"""
        fig, axes = plt.subplots(2, 2, figsize=(12, 8))
        fig.suptitle('Training Progress')
        
        # Plot rewards
        axes[0, 0].set_title('Episode Rewards')
        for player_id in range(1, 5):
            rewards = list(self.metrics[player_id].episode_rewards)
            if rewards:
                axes[0, 0].plot(rewards, label=f'Player {player_id}', alpha=0.7)
        axes[0, 0].legend()
        axes[0, 0].set_xlabel('Episode')
        axes[0, 0].set_ylabel('Reward')
        
        # Plot scores
        axes[0, 1].set_title('Episode Scores')
        for player_id in range(1, 5):
            scores = list(self.metrics[player_id].episode_scores)
            if scores:
                axes[0, 1].plot(scores, label=f'Player {player_id}', alpha=0.7)
        axes[0, 1].legend()
        axes[0, 1].set_xlabel('Episode')
        axes[0, 1].set_ylabel('Score')
        
        # Plot episode lengths
        axes[1, 0].set_title('Episode Lengths')
        for player_id in range(1, 5):
            lengths = list(self.metrics[player_id].episode_lengths)
            if lengths:
                axes[1, 0].plot(lengths, label=f'Player {player_id}', alpha=0.7)
        axes[1, 0].legend()
        axes[1, 0].set_xlabel('Episode')
        axes[1, 0].set_ylabel('Steps')
        
        # Plot mean rewards (smoothed)
        axes[1, 1].set_title('Mean Rewards (Last 10 Episodes)')
        for player_id in range(1, 5):
            rewards = list(self.metrics[player_id].episode_rewards)
            if len(rewards) >= 10:
                smoothed = [np.mean(rewards[max(0, i-9):i+1]) for i in range(len(rewards))]
                axes[1, 1].plot(smoothed, label=f'Player {player_id}')
        axes[1, 1].legend()
        axes[1, 1].set_xlabel('Episode')
        axes[1, 1].set_ylabel('Mean Reward')
        
        plt.tight_layout()
        plt.savefig('training_progress.png', dpi=100, bbox_inches='tight')
        plt.show()
    
    def save_models(self, checkpoint_name):
        """Save trained models"""
        os.makedirs('models', exist_ok=True)
        
        for player_id in range(1, 5):
            model_path = f"models/{checkpoint_name}_player_{player_id}.pth"
            torch.save({
                'model_state_dict': self.agents[player_id].state_dict(),
                'optimizer_state_dict': self.optimizers[player_id].state_dict(),
                'config': self.config,
                'metrics': {
                    'episodes': self.metrics[player_id].episodes,
                    'total_steps': self.metrics[player_id].total_steps
                }
            }, model_path)
        
        print(f"Models saved as {checkpoint_name}")
    
    def load_models(self, checkpoint_name):
        """Load trained models"""
        for player_id in range(1, 5):
            model_path = f"models/{checkpoint_name}_player_{player_id}.pth"
            if os.path.exists(model_path):
                checkpoint = torch.load(model_path)
                self.agents[player_id].load_state_dict(checkpoint['model_state_dict'])
                self.optimizers[player_id].load_state_dict(checkpoint['optimizer_state_dict'])
                print(f"Loaded model for Player {player_id}")
            else:
                print(f"Model file not found: {model_path}")
    
    def evaluate(self, num_episodes=10):
        """Evaluate trained agents"""
        print(f"Evaluating agents for {num_episodes} episodes...")
        
        eval_metrics = {pid: {'rewards': [], 'scores': [], 'wins': 0} for pid in range(1, 5)}
        
        for episode in range(num_episodes):
            obs = self.env.reset()[0]
            episode_rewards = {pid: 0 for pid in range(1, 5)}
            done = False
            
            while not done:
                actions = {}
                
                # Get actions (deterministic for evaluation)
                for player_id in range(1, 5):
                    if player_id in obs:
                        action, _, _, _ = self.agents[player_id].get_action(obs[player_id], deterministic=True)
                        actions[player_id] = action
                    else:
                        actions[player_id] = 0
                
                obs, rewards, dones, _ = self.env.step(actions)
                
                for player_id in range(1, 5):
                    episode_rewards[player_id] += rewards.get(player_id, 0)
                
                done = all(dones.values())
            
            # Record episode results
            for player_id in range(1, 5):
                eval_metrics[player_id]['rewards'].append(episode_rewards[player_id])
                eval_metrics[player_id]['scores'].append(self.env.scores.get(player_id, 0))
            
            # Determine winner
            winner = max(self.env.scores.keys(), key=lambda x: self.env.scores[x])
            eval_metrics[winner]['wins'] += 1
            
            print(f"Episode {episode + 1}: Scores = {self.env.scores}, Winner = Player {winner}")
        
        # Print evaluation summary
        print("\\nEvaluation Summary:")
        for player_id in range(1, 5):
            metrics = eval_metrics[player_id]
            print(f"Player {player_id}:")
            print(f"  Mean Reward: {np.mean(metrics['rewards']):.2f}")
            print(f"  Mean Score: {np.mean(metrics['scores']):.1f}")
            print(f"  Wins: {metrics['wins']}/{num_episodes} ({100*metrics['wins']/num_episodes:.1f}%)")
        
        return eval_metrics

def main():
    """Main training function for Google Colab"""
    import argparse
    
    parser = argparse.ArgumentParser(description="Train Paper.io ML-Agents")
    parser.add_argument("--config", default="config.yaml", help="Config file path")
    parser.add_argument("--steps", type=int, default=500000, help="Total training steps")
    parser.add_argument("--eval", action="store_true", help="Evaluate trained model")
    parser.add_argument("--load", type=str, help="Load checkpoint name")
    parser.add_argument("--tensorboard", action="store_true", default=True, help="Use Tensorboard")
    
    args = parser.parse_args()
    
    # Create trainer
    trainer = PaperIOTrainer(
        config_path=args.config,
        use_tensorboard=args.tensorboard
    )
    
    # Load checkpoint if specified
    if args.load:
        trainer.load_models(args.load)
    
    if args.eval:
        # Evaluation mode
        trainer.evaluate(num_episodes=20)
    else:
        # Training mode
        trainer.train(total_steps=args.steps)

if __name__ == "__main__":
    main()