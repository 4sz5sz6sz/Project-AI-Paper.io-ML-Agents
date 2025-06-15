# Paper.io ML-Agents 사용 가이드

## 폴더 구조

```
results/
└── shared_models/          # 팀 공유용 학습된 모델들
    └── run6_good_model/     # 예시: 좋은 성능의 모델
        ├── Basic.onnx       # 학습된 AI 모델
        ├── configuration.yaml
        └── run_logs/
```

---

## 빠른 시작

### 1. AI 모델 사용하기 (ONNX)

1. Unity에서 **MyAgent GameObject** 선택
2. **Behavior Parameters** 컴포넌트에서:
   - **Model**: `results/shared_models/run6_good_model/Basic.onnx` 드래그
   - **Behavior Type**: `Default` (자동 전환)
3. **Play 버튼** 클릭 → AI가 자동으로 플레이!

### 2. 수동 제어 (테스트용)

1. **Model 필드를 비우기**
2. **Play 버튼** 클릭
3. **IJKL 키**로 수동 조작:
   - `I`: 위
   - `K`: 아래
   - `J`: 왼쪽
   - `L`: 오른쪽

---

## 학습 및 실험

### **새로운 모델 학습**

```bash
# conda 환경 활성화
conda activate mlagents

# 학습 시작 (로컬 폴더에)
mlagents-learn basic.yaml --run-id=run999

# Unity Play 버튼 클릭
```

### **좋은 모델 공유하기**

1. **로컬에서 좋은 성능 발견** 시:

   ```bash
   # 로컬 results → Git results로 복사
   cp -r ~/ml-agents/result/run999/ results/shared_models/run999/
   ```

   또는,

   results/shared_models 에 수동 복사.

2. **Git에 추가**:
   ```bash
   git add results/shared_models/run999/
   git commit -m "feat: Add run999 model - survival 90%"
   ```

---

## 자동 전환 시스템

ML-Agents는 **자동으로 모드를 전환**합니다:

```
conda 켜져있을 때    → 실시간 학습
ONNX 모델 있을 때    → AI 플레이
둘 다 없을 때       → 수동 제어 (IJKL)
```

---

## 현재 사용 가능한 모델

| 모델명            | 성능 | 특징          | 사용법            |
| ----------------- | ---- | ------------- | ----------------- |
| `run6_good_model` | 보통 | 안정적 플레이 | `Basic.onnx` 사용 |

---

## 문제 해결

### **AI가 동작하지 않을 때:**

- ONNX 파일이 올바르게 할당되었는지 확인
- Behavior Type이 `Default`인지 확인
- Console에서 에러 메시지 확인

### **학습이 연결되지 않을 때:**

- conda 환경이 활성화되었는지 확인
- `mlagents-learn` 명령이 실행 중인지 확인
- Unity와 Python이 같은 네트워크에 있는지 확인

---

## 팀 규칙

1. **개인 실험**: 로컬에서 자유롭게
2. **좋은 모델만**: `results/shared_models/`에 추가
3. **의미있는 이름**: 성능이나 특징을 포함한 폴더명
4. **커밋 메시지**: 성능 개선 사항 명시

---

_마지막 업데이트: 2025년 6월 15일_
