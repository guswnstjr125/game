# 다크 레기온 (Dark Legion)

몬스터를 소환하고, 희생하고, 융합하는 로그라이트 덱빌더. 혼자 개발하는 인디 프로젝트다.

## 현재 단계

- **설계**: 1단계(1~2층, 네크로맨서, 카드 32장) 구현에 필요한 설계 완료. 결정 로그 `docs/design/01_MVP_설계_동결_선언.md` §7
- **구현**: 1단계 착수 전. GitHub 이슈 #34(Unity 프로젝트 생성)부터 #47까지 6주 일정 (`docs/design/31_MVP_이슈_초안.md` §0)
- **엔진**: Unity 6.3 LTS, 2D URP, uGUI + TextMeshPro, PC 1920×1080 (`01` D-39). 프로젝트 폴더는 `DarkLegion/` (1단계 #34에서 생성)

## 폴더

| 경로 | 내용 |
|---|---|
| `docs/design/` | 설계 패키지 (MD + Excel). 시작은 [00_문서_인덱스.md](docs/design/00_문서_인덱스.md) |
| `tools/check_design.py` | 설계 MD·Excel 정합 검사. 설계 수치를 바꾼 PR에는 출력 전체를 붙인다 |
| `tools/build_ops_xlsx.py` | 운영 Excel 5종을 MD에서 생성 (`--check`는 차이만 보고) |
| `tools/export_game_data.py` | 정본 Excel을 Unity용 JSON으로 내보냄 (`docs/design/38`) |
| `tools/build_master_xlsx.py` | 리뷰용 `다크레기온_마스터통합.xlsx`를 정본 Excel에서 생성 |
| `.claude/skills/game-design/` | Claude Code용 설계 작업 스킬 |

## 작업 규칙

- `main`에 직접 올리지 않고 PR로 올린다. 작성자는 자기 PR을 병합하지 않는다.
- 문서끼리 충돌하면 `01` §7 결정 로그가 이긴다.
- 게임 규칙·수치 결정은 사용자가 내린다. 결정과 위임의 근거는 이슈나 PR에 남긴다.
