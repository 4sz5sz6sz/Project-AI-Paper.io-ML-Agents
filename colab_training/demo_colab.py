#!/usr/bin/env python3
"""
Paper.io ML-Agents Colab Demo Script
Quick demonstration and testing script for Google Colab

Usage in Google Colab:
!git clone https://github.com/4sz5sz6sz/Project-AI-Paper.io-ML-Agents.git
%cd Project-AI-Paper.io-ML-Agents/colab_training
!pip install -r requirements.txt
!python demo_colab.py
"""

import sys
import os
import time
import numpy as np
import matplotlib.pyplot as plt
import matplotlib
matplotlib.use('Agg')  # Use non-interactive backend for Colab

def check_environment():
    """Check if we're running in Google Colab"""
    try:
        import google.colab
        return True
    except ImportError:
        return False

def install_dependencies():
    """Install required dependencies"""
    print("📦 Installing dependencies...")
    os.system("pip install -q numpy gym matplotlib seaborn torch PyYAML")
    print("✅ Dependencies installed!")

def demo_environment():
    """Demonstrate the Paper.io environment"""
    print("\n🎮 === Paper.io Environment Demo ===")
    
    from paper_io_env import PaperIOEnv
    
    # Create environment
    env = PaperIOEnv()
    print(f"✅ Environment created (Map: {env.MAP_SIZE}x{env.MAP_SIZE}, Players: {env.NUM_PLAYERS})")
    
    # Reset environment
    obs = env.reset()
    print(f"✅ Environment reset - Observation dimensions: {[len(obs[0][pid]) for pid in obs[0]]}")
    
    # Run some random steps
    print("🏃 Running 20 random steps...")
    for step in range(20):
        actions = {pid: np.random.randint(0, 4) for pid in range(1, 5)}
        obs, rewards, dones, infos = env.step(actions)
        
        if step % 5 == 0:
            print(f"  Step {step}: Scores = {dict(env.scores)}")
            alive_players = sum(1 for p in env.players.values() if p.is_alive)
            print(f"           Alive players: {alive_players}/4")
        
        # Reset if episode ends
        if all(dones.values()):
            print("  Episode ended, resetting...")
            obs = env.reset()
    
    print("✅ Environment demo completed!")
    return env

def demo_visualization(env):
    """Demonstrate visualization capabilities"""
    print("\n📊 === Visualization Demo ===")
    
    from visualizer import PaperIOVisualizer
    
    visualizer = PaperIOVisualizer(env)
    print("✅ Visualizer created")
    
    # Run a few more steps for interesting visualization
    for _ in range(30):
        actions = {pid: np.random.randint(0, 4) for pid in range(1, 5)}
        obs, _, dones, _ = env.step(actions)
        if all(dones.values()):
            env.reset()
    
    # Analyze observations
    print("\n🔍 Analyzing Player 1 observations:")
    obs_data = visualizer.analyze_observations(player_id=1, display_components=False)
    
    # Test action masking
    print("\n🚫 Action masking test:")
    for pid in range(1, 5):
        mask = env.get_action_mask(pid)
        blocked_actions = [i for i, allowed in enumerate(mask) if not allowed]
        print(f"  Player {pid}: Blocked actions = {blocked_actions}")
    
    print("✅ Visualization demo completed!")

def demo_training(steps=1000):
    """Demonstrate training capabilities"""
    print(f"\n🧠 === Training Demo ({steps} steps) ===")
    
    from train_colab import PaperIOTrainer
    
    # Create trainer
    trainer = PaperIOTrainer(config_path="config.yaml", use_tensorboard=False)
    print(f"✅ Trainer created with config: {trainer.config.get('batch_size')} batch size")
    
    # Short training demonstration
    print(f"🏋️ Starting training for {steps} steps...")
    start_time = time.time()
    
    # Collect some experience
    trainer.collect_experience(num_steps=steps)
    
    # Check what was collected
    total_experiences = sum(len(trainer.experience_buffer[pid]) for pid in range(1, 5))
    print(f"✅ Collected {total_experiences} total experiences")
    
    # Show metrics
    for pid in range(1, 5):
        stats = trainer.metrics[pid].get_stats()
        if stats['episodes'] > 0:
            print(f"  Player {pid}: {stats['episodes']} episodes, "
                  f"avg reward: {stats['mean_reward']:.2f}, "
                  f"avg score: {stats['mean_score']:.1f}")
    
    training_time = time.time() - start_time
    print(f"✅ Training demo completed in {training_time:.1f}s")
    
    return trainer

def demo_model_operations(trainer):
    """Demonstrate model save/load"""
    print("\n💾 === Model Operations Demo ===")
    
    # Save models
    trainer.save_models("demo_checkpoint")
    print("✅ Models saved")
    
    # Test loading
    trainer.load_models("demo_checkpoint")
    print("✅ Models loaded")
    
    # Run evaluation
    print("🎯 Running evaluation...")
    eval_results = trainer.evaluate(num_episodes=3)
    
    # Show results
    for pid, metrics in eval_results.items():
        win_rate = metrics['wins'] / 3 * 100
        avg_score = np.mean(metrics['scores'])
        print(f"  Player {pid}: {metrics['wins']}/3 wins ({win_rate:.1f}%), "
              f"avg score: {avg_score:.1f}")
    
    print("✅ Model operations demo completed!")

def create_summary_report(env, trainer):
    """Create a summary report"""
    print("\n📋 === Summary Report ===")
    
    print("🏗️ Implementation Features:")
    print("  ✅ 84-dimension observations (Unity MyAgent.cs compatible)")
    print("  ✅ Exact reward system replication")
    print("  ✅ 4-player competitive environment")
    print("  ✅ Action masking (prevents invalid moves)")
    print("  ✅ PPO training algorithm")
    print("  ✅ Real-time visualization")
    print("  ✅ Model save/load functionality")
    print("  ✅ Google Colab compatibility")
    
    print("\n📊 Environment Status:")
    print(f"  Map Size: {env.MAP_SIZE}x{env.MAP_SIZE}")
    print(f"  Players: {env.NUM_PLAYERS}")
    print(f"  Current Scores: {dict(env.scores)}")
    alive_count = sum(1 for p in env.players.values() if p.is_alive)
    print(f"  Alive Players: {alive_count}/{env.NUM_PLAYERS}")
    
    print("\n🎯 Training Configuration:")
    config = trainer.config
    print(f"  Batch Size: {config.get('batch_size')}")
    print(f"  Learning Rate: {config.get('learning_rate')}")
    print(f"  Hidden Units: {config.get('hidden_units')}")
    print(f"  Network Layers: {config.get('num_layers')}")
    
    print("\n🚀 Next Steps:")
    print("  1. Run full training: trainer.train(total_steps=100000)")
    print("  2. Monitor with TensorBoard: trainer = PaperIOTrainer(use_tensorboard=True)")
    print("  3. Create visualizations: visualizer.render_game_state(show_observations=True)")
    print("  4. Adjust hyperparameters in config.yaml")
    print("  5. Scale up training steps for better performance")

def main():
    """Main demo function"""
    print("🎯 Paper.io ML-Agents Colab Demo")
    print("=" * 50)
    
    is_colab = check_environment()
    if is_colab:
        print("🌐 Running in Google Colab")
        install_dependencies()
    else:
        print("💻 Running in local environment")
    
    try:
        # Demo environment
        env = demo_environment()
        
        # Demo visualization
        demo_visualization(env)
        
        # Demo training (short version)
        trainer = demo_training(steps=500)
        
        # Demo model operations
        demo_model_operations(trainer)
        
        # Create summary
        create_summary_report(env, trainer)
        
        print("\n🎉 === Demo Completed Successfully! ===")
        print("\nTo start full training, run:")
        print("python train_colab.py --steps 100000")
        
    except Exception as e:
        print(f"\n❌ Demo failed with error: {e}")
        import traceback
        traceback.print_exc()
        return 1
    
    return 0

if __name__ == "__main__":
    exit_code = main()
    sys.exit(exit_code)