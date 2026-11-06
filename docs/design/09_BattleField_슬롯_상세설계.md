# 다크 레기온 — BattleField 슬롯 상세설계

> **문서 역할**: 아군 3 / 적 5 슬롯 배치·충돌·소환·사망·Wave 전환
> **참조 문서**: 04_시스템_설계.md §2, 06_전투_코어_상세설계.md, 13_희생_융합_시스템_상세.md, 03_MVP_전층_스펙.md §2
> **구현 시점**: Phase 1-A(RuntimeBattleField) + Phase 4-B(Summon) 전 검토 필수

---

## 1. 필드 구조

```
아군 AllySlots[3]:   Front | Middle | Back
적군 EnemySlots[5]:  Front | Middle | Back | Extra1 | Extra2  (MVP: Extra는 소환·다수 적용)
```

| 팀 | 슬롯 수 | MVP 사용 |
|----|---------|----------|
| Player | 3 | 항상 3 (몬스터+토큰 합산) |
| Enemy | 5 | 일반 1~3체, 보스 1+졸 2~4 |

```csharp
public class RuntimeBattleField
{
    public RuntimeSlot[] AllySlots = new RuntimeSlot[3];
    public RuntimeSlot[] EnemySlots = new RuntimeSlot[5];
}
```

---

## 2. 배치(Place) 기본 규칙

### 2.1 빈 슬롯

- `SummonAction` / `EncounterFactory`는 **지정 Position**에 배치
- Position 미지정 시: 아군 **Back → Middle → Front** 첫 빈 슬롯, 적은 **Front 우선**

### 2.2 슬롯 점유

- 1슬롯 = 1 `RuntimeUnit` (Monster 또는 Summon)
- `RuntimePlayer`는 슬롯 없음 (플레이어 HP는 별도)

---

## 3. 아군 슬롯 가득 참 — MVP 확정

| 상황 | 동작 | 이유 |
|------|------|------|
| 몬스터 카드 소환, 빈 슬롯 있음 | 지정/자동 Position에 배치 | 기본 |
| 몬스터 카드 소환, **3슬롯 전부 점유** | **소환 거부(Reject)** — 카드 Cost 소모 없음, 패는 Hand 유지 | 덱 낭비·실수 방지 |
| 스킬/액션 토큰 소환, 슬롯 가득 | **Front 슬롯 유닛 1체 선택 UI** → 선택 유닛 Death Queue (희생 아님, Crystal 없음) | 토큰은 임시·교체 허용 |
| 보스 스킬 다수 소환 (적) | 적 Extra 슬롯 사용, 가득 시 **가장 오래된 Extra 제거** (AI) | 적 필드 5칸 활용 |

> **희생**은 슬롯 비우기 수단이지만 **플레이어 의도** 필요 → 자동 교체와 분리.

### 3.1 검증 흐름 (SummonAction)

```
ValidateSummon(cardOrTemplate, position)
  → if Ally full && type==MonsterCard → ValidationResult.Reject(FullField)
  → if Ally full && type==Token → RequestReplaceTarget UI (BattleState.WaitingReplace)
  → else Place
```

### 3.2 UI 상태

```csharp
public enum BattleState
{
    // ...
    WaitingReplace,  // 토큰 소환 교체 대상 선택
}
```

- MainPhase에서만 `WaitingReplace` 진입
- 타임아웃 없음 (MVP) — 선택 필수

---

## 4. 위치 이동(Swap / Shift)

| 행동 | 규칙 |
|------|------|
| 카드/스킬 "위치 변경" | Main Queue `MoveUnitAction` — 인접 빈 슬롯으로만 (MVP) |
| 사망 후 빈 슬롯 | **자동 당김 없음** (MVP) — Front 빈 채로 전투 가능, 타겟팅은 `Front→Middle→Back` 순서로 처리 |

---

## 5. 사망·슬롯 해제

```
DeathAction.Execute
  → OnDeath Trigger (유닛 아직 슬롯에 있음)
  → 슬롯 Occupant = null
  → TriggerBus.UnregisterAllOwnedBy(deadUnit)
```

- `IsDead=true` 동안 슬롯 **점유 유지** (06_전투_코어_상세설계.md 정본)
- DeathAction **끝**에서만 Occupant 제거

---

## 6. Wave 2+ 전환 (`03_MVP_전층_스펙.md` §2, `04_시스템_설계.md` §11)

| 항목 | 규칙 |
|------|------|
| 아군 Monster 슬롯 | **유지** (HP·상태·위치) |
| 아군 Summon(토큰) | **전부 제거** — DeathAction 없이 `RemoveTokenAction` (보상·트리거 없음) |
| 적 슬롯 | 전부 클리어 후 새 Wave Spawn |
| Crystal / 패 | TurnManager 규칙 (리셋·Discard) |

```csharp
public void OnWaveStart(BattleContext ctx)
{
    foreach (var slot in AllySlots)
        if (slot.Occupant is RuntimeSummon)
            ctx.Queue.Enqueue(new RemoveTokenAction(slot.Occupant));
    ClearEnemySlots();
    EncounterFactory.SpawnWave(nextWave, ctx);
}
```

---

## 7. 타겟팅과 빈 슬롯

- `FrontEnemy`: 첫 비어있지 않은 Front → Middle → Back
- Assassin 후열: Back 우선 (12_AI_상세설계.md)
- 빈 Front만 있고 Middle에 유닛: Middle이 "Front 라인" 공격 대상 (MVP 단순 규칙)

---

## 8. 구현 체크리스트

```
□ Monster 소환 FullField → Reject, Cost 미소모
□ Token 소환 FullField → WaitingReplace → Replace 후 배치
□ Death 후 Occupant null + Trigger 해제
□ WaveStart 토큰만 제거, 몬스터 유지
□ 적 5슬롯 스폰 표 03_MVP_전층_스펙.md와 일치
```

---

*문서 목록: [00_문서_인덱스.md](00_문서_인덱스.md)*
