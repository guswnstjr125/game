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
    public string RunId;        // 새 런마다 고유한 ID
    public bool TutorialEnabled;
    public bool TutorialCompleted;
    public RunSavePhase Phase;  // Map, BattleEntry, RewardPending, SettlementPending
    public RewardPendingDTO Reward;
    public SettlementPendingDTO Settlement;
    public PlayerSaveDTO Player;
    public DeckSaveDTO Deck;
    public long NextCardInstanceId; // 새 카드에만 발급. 저장·재개 후 재사용 금지
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
    public bool FreeFusionPending; // 성소에서 받은 무료 융합을 아직 안 씀 (다음 융합 1회 충전 미소모, `01` D-62)
    public List<string> Consumables; // 소모품 ID, 최대 3 (`25` §3, D-62)
    public int ShopRemovalsUsed;     // 런 카드 제거 횟수, 최대 2 (`24` §3)
    public ShopVisitDTO Shop;        // 현재 노드가 상점일 때만. 진열은 노드 시드로 다시 만든다
}
```

`Phase`에 따라 대기 필드는 하나만 채운다. `RewardPendingDTO`는 노드 ID, 보상 출처, 후보 ID의 순서, 선택 단계·항목 번호(카드 건너뛰기 포함), 미적용 비용·효과를 기록한다. `SettlementPendingDTO`는 승리·패배·포기, 도달 위치, 클리어한 층, 지급 내역과 업적 판정에 쓰는 런 집계를 기록한다. `SettlementReceiptDTO`는 해당 런의 결과와 실제 지급 내역·새 업적을 기록한다.

### ShopVisitDTO

```csharp
public class ShopVisitDTO
{
    public List<int> PurchasedSlots;   // 산 칸 번호 (카드 0~4, 유물 5, 소모품 6)
    public int RefreshCount;           // 이번 방문 새로고침 횟수 (첫 1회 무료)
}
```

상점 구매·새로고침·제거는 할 때마다 런을 저장한다. 상점에서 껐다가 이어하면 노드 시드 + 새로고침 횟수로 진열을 다시 만들고, 산 칸은 판매 완료로 보인다 (`01` D-62).

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
    public long InstanceId;     // 런 안에서 카드 개체마다 고유. 재개 후에도 동일
    public string CardId;        // CardData.Id
    public int UpgradeLevel;     // 0 = 기본, 1 = 강화 (`25` §2, `01` D-31)
    public bool FromFusion;      // 융합으로 만든 카드 (REL_BOSS_3F, `01` D-38)
}
```

런 덱의 카드 개체 ID는 전투에 들어갈 때 보존한다. 융합의 재료 두 카드와 결과를 개체 ID로 연결한다. 전투 승리 시 두 재료 개체를 제거하고 결과 개체를 `FromFusion=true`로 보상 대기 저장에 포함한다. 저장 전 종료하면 전투 진입 기록으로, 저장 후 종료하면 수정된 덱으로 복원한다. 전투 중 Exhaust는 런 덱 제거로 기록하지 않는다.

### MapSaveDTO

```csharp
public class MapSaveDTO
{
    public string CurrentNodeId;
    public List<string> VisitedNodeIds;
    public Dictionary<string, string> NodeEventIds; // 이벤트 노드 ID → 나온 이벤트 ID (`24` §2.9, `01` D-69)
    // 분기 맵 간선(Edges)은 Post-MVP (`01` D-11). MVP는 층당 9노드 단일 경로라 저장하지 않는다
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
    public string LastSettledRunId; // 단일 런 슬롯의 마지막 정산 ID
    public SettlementReceiptDTO LastSettlement; // 결과 화면에 표시할 확정 내역
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

새 런은 첫 노드에 진입하기 전에 초기 덱·HP·골드·위치·런 시드와 `TutorialEnabled = !Meta.TutorialDone`을 저장한다. 각 전투 진입 직전에 해당 노드와 런 상태를 기록한다. 전투 중 종료 후에는 이 기록으로 노드를 처음부터 시작한다. 앱이 백그라운드로 전환되어도 진행 중인 전투 상태로 진입 기록을 덮어쓰지 않는다.

안내 전투의 승리 또는 전투 패배가 기록되면 `TutorialCompleted`를 같은 런 저장에 기록한다. 전투 종료 전 앱 종료나 명시적 런 포기는 완료로 보지 않는다. 런 저장이 성공한 다음 `meta.json`의 `TutorialDone`을 true로 저장한다. 복구 시에도 런에 완료가 기록되어 있으면 true를 반영한다. 이미 true면 그대로 유지한다.

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

## 4. 저장·복구 상태 (D-67)

| 상태 | 확정 시점과 저장 내용 | 재시작 시 |
|---|---|---|
| 맵 | 새 런 생성 또는 노드 완료 시 런과 다음 위치 저장 | 저장된 맵을 연다 |
| 전투 진입 | 전투를 열기 전에 노드·진입 상태 저장. 이벤트 강제 전투는 선택 및 진입 전 효과까지 함께 저장 | 해당 전투를 같은 상태와 노드 시드로 처음부터 시작 |
| 보상 대기 | 승리 후 런 상태, 최종 보스라면 승리 판정, 노드 ID, 고정된 후보, 선택 단계와 현재 선택, 미지급 내역을 저장한 뒤 화면 표시 | 같은 보상 화면과 선택 상태 복구 |
| 정산 대기 | 최종 보상 확정, 패배 또는 포기 시 결과와 층별 지급 내역·통계·업적 판정 입력을 저장 | 런 ID로 정산을 재개하고 결과 화면을 복구 |

- 골드·자동 유물·선택 유물·카드의 지급은 보상 화면의 마지막 확인에서 확정한다. 카드 건너뛰기도 선택으로 저장한다. 지급과 다음 위치 또는 정산 대기를 같은 런 파일 교체로 확정한다. 화면에서 같은 확인 입력이 다시 와도 보상 대기 상태가 이미 끝났으면 처리하지 않는다.
- 이벤트에서 선택 보상이 이어지면 이벤트 선택·추첨 결과·후보와 아직 적용할 비용 및 효과를 보상 대기 상태에 저장한다. 마지막 확인에서 비용·효과·보상·노드 진행을 함께 확정한다. 강제 전투로 이어지는 비용·효과는 해당 전투의 진입 기록에 포함한다. HP가 0 이하가 되는 이벤트는 D-51에 따라 패배 정산으로 전환한다.
- 정산은 먼저 정산 대기 상태를 저장한다. `LastSettledRunId`가 현재 ID와 다르면 D-37의 층별 화폐와 적용 단계의 업적·누적 통계를 반영하고 같은 메타 파일 교체로 해당 ID를 기록한다. ID가 같으면 저장된 결과 화면만 복구한다. 결과 표시용 지급·업적 내역도 정산 ID와 함께 메타에 저장한다.
- 정산 중에는 새 런을 시작할 수 없다. 결과 화면에서 타이틀로 돌아갈 때 런 파일을 삭제한다. 삭제 실패 시 새 런 시작을 열지 않고 정리 재시도를 제공한다. 단일 런 슬롯이므로 최근 정산 ID 한 개를 유지한다.

---

## 5. 파일 구조와 쓰기 실패

```
/SaveData/
  save_slot_0.json   # 진행 중인 런 또는 정산 대기
  meta.json         # 메타와 마지막 정산 기록
```

각 파일은 같은 디렉터리의 임시 파일에 완전한 내용을 쓴 뒤 닫고, 한 번의 파일 교체로 확정한다. 교체 실패 시 이전 정상 파일을 유지하고 현재 화면에서 재시도를 제공한다. 저장이 성공한 뒤 화면을 전환한다. 앱 시작 시 확정 파일만 읽고 남은 임시 파일을 정리한다. 두 파일을 동시에 교체해야 하는 정산은 §4의 순서와 런 ID로 복구한다.

---

*문서 목록: [00_문서_인덱스.md](00_문서_인덱스.md)*
