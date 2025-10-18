# Paper.io ML-Agents Colab Training Environment

## 🎯 개요

Unity ML-Agents Paper.io 프로젝트를 Google Colab에서 학습 가능하도록 Python으로 완전히 재구현한 환경입니다. 원본 Unity 에이전트(`MyAgent.cs`)의 관찰 시스템과 보상 구조를 정확히 복제하여 동일한 학습 성능을 제공합니다.

## 🚀 빠른 시작 (Google Colab)

### 1. 환경 설정

```python
# Colab에서 저장소 클론
!git clone https://github.com/4sz5sz6sz/Project-AI-Paper.io-ML-Agents.git
%cd Project-AI-Paper.io-ML-Agents/colab_training

# 의존성 설치
!pip install -r requirements.txt

# 추가 시스템 패키지 (필요시)
!apt-get update
!apt-get install -y python3-opengl xvfb
```

### 2. 기본 학습 시작

```python
# 기본 학습 실행
!python train_colab.py --steps 100000

# 또는 Jupyter 노트북에서
from train_colab import PaperIOTrainer

trainer = PaperIOTrainer(config_path="config.yaml")
trainer.train(total_steps=100000)
```

### 3. 실시간 시각화

```python
from visualizer import PaperIOVisualizer
from paper_io_env import PaperIOEnv

env = PaperIOEnv()
visualizer = PaperIOVisualizer(env)

# 게임 상태 시각화
env.reset()
visualizer.render_game_state(show_observations=True, target_player=1)
```

## 📁 파일 구조

```
colab_training/
├── paper_io_env.py       # 메인 게임 환경 (Unity 로직 복제)
├── train_colab.py        # PPO 학습 스크립트
├── visualizer.py         # 실시간 시각화 도구
├── config.yaml          # ML-Agents 호환 설정
├── requirements.txt      # Python 의존성
└── README_colab.md      # 이 문서
```

## 🎮 게임 환경 세부사항

### 환경 파라미터
- **맵 크기**: 100x100
- **플레이어 수**: 4명
- **관찰 차원**: 84차원 (Unity MyAgent.cs와 동일)
- **액션 공간**: 4개 이산 액션 (상, 우, 하, 좌)
- **최대 스텝**: 2000 스텝/에피소드

### 관찰 시스템 (84차원)
Unity `MyAgent.cs`의 `CollectObservations`를 정확히 복제:

1. **Ultra Critical 3x3 관찰** (45차원)
   - 3x3 영역을 5번 반복 관찰
   - 즉각적인 위험 감지에 중점

2. **Critical Proximity 관찰** (9차원)
   - 근접 3x3 영역 상세 분석
   - 생존 핵심 정보 제공

3. **Immediate Danger 관찰** (10차원)
   - 즉시 위험 요소 감지
   - 벽, 궤적 충돌 위험도

4. **Enemy Threat Assessment** (15차원)
   - 적 플레이어 위협 분석
   - 거리, 공격성, 영토 우위 등

5. **Basic Info** (5차원)
   - 위치, 방향, 점수 등 기본 정보

### 보상 시스템
Unity `MyAgent.cs`의 보상 구조를 정확히 복제:

- **벽 충돌**: -80점
- **자기 궤적 충돌**: -100점  
- **적에게 사망**: -15점
- **영역 확장**: +0.1점 × 획득 타일 수
- **안전지대 페널티**: -0.1점/스텝

### 액션 마스킹
- 180도 회전 금지 (Unity `WriteDiscreteActionMask` 복제)
- 벽/궤적 충돌 방지

## 🧠 학습 설정

### PPO 하이퍼파라미터 (Unity 설정과 동일)
```yaml
batch_size: 2048
buffer_size: 20480
learning_rate: 3.0e-4
epsilon: 0.2
lambd: 0.95
num_epoch: 3
gamma: 0.99
hidden_units: 512
num_layers: 3
memory_size: 256 (LSTM)
```

### 네트워크 구조
- **입력**: 84차원 관찰
- **은닉층**: 512 유닛 × 3층
- **메모리**: LSTM (256 차원)
- **출력**: 4차원 정책 + 1차원 가치함수

## 📊 모니터링 및 시각화

### 1. TensorBoard 연동
```python
# 학습 시작 시 자동으로 TensorBoard 로그 생성
trainer = PaperIOTrainer(use_tensorboard=True)
trainer.train(total_steps=500000)

# Colab에서 TensorBoard 실행
%load_ext tensorboard
%tensorboard --logdir=runs
```

### 2. 실시간 게임 상태 시각화
```python
# 게임 상태 렌더링
visualizer.render_game_state(
    show_trails=True,           # 궤적 표시
    show_observations=True,     # 관찰값 시각화
    target_player=1            # 분석 대상 플레이어
)

# 관찰값 상세 분석
visualizer.analyze_observations(player_id=1)
```

### 3. 학습 진행 대시보드
```python
# 학습 통계 대시보드
visualizer.create_training_dashboard(trainer.metrics)
```

### 4. 게임 애니메이션
```python
# 게임 플레이 애니메이션 생성
visualizer.animate_game(
    max_steps=200, 
    save_gif=True, 
    gif_filename='training_demo.gif'
)
```

## 🔧 고급 사용법

### 1. 커스텀 학습 설정
```python
# 커스텀 config로 학습
custom_config = {
    'batch_size': 4096,
    'learning_rate': 1e-4,
    'hidden_units': 1024,
    'max_steps': 1000000
}

trainer = PaperIOTrainer()
trainer.config.update(custom_config)
trainer.train()
```

### 2. 체크포인트 저장/로드
```python
# 학습 중 자동 저장 (50,000 스텝마다)
trainer.train(total_steps=500000, save_interval=50000)

# 체크포인트에서 재개
trainer.load_models("checkpoint_100000")
trainer.train(total_steps=200000)  # 추가 학습
```

### 3. 에이전트 평가
```python
# 훈련된 에이전트 평가
trainer.load_models("final_model")
eval_results = trainer.evaluate(num_episodes=50)

# 결과 분석
for player_id, metrics in eval_results.items():
    print(f"Player {player_id} - Win Rate: {metrics['wins']/50*100:.1f}%")
```

### 4. 멀티 에이전트 대전
```python
# 서로 다른 체크포인트의 에이전트들을 대전시키기
trainer1 = PaperIOTrainer()
trainer1.load_models("checkpoint_100000")

trainer2 = PaperIOTrainer()  
trainer2.load_models("checkpoint_200000")

# 대전 로직 구현...
```

## 🎯 학습 팁

### 1. 학습 안정성
- **배치 크기**: 2048 이상 권장 (4 플레이어 동시 학습)
- **학습률**: 3e-4에서 시작, 필요시 감소
- **에피소드 길이**: 너무 길면 학습이 불안정할 수 있음

### 2. 성능 최적화
```python
# GPU 사용 확인
import torch
print(f"CUDA available: {torch.cuda.is_available()}")

# 멀티프로세싱 환경 (로컬에서)
trainer = PaperIOTrainer()
trainer.config['num_envs'] = 4  # 병렬 환경 수
```

### 3. 하이퍼파라미터 튜닝
```python
# 그리드 서치 예시
learning_rates = [1e-4, 3e-4, 1e-3]
batch_sizes = [1024, 2048, 4096]

for lr in learning_rates:
    for bs in batch_sizes:
        config = {'learning_rate': lr, 'batch_size': bs}
        trainer = PaperIOTrainer()
        trainer.config.update(config)
        trainer.train(total_steps=100000)
        # 결과 기록...
```

## 🐛 트러블슈팅

### 1. 메모리 부족
```python
# 배치 크기 줄이기
trainer.config['batch_size'] = 1024
trainer.config['buffer_size'] = 10240
```

### 2. 학습 속도 개선
```python
# 환경 단순화 (디버깅용)
env = PaperIOEnv()
env.MAX_STEPS = 500  # 에피소드 길이 단축
```

### 3. 시각화 문제 (Colab)
```python
# 백엔드 설정
import matplotlib
matplotlib.use('Agg')  # 헤드리스 모드
```

## 📈 성능 벤치마크

### 예상 학습 성능
- **100k 스텝**: 기본 생존 전략 학습
- **500k 스텝**: 영역 확장 전략 습득  
- **1M 스텝**: 고급 대전 전략 완성
- **5M 스텝**: Unity 원본 성능 달성

### 하드웨어 요구사항
- **최소**: CPU (학습 가능하지만 느림)
- **권장**: GPU (Tesla T4 이상)
- **메모리**: 8GB RAM 이상
- **저장공간**: 1GB (모델 + 로그)

## 🤝 기여 및 확장

### 환경 확장
```python
# 맵 크기 변경
class CustomPaperIOEnv(PaperIOEnv):
    def __init__(self):
        super().__init__()
        self.MAP_SIZE = 200  # 더 큰 맵
```

### 새로운 보상 함수
```python
# 커스텀 보상 추가
def custom_reward_function(self, player_id, action, next_state):
    reward = 0.0
    # 커스텀 로직...
    return reward
```

## 📚 참고 자료

- [Unity ML-Agents 공식 문서](https://github.com/Unity-Technologies/ml-agents)
- [PPO 알고리즘 논문](https://arxiv.org/abs/1707.06347)
- [원본 Unity 프로젝트](https://github.com/4sz5sz6sz/Project-AI-Paper.io-ML-Agents)

## 📄 라이선스

이 프로젝트는 원본 Unity 프로젝트와 동일한 라이선스를 따릅니다.

---

**Paper.io ML-Agents Colab Training Environment**  
*Unity ML-Agents를 Python으로 완전 재구현한 Colab 학습 환경*