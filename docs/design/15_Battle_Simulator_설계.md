# 다크 레기온 — Battle Simulator 설계

> **문서 역할**: Headless 전투 시뮬레이션, 밸런스 탐지, JSON 리포트
> **참조 문서**: 16_밸런스_규칙.md §8, 06_전투_코어_상세설계.md, 17_카드_데이터베이스.md §7

---

## 1. 구조

```
BattleSimulator
 ├─ BattleRunner
 ├─ AIController (Greedy / Random)
 ├─ ResultCollector
 └─ BalanceAnalyzer
```

---

## 2. BattleRunner

- `BattleManager`를 **Headless** 실행 (Presentation 비활성)
- `BattleRandom` 시드 고정 → 재현 가능
- 기본 **1000회** 반복 / 덱·적 ID per run
- 한도: MaxQueue 500, MaxTrigger 32 (06_전투_코어_상세설계.md)

```csharp
public class BattleRunner
{
    public BattleResult RunSingle(int seed, DeckDefinition deck, EncounterDefinition enc);
    public List<BattleResult> RunBatch(int iterations, int baseSeed);
}
```

---

## 3. AIController (플레이어)

| 타입 | 용도 |
|------|------|
| GreedyAI | 턴당 PP/Crystal 효율 최대 카드 |
| RandomAI | 하한선·분산 테스트 |

우선순위: 생존(HP<30% Shield) → 킬 가능 → Lv3 소환 → 희생(+Crystal) → 기본 공격 카드

---

## 4. ResultCollector

| 필드 | 설명 |
|------|------|
| win_rate | 승률 |
| avg_turns | 평균 턴 |
| avg_remaining_hp | 승리 시 잔 HP |
| card_usage | 카드별 사용·기여 피해 |
| sacrifice_count / fusion_count | 경제 지표 |
| status_damage | Burn/Poison 총량 |
| death_causes | 사망 원인 분포 |

---

## 5. BalanceAnalyzer 탐지

| 플래그 | 조건 |
|--------|------|
| OTK | 승리 & turn <= 1 |
| INFINITE_LOOP | Queue 한도 초과 |
| INFINITE_DRAW | 덱+Discard 순환 3회 초과 |
| OVERPOWERED_CARD | 사용률 > 80% |
| FAST_KILL_META | avg_turn < 2 |
| STALEMATE | avg_turn > 10 |
| WIN_RATE_LOW / HIGH | < 20% or > 80% |

---

## 6. JSON 출력

```json
{
  "deck_id": "NEC_TEST_001",
  "enemy_id": "ENE_1F_BOS_001",
  "iterations": 1000,
  "win_rate": 0.52,
  "avg_turns": 5.3,
  "avg_remaining_hp": 14.2,
  "otk_detected": false,
  "infinite_loop_detected": false,
  "overpowered_cards": [],
  "balance_flags": []
}
```

---

## 7. 우선 검증 덱 (17_카드_데이터베이스.md §7)

- ELE_MON_011, BEA 광폭한 곰, HOL 발키리, NEC 죽음의 소용돌이

---

*이전 문서: [22_보스_패턴_설계.md](22_보스_패턴_설계.md)*
*다음 문서: [14_Save_Load_상세.md](14_Save_Load_상세.md)*
