# Paper.io ML-Agents 에피소드 관리 시스템

Paper.io 게임에서 ML-Agents 에피소드를 효과적으로 관리하기 위한 가이드입니다.

## 개요

### 핵심 철학: 원작 Paper.io와 동일한 부활 시스템

Paper.io의 에피소드 관리는 **원작 게임과 동일한 철학**으로 설계되었습니다:

- 🎯 **개별 에피소드**: 각 Agent는 자신만의 에피소드를 독립적으로 관리
- ⚡ **즉시 부활**: Agent가 죽으면 → 그 Agent만 즉시 부활하고 새 에피소드 시작
- 🎮 **게임 연속성**: 다른 Agent들은 계속 진행 (게임 전체 리셋 없음)
- 🏆 **자연스러운 불공평**: 원작처럼 플레이어 간 불균형 허용 (이게 게임의 재미!)
- 🔄 **에피소드 ≠ 게임**: 에피소드 종료와 게임 리셋은 별개

### 원작 Paper.io의 동작 방식

1. **플레이어가 죽으면** → 그 플레이어만 즉시 부활
2. **다른 플레이어들** → 계속 진행, 아무 영향 없음
3. **불공평해도 상관없음** → 늦게 들어온 플레이어는 불리하지만 그게 게임
4. **경쟁은 계속** → 누군가 죽어도 게임은 멈추지 않음

우리 ML-Agents 시스템도 정확히 이와 같이 동작합니다!

## 📁 구현된 파일들

### 1. EpisodeManager.cs

**경로**: `Assets/Scripts/EpisodeManager.cs`
**역할**: 원작 Paper.io와 동일한 에피소드 관리

**핵심 기능**:

- ✅ **개별 Agent 추적**: 각 Agent의 에피소드 시간/스텝 독립적 관리
- ✅ **즉시 부활 처리**: `OnAgentDeath()` → 죽은 Agent만 부활
- ✅ **개별 종료 조건**: 점수/시간/스텝 제한을 Agent별로 체크
- ✅ **게임 지속성**: 누군가 죽어도 게임은 멈추지 않음

### 2. MyAgent.cs (수정됨)

**경로**: `Assets/Scripts/MyAgent.cs`
**역할**: Agent와 EpisodeManager 통합

**추가된 기능**:

- ✅ `OnActionReceived()` 메서드로 액션 처리
- ✅ EpisodeManager 스텝 카운팅 (`OnAgentStep()`)
- ✅ 사망 시 EpisodeManager 알림 (`OnAgentDeath()`)
- ✅ 무진전 감지 시 자동 에피소드 종료

## 🛠️ Unity 설정 방법

### 1단계: EpisodeManager GameObject 생성

```
1. Unity Hierarchy에서 우클릭
2. "Create Empty" 선택
3. GameObject 이름을 "EpisodeManager"로 변경
4. EpisodeManager.cs 스크립트 추가
```

### 2단계: Inspector 설정

#### 기본 권장 설정

```
EpisodeManager Component:

[에피소드 종료 조건]
• Max Score: 1000 (목표 점수 도달 시 해당 Agent 에피소드 종료)
• Max Episode Time: 300 (5분, 0이면 무제한)
• Max Steps: 10000 (최대 스텝, 0이면 무제한)

[부활 설정]
• Respawn Delay: 0.1 (부활 대기시간 0.1초)
```

#### 학습용 설정 (빠른 순환)

```
• Max Score: 500 (낮은 목표)
• Max Episode Time: 120 (2분)
• Max Steps: 5000 (적은 스텝)
• Respawn Delay: 0.0 (즉시 부활)
```

#### 게임플레이용 설정 (자연스러운 플레이)

```
• Max Score: 2000 (높은 목표)
• Max Episode Time: 0 (무제한)
• Max Steps: 0 (무제한)
• Respawn Delay: 0.5 (약간의 딜레이)
```

## 🎮 시스템 동작 방식

### 시나리오 1: Agent 사망

```
1. Agent A가 죽음 → MyAgent.NotifyDeath() 호출
2. EpisodeManager.OnAgentDeath(Agent A) 실행
3. Agent A만 EndEpisode() → 개별 에피소드 종료
4. 0.1초 후 Agent A 부활 → 새 에피소드 시작
5. Agent B, C, D는 계속 플레이 (아무 영향 없음)
```

### 시나리오 2: 목표 점수 달성

```
1. Agent B가 1000점 달성
2. EpisodeManager가 감지 → Agent B만 EndEpisode()
3. Agent B 새 에피소드 시작 (점수 0부터)
4. 다른 Agent들은 계속 진행
```

### 시나리오 3: 시간/스텝 제한

```
1. Agent C가 5분 또는 10000스텝 도달
2. EpisodeManager가 감지 → Agent C만 EndEpisode()
3. Agent C 새 에피소드 시작
4. 다른 Agent들은 영향 없음
```

## 🔍 핵심 메서드 설명

### EpisodeManager.cs

```csharp
// Agent 사망 처리 - 죽은 Agent만 부활
OnAgentDeath(MyAgent agent)

// 개별 Agent 스텝 카운팅
OnAgentStep(int playerId)

// 새 Agent 등록
RegisterAgent(MyAgent agent)

// 개별 에피소드 조건 체크 (Update에서 실행)
CheckIndividualEpisodeConditions()
```

### MyAgent.cs

```csharp
// ML-Agents 액션 처리 + EpisodeManager 통합
OnActionReceived(ActionBuffers actions)

// 사망 시 EpisodeManager에 알림
NotifyDeath() // SendMessage("OnAgentDeath", this) 호출
```

## 📊 실시간 모니터링

### 콘솔 로그 확인

```
✅ "[EpisodeManager] Player 1 사망 - 즉시 개별 부활 처리"
✅ "[EpisodeManager] Player 2가 목표 점수 1000 도달 - 에피소드 종료"
✅ "[EpisodeManager] Player 3 시간 제한 300초 도달 - 에피소드 종료"
✅ "[MyAgent] Player 1: 1000 스텝 동안 진전 없음. 에피소드 종료."
```

### 런타임 통계

```csharp
// 특정 Agent 상태 확인
EpisodeManager.Instance.GetAgentEpisodeStats(playerId);
// 결과: "Player 1: 시간 45.2s, 스텝 892"

// 모든 Agent 상태
EpisodeManager.Instance.GetAllAgentsStats();
```

## ⚠️ 중요 사항

### 1. GameObject 이름

EpisodeManager GameObject의 이름이 정확히 **"EpisodeManager"**이어야 합니다.
(`GameObject.Find("EpisodeManager")` 방식 사용)

### 2. 안전한 참조

MyAgent에서 EpisodeManager를 찾지 못해도 오류가 발생하지 않도록 안전장치가 구현되어 있습니다.

### 3. 게임 철학 유지

- **불공평 허용**: Agent 간 불균형은 자연스러운 현상
- **즉시 부활**: 죽으면 바로 부활 (대기시간 최소화)
- **개별 관리**: 각자의 에피소드는 각자가 책임

## 🚀 다음 단계

### 1. Unity 테스트

```
1. EpisodeManager GameObject 생성 및 설정
2. 게임 플레이 후 로그 확인
3. Agent 사망/부활 동작 검증
4. 개별 에피소드 종료 조건 테스트
```

### 2. 파라미터 튜닝

```
• maxScore: 게임 난이도에 맞게 조정
• maxEpisodeTime: 학습 속도와 게임 재미의 균형
• maxSteps: 무한 루프 방지용 안전장치
• respawnDelay: 부활 속도 조절
```

### 3. 추가 개선사항

```
• 실시간 통계 UI 추가
• Agent별 성과 분석 기능
• 동적 난이도 조절 시스템
• 더 정교한 진전 감지 알고리즘
```

## 🎯 결론

이제 Paper.io ML-Agents는 **원작 게임과 동일한 철학**으로 에피소드를 관리합니다:

- ✅ **자연스러운 불공평**: 플레이어 간 실력차/타이밍차 허용
- ✅ **즉시 부활**: 죽으면 바로 부활해서 게임 재개
- ✅ **게임 지속성**: 누군가 죽어도 게임은 멈추지 않음
- ✅ **개별 에피소드**: 각 Agent는 독립적인 학습/플레이 진행

이것이 바로 **진짜 Paper.io의 재미**입니다! 🎉
