# 다크 레기온 — MVP 검증 시나리오 매트릭스

> **문서 역할**: 설계를 구현으로 옮길 때 필요한 최소 검증 시나리오를 사전에 확정
> **참조**: `03_MVP_전층_스펙.md`, `01_MVP_설계_동결_선언.md` §7, `10_카드_런타임_매핑_상세.md`, `16_밸런스_규칙.md`, `09_BattleField_슬롯_상세설계.md`, `25_MVP_보완_시스템_상세.md`, `31_MVP_이슈_초안.md`
> **단계**: 1 = 1단계(1~2층 NEC 빌드)에서 통과해야 함 · 2 = 2단계(MVP 전층)에서 통과해야 함 (`01` D-13·D-26)

---

## 1. 전투 코어

| ID | 단계 | 시나리오 | 기대 결과 | 기준 문서 |
|---|---|---|---|---|
| CORE-01 | 1 | 기본 공격 10턴 연속 | 턴/큐/승패 상태 불일치 없음 | 06_전투_코어_상세설계.md |
| CORE-02 | 1 | 사망 트리거 연쇄 | Death 처리 후 슬롯/트리거 해제 정상 | 06_전투_코어_상세설계.md, 07_TriggerBus_상세설계.md, 09_BattleField_슬롯_상세설계.md |
| CORE-03 | 1 | Wave 전환 | 토큰 제거, 몬스터 유지, 패/Crystal 리셋, 중간 보상 없음 | 03_MVP_전층_스펙.md, 09_BattleField_슬롯_상세설계.md |
| CORE-04 | 1 | 노드 30턴 초과 | 패배 처리 | 03_MVP_전층_스펙.md |
| DMG-01 | 1 | D-01 피해 공식 픽스처 (ATK8 → Front 뼈 병사 6, → Front 역병 쥐 3, 해골 Weak → Front DEF2 Vuln 2, 최소 1) | 모두 일치 | 01_MVP_설계_동결_선언.md D-01, 06_전투_코어_상세설계.md §11 |
| DMG-02 | 1 | Burn/Poison 틱 | DEF·Weak·Vulnerable 무시, Shield 적용 | 01 D-01, 11_상태이상_상세설계.md |
| CMD-01 | 1 | 커맨드 공격/방어 | 기본값 공격(Front), 대상 변경 가능, 방어 시 Shield = 3 + 현재 DEF, Bind·Stun이면 선택 불가 | 01 D-27 |
| INT-01 | 1 | 적 의도 공개 | 플레이어 턴 시작에 종류 아이콘만 표시, Enemy Phase에 확정된 행동 실행, 대상 사망 시 폴백 | 01 D-28 |
| HIT-01 | 1 | 플레이어 피격 | 아군 있으면 단일 공격은 플레이어 미피격, 광역은 아군 전체 + 플레이어, 아군 0이면 단일도 플레이어 | 01 D-29 |
| HAND-01 | 1 | 손패 10장 초과 드로우 | 초과분은 버린 더미, Crystal은 턴 중 3 초과 가능 | 01 D-30 |
| SUM-01 | 1 | 소환 턴 행동 | 아군은 소환 턴 Command Phase에 행동, 적 소환 유닛은 다음 Enemy Phase부터 | 01 D-02 |

## 2. 카드/Resolver

| ID | 단계 | 시나리오 | 기대 결과 | 기준 문서 |
|---|---|---|---|---|
| CARD-01 | 1 | CardId 기반 카드 실행 | 카드명 없이 실행 가능 | 10_카드_런타임_매핑_상세.md |
| CARD-02 | 1 | 잘못된 CardId 주입 | 크래시 없이 안전 실패 + 로그 | 10_카드_런타임_매핑_상세.md, 08_RuntimeFactory_상세설계.md |
| CARD-03 | 1·2 | 카드 샘플 회귀 | 1단계 32장, 2단계 98장 누락 없음 | 10_카드_런타임_매핑_상세.md, 17_카드_데이터베이스.md |
| CARD-04 | 1 | NEC 액션 Exhaust | NEC_ACT_001~004 사용 후 ExhaustPile, 피의 의식 보너스 Crystal 1 | 01 D-20·D-24 |

## 3. 희생·Crystal 경제

| ID | 단계 | 시나리오 | 기대 결과 | 기준 문서 |
|---|---|---|---|---|
| ECO-01 | 1 | 같은 턴 희생 3회 (피의 잔 보유) | 첫 희생만 +1 추가. Lv1 +1, 파편 +1 | 01 D-04, 13_희생_융합_시스템_상세.md §2 |
| ECO-02 | 2 | 피의 잔 + `REL_BOSS_1F` 보유, 같은 턴 희생 2회 | 첫 희생 Lv1 기준 +3, 둘째는 +1 | 01 D-15 |
| ECO-03 | 2 | `REL_BOSS_5F` 보유, 같은 턴 아군 사망 2회 | Draw는 매번, Crystal은 첫 사망만 | 01 D-15 |

## 4. 융합

| ID | 단계 | 시나리오 | 기대 결과 | 기준 문서 |
|---|---|---|---|---|
| FUS-01 | 1 | FusionCharge | 노드 3·6·9 클리어 시 +1, 최대 3, 층 이동 시 유지, 충전 0이면 거부 | 01 D-03 |
| FUS-02 | 1 | 고정 레시피 | FUS_001(구울+구울)→식인 구울, FUS_014(뼈 병사+뼈 창병)→저주 기사. FUS_002·003·009는 미발동 | 01 D-03·D-22 |
| FUS-03 | 1 | 랜덤 융합 1만 회 | NEC Lv1 풀 클래스 80% / NEU 20%, Mutation 0%, 카드 없는 결과 0% | 01 D-21, 09_몬스터_바이블.xlsx Fusion_RandomPool |
| FUS-04 | 1 | 비용·레벨 제한 | Lv1+Lv1 Crystal 2, Lv3+Lv3 `LEVEL_CAP` 거부 | 01 D-03 |
| FUS-05 | 2 | Lv2+Lv2, 3층 융합 성소 | Crystal 3, 성소는 충전 소모 없이 1회 | 01 D-03·D-26 |

## 5. 상태이상

| ID | 단계 | 시나리오 | 기대 결과 | 기준 문서 |
|---|---|---|---|---|
| ST-01 | 1 | Burn 20스택 | TurnEnd 피해/감소 규칙 정확, 20 초과 부여 무시 | 11_상태이상_상세설계.md |
| ST-02 | 1 | Weak+Vulnerable 동시 | 상태 배율 ×0.9375, DEF 차감은 Weak 다음·Vulnerable 앞 | 11_상태이상_상세설계.md, 01 D-01 |
| ST-03 | 1 | Shield 처리 | 피해 흡수 후 TurnEnd 전량 제거 | 11_상태이상_상세설계.md |
| ST-04 | 1 | Poison 20스택 + Wave 전환 | 상한 20, Wave 사이 유지, 전투 종료 시 제거 | 01 D-08 |

## 6. AI/적 행동

| ID | 단계 | 시나리오 | 기대 결과 | 기준 문서 |
|---|---|---|---|---|
| AI-01 | 1 | Front/Back 우선 타겟 | 표준 타겟 규칙 일치 | 12_AI_상세설계.md, 21_적_스킬_의도_상세.md, 09_BattleField_슬롯_상세설계.md |
| AI-02 | 1 | 조건 분기 스킬 | 의도(Intent)와 실제 행동 동일 | 21_적_스킬_의도_상세.md |
| AI-03 | 1 | 타겟 없음 폴백 | 유효 타겟으로 안전 재선택 | 12_AI_상세설계.md |
| BOSS-01 | 1 | 1층 보스 Phase 3 | 2회 공격(ATK 8 × 2), 풀피 Lv1 Front도 턴당 1체 사망 | 01 D-05·D-18, 14_보스_패턴.xlsx |
| BOSS-02 | 2 | 3~5층 보스 페이즈 경계 | HP 50%·20% 경계에서 정확히 전환 | 22_보스_패턴_설계.md |

## 7. 진행/보상

| ID | 단계 | 시나리오 | 기대 결과 | 기준 문서 |
|---|---|---|---|---|
| PROG-01 | 1·2 | 완주 | 1단계 1~2층 18노드, 2단계 1~5층 45노드, 층 전환 정상 | 03_MVP_전층_스펙.md |
| PROG-02 | 1 | 보상 지급 | Reward_BattleTable 범위 일치, 보스 보상 유물 `REL_BOSS_*` | 03_MVP_전층_스펙.md, 23_콘텐츠_설계.md, 01 D-16 |
| PROG-03 | 2 | 상점 | 2~5층 노드6 상점 확정, 진열·가격·제거 `24` §3 | 03_MVP_전층_스펙.md, 24_콘텐츠_런타임_상세.md |
| PROG-04 | 1 | Mimic 입력 | Treasure 폴백, Mimic 노드 미생성 | 03_MVP_전층_스펙.md, 25_MVP_보완_시스템_상세.md |
| PROG-05 | 2 | 시작 클래스 카드 해금 | ELE·BEA·HOL 시작 시 그 클래스 카드 1층 해금 (1층 누적: ELE 50, HOL 52, BEA 52) | 01 D-07·D-17 |
| PROG-06 | 1 | 이벤트 풀 | 층마다 활성 6종, 공통 14%·신규 22%. 1~2층 합집합 7종 | 01 D-23 |
| TUT-01 | 1 | 희생 안내 전투 | 첫 런만 1층 노드 1 고정 손패·3단계 안내, 완료 후 TutorialDone=true, 두 번째 런부터 일반 전투 | 01 D-32 |
| UPG-01 | 1 | 휴식 강화 | Cards_Upgrade 카드별 값 적용, 저장 후 강화 단계 유지 | 01 D-31 |
| FLOW-01 | 1 | 런 흐름 | 타이틀→런→결과→타이틀, 카드 보상 건너뛰기, 포기=패배, 메타 화폐 층별 합산 | 01 D-37, 37 §1 |
| RELIC-01 | 2 | 유물 20종 트리거 | `24` §1 조건·제한(턴당·런당) 그대로 동작, 불멸의 양초는 희생에 미발동 | 01 D-38 |
| PROG-07 | 1 | 저장·이어하기 | 2층 보스 직전 저장 → 재시작 → HP·덱·유물·골드·FusionCharge·위치 동일 | 14_Save_Load_상세.md |
| PROG-08 | 1 | 1단계 카드 풀 제한 | 1~2층 카드 보상·이벤트 GrantCard 1만 회(고정 시드)에서 `Stage1CardPool` 32장 밖의 카드가 0회 | 01 D-44 |
| PROG-09 | 1 | 보상 희귀도 후보 부족 | 1~2층 카드 보상 1만 회(고정 시드): Legendary 0회, 희귀도 비율이 빈 희귀도 제외 후 정규화한 값과 ±1.5%p 이내, Rare가 나온 보상은 Rare 1장 + 다시 굴린 칸(중복 허용). 이벤트 GrantCard도 같은 규칙, 확률은 그 층 Normal 행 (D-49, `36` J25) | 01 D-49 |
| EVT-01 | 1 | 이벤트 저주 | 악마 계약 옵션2는 CUR_002·CUR_004만(같은 노드 시드면 같은 결과), 어둠의 내기 옵션2는 CUR_002, 저주 상자는 CUR_003. 각 저주의 드로우 시 효과·Ethereal·Exhaust 동작 | 01 D-45, 24 §2 |

## 8. 밸런스/시뮬레이터

| ID | 단계 | 시나리오 | 기대 결과 | 기준 문서 |
|---|---|---|---|---|
| BAL-01 | 2 | 밸런스 주의 카드 세트 | OTK/루프 경고 검출 | 17_카드_데이터베이스.md, 16_밸런스_규칙.md |
| BAL-02 | 2 | 런 단위 리포트 | 런 클리어율 30~45%, 플래그(`RUN_CLEAR_*`·`BATTLE_WIN_LOW`·`HP_LOSS_HIGH`) 출력 | 16_밸런스_규칙.md §8, 15_Battle_Simulator_설계.md §5 |
| BAL-03 | 2 | 재현성 테스트 | 동일 시드 동일 결과 | 15_Battle_Simulator_설계.md |
| BAL-04 | 1 | HP 예산 (수동 플레이 10런) | 전투당 평균 손실 일반 ≤4 / 엘리트 ≤10 / 보스 ≤15 | 01 D-14 |

## 9. 설계 데이터

| ID | 단계 | 시나리오 | 기대 결과 | 기준 문서 |
|---|---|---|---|---|
| DATA-01 | 1 | `python3 tools/check_design.py` | 모든 규칙 그룹 OK, 0 failed | 00_문서_인덱스.md |
| DATA-02 | 1 | `python3 tools/export_game_data.py --check` 후 Unity 임포트 | JSON이 Excel과 같고, 임포터 검사(`38` §5) 0 실패 | 38_데이터_임포트_매핑.md |

---

## 10. 합격 기준

**1단계 (1~2층 NEC 빌드)**
- Must Pass: 단계 1인 `CORE-*`, `DMG-*`, `CMD-01`, `INT-01`, `HIT-01`, `HAND-01`, `SUM-01`, `TUT-01`, `UPG-01`, `FLOW-01`, `CARD-01~04`, `ECO-01`, `FUS-01~04`, `PROG-01`(1~2층)·`02`·`04`·`06`·`07`·`08`·`09`, `EVT-01`, `DATA-01~02`
- Should Pass: 단계 1인 `ST-*`, `AI-*`, `BOSS-01`, `BAL-04`

**MVP 릴리즈 (2단계)**
- Must Pass: 1단계 Must Pass 전체 + `CARD-03`(98장), `ECO-02~03`, `FUS-05`, `PROG-01`(1~5층)·`03`·`05`
- Should Pass: 나머지 `ST-*`, `AI-*`, `BOSS-02`
- Release Gate: `BAL-01~03` 중 2개 이상 통과 + 잔여 리스크 문서화


---

## 11. 테스트 전략 (이슈 #52)

### 11.1 어셈블리 구조

전투·런 규칙은 Unity 없이 돌아가는 순수 C# 어셈블리에 둔다. 그래야 EditMode 테스트가 씬 없이 빠르게 돈다.

| asmdef | 폴더 (`DarkLegion/Assets/_Project/`) | 참조 | 내용 |
|---|---|---|---|
| `DarkLegion.Core` | `Scripts/Core/` | 없음 (`noEngineReferences: true`) | Runtime 유닛·Queue·TriggerBus·Damage·Status·AI·Turn·BattleRandom·런 진행·Save DTO |
| `DarkLegion.Data` | `Scripts/Data/` | Core | ScriptableObject 정의(`38` §3)와 SO → Core 정의 변환 |
| `DarkLegion.Game` | `Scripts/Game/`, `Scripts/UI/` | Core, Data | MonoBehaviour·ViewModel·View·연출 |
| `DarkLegion.Editor` | `Scripts/Editor/` | Core, Data | 임포터 (`38`) |
| `DarkLegion.Tests.EditMode` | `Tests/EditMode/` | Core, Data | 규칙·데이터 테스트 |
| `DarkLegion.Tests.PlayMode` | `Tests/PlayMode/` | Core, Data, Game | 화면 흐름 테스트 |

Core가 `UnityEngine`을 참조하면 컴파일 오류가 나므로, 로직이 UI·연출 쪽으로 새는 것을 어셈블리가 막는다 (`05` §1 Rule 5).

### 11.2 시나리오별 테스트 방식

| 시나리오 | 방식 | 데이터 |
|---|---|---|
| `CORE-*`·`DMG-*`·`CMD-01`·`INT-01`·`HIT-01`·`HAND-01`·`SUM-01`·`ST-*`·`AI-*`·`ECO-*` | **EditMode 자동** | 테스트 코드 안에서 만든 유닛 (손으로 적은 값) |
| `CARD-01~04`·`FUS-01~05`·`BOSS-*`·`UPG-01`·`RELIC-01` | **EditMode 자동** | 임포트된 SO (실제 카드·레시피·보스 데이터) |
| `PROG-01`·`02`·`04`·`06`·`07`·`08`·`09`, `EVT-01` | **EditMode 자동** | 임포트된 SO. 런 진행은 Core만으로 노드를 넘긴다. `PROG-07`은 SaveData JSON 왕복 비교 |
| `TUT-01`·`FLOW-01` | **PlayMode 자동** + 수동 확인 1회 | 씬 전환·버튼 입력. 문구·연출은 사람이 본다 |
| `BAL-04` | **수동** (10런) | 결과는 `35_운영_검증_팩.xlsx` 플레이테스트_로그에 기록 |
| `BAL-01~03` | Battle Simulator (2단계, `15`) | — |
| `DATA-01`·`DATA-02` | 명령 실행 | Python 도구와 Unity 임포터 |

- 테스트 이름은 시나리오 ID로 시작한다: `CORE_02_DeathTriggerChain()`. 단계는 `[Category("Stage1")]`·`[Category("Stage2")]`로 표시한다.
- **D-01 피해·D-29 피격 같은 규칙 테스트의 기대값은 손으로 적는다.** 같은 계산 함수로 기대값을 만들면 틀린 공식도 통과한다 (`check_design.py` R13과 같은 이유).

### 11.3 난수와 시드

- 모든 난수는 `BattleRandom`으로 뽑는다 (`06` §15). 테스트는 시드를 상수로 넘긴다: `new BattleContext(seed: 1234)`.
- 실패한 테스트는 메시지에 시드를 출력한다.
- 확률 테스트(`FUS-03` 1만 회)는 시드를 고정하고 허용 오차 ±1.5%p로 비교한다. 80% 기대값의 표준편차는 1만 회에서 0.4%p이므로 3σ(1.2%p)보다 여유가 있다.
- 전투 중 종료 재현(`14` §3 노드 시드): 같은 노드 시드로 두 번 시작했을 때 첫 손패와 적 의도가 같은지 `PROG-07`에 포함한다.

### 11.4 실행과 기록

- 에디터: Window → General → Test Runner에서 EditMode·PlayMode 실행
- 명령줄: `Unity -batchmode -projectPath DarkLegion -runTests -testPlatform EditMode -testResults TestResults/editmode.xml` (PlayMode도 같은 방식). `TestResults/`는 커밋하지 않는다
- 구현 PR 본문에는 EditMode·PlayMode 통과·실패 수를 적는다 (`30` §2)
- 1단계 완료 판정: §10 1단계 Must Pass가 모두 자동 테스트 통과(TUT-01·FLOW-01은 수동 확인 포함), Should Pass는 실패 목록과 사유를 `34`에 기록
---

*문서 목록: [00_문서_인덱스.md](00_문서_인덱스.md)*
