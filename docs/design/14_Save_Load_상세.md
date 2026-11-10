# 다크 레기온 — Save / Load 상세

> **문서 역할**: DTO 직렬화, 저장 시점, 파일 레이아웃
> **참조 문서**: 05_기술_아키텍처 §9·§21, 06_전투_코어_상세설계 §17

---

## 1. 원칙

```
Runtime → DTO → JSON → 파일
Runtime 직접 직렬화 금지
```

---

## 2. SaveData

```csharp
[Serializable]
public class SaveData
{
    public int SaveVersion = 1;  // 저장 형식 버전 (`01` D-59). 현재와 다르면 런 저장을 버린다
    public PlayerSaveDTO Player;
    public DeckSaveDTO Deck;
    public MapSaveDTO Map;
    public List<RelicSaveDTO> Relics;
    public MetaSaveDTO Meta;
    public long RngSeed;
    public string StartClass;    // NEC/ELE/BEA/HOL/NEU — 카드 해금(D-07·D-17)·시작 유물(D-06) 기준
    public int Floor;
    public int StageIndex;
    public int Gold;
    public int FusionCharge;     // 0~3, 층 이동 시 유지 (01 D-03)
    public bool FreeFusionUsed;  // 3층 융합 성소 무료 1회 사용 여부 (D-03, 2단계)
}
```

### RelicSaveDTO

```csharp
public class RelicSaveDTO
{
    public string RelicId;
    public bool UsedThisRun;     // OncePerRun 유물(REL_FORBIDDEN_CANDLE, REL_RARE_001)
    public int Counter;          // 런 단위로 누적되는 유물 값. 해당 유물이 없으면 0
}
```

> 턴당 1회 제한(D-04·D-15)은 전투 중 상태라 저장하지 않는다. MVP는 전투 중 저장을 하지 않는다 (§3, `01` D-37).

### PlayerSaveDTO

```csharp
public class PlayerSaveDTO
{
    public int CurrentHp;
    public int MaxHp;
    public int CurrentCrystal;
    public int MaxCrystal;
}
```

### DeckSaveDTO

```csharp
public class DeckSaveDTO
{
    public List<CardSaveDTO> DrawPile;
    public List<CardSaveDTO> Hand;
    public List<CardSaveDTO> DiscardPile;
    public List<CardSaveDTO> ExhaustPile;
}

public class CardSaveDTO
{
    public string CardId;        // CardData.Id
    public int UpgradeLevel;     // 0 = 기본, 1 = 강화 (`25` §2, `01` D-31)
    public bool FromFusion;      // 융합으로 만든 카드 (REL_BOSS_3F, `01` D-38)
}
```

### MapSaveDTO

```csharp
public class MapSaveDTO
{
    public string CurrentNodeId;
    public List<string> VisitedNodeIds;
    public List<MapEdgeDTO> Edges;   // 간선 직렬화
}
```

### MetaSaveDTO

```csharp
public class MetaSaveDTO
{
    public int SaveVersion = 1;  // 메타 저장 형식 버전 (`01` D-59). 낮으면 변환, 높으면 읽지 않고 알림
    public List<string> UnlockedClasses;
    public List<string> PermanentUnlocks;  // 심연의 가마솥 — Post-MVP (`29` §8). MVP에서는 빈 목록
    public int MetaSoul;
    public int MetaSigil;
    public List<string> UnlockedCards;
    public List<string> UnlockedRelics;
    public Dictionary<string, bool> AchievementFlags;
    public bool TutorialDone;    // 1층 노드 1 희생 안내 전투 완료 여부 (`01` D-32)
    public MetaStatsDTO Stats;   // 언락 조건·업적 판정용 누적 기록 (`01` D-53, `41` §4.1). 2단계
}

public class MetaStatsDTO
{
    public int[] FloorClearCount;            // 길이 5, 층별 보스 처치 런 수
    public int TotalSacrifice;
    public int TotalFusion;
    public int TotalExecuteKill;
    public int TotalBurnDamage;
    public int TotalPoisonDamage;
    public Dictionary<string, int> ClassWins;
    public string LastClass;
}
```

> 메타 진행 상세: [26_메타_언락_설계.md](26_메타_언락_설계.md)

---

## 3. 전투 중 종료 (`01` D-37)

MVP(1·2단계)는 전투 상태를 저장하지 않는다. 전투 중에 게임을 끄면 마지막 저장(직전 노드 클리어 시점)이 남아 있고, 이어하면 **그 전투 노드를 처음부터 같은 시드로** 다시 시작한다. 덱 순서·적 의도·보상이 처음과 똑같다.

노드 시드는 런 시드와 위치로 만든다. 그 노드의 전투·보상 난수는 모두 이 시드에서 시작한다 (`06` §15).

```csharp
// SaveData.RngSeed, Floor, StageIndex에서 계산. 저장하지 않는다
int NodeSeed = unchecked((int)(RngSeed * 31 + Floor) * 31 + StageIndex);
```

### BattleSnapshotDTO (Post-MVP·디버그 전용)

아래 구조는 디버그 재현(`06` §17)과 Post-MVP 전투 중 저장용이다. MVP 빌드는 파일로 저장하지 않는다.

```csharp
public class BattleSnapshotDTO
{
    public int TurnCount;
    public long RngSeed;
    public List<UnitSnapshotDTO> Allies;
    public List<UnitSnapshotDTO> Enemies;
    public DeckSaveDTO Deck;
    public List<string> MainQueueLog;   // 디버그
}
```

---

## 4. 저장 시점

| 시점 | 파일 |
|------|------|
| 스테이지 클리어 | save_slot_N.json |
| 게임 종료 (전투 밖) | save_slot_0.json |
| 런 종료 | meta.json 갱신 |

---

## 5. 파일 구조

```
/SaveData/
  save_slot_0.json
  save_slot_1.json
  meta.json
```

---

*문서 목록: [00_문서_인덱스.md](00_문서_인덱스.md)*
