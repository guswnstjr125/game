# 작업 규칙 (Codex 등 Claude 외 에이전트)

다크 레기온: Unity 6.3 LTS 2D URP 로그라이트 덱빌더, MVP 5층, 1인 개발. Claude도 이 저장소에서 같이 작업한다.
사용자와 대화, 문서, PR, 커밋 메시지는 한국어로 쓰고 결론부터 적는다.

## 정본 (먼저 읽는다)

| 무엇 | 어디 |
|---|---|
| 설계 작업 절차 전체 (결정·값 변경·검산·검사·PR) | `.claude/skills/game-design/SKILL.md` — 설계 규칙·수치·데이터를 건드리면 항상 먼저 |
| 작업별 기본 스킬과 충돌 규칙 | `CLAUDE.md` — 너에게도 그대로 적용된다 |
| 주제별 정본 MD·Excel 위치 | `docs/design/00_문서_인덱스.md` §2 |
| 결정 로그 (D 번호) | `docs/design/01_MVP_설계_동결_선언.md` §7 — 다른 문서와 충돌하면 이것이 이긴다 |
| 처리한 설계 문제 기록 (J 번호) | `docs/design/36_설계_수정_필요_목록.md` |
| 작업 순서 — 다음에 무엇을 할지 고를 때 | [이슈 #76 작업 지도](https://github.com/guswnstjr125/game/issues/76) — 앞 단계가 끝나야 시작하는 일은 건너뛰지 않는다 |

## 기본 스킬 플러그인 (mattpocock/skills)

Claude는 `.claude/settings.json`에 등록된 `mattpocock-skills`를 쓴다. 너도 같은 플러그인을 설치하고, `CLAUDE.md` 표의 작업마다 같은 스킬을 따로 요청이 없어도 쓴다.

```bash
codex plugin marketplace add mattpocock/skills
codex plugin add mattpocock-skills@mattpocock
```

설치가 안 되면 https://github.com/mattpocock/skills/tree/main/skills 의 `SKILL.md`를 읽고 그 절차를 따른다. 경로는 `engineering/tdd`, `engineering/code-review`, `engineering/pr`, `engineering/diagnosing-bugs`, `engineering/codebase-design`, `productivity/grilling`이다.

이 저장소에서는 다음처럼 맞춘다:
- 설정은 이미 끝나 있다. 이슈는 GitHub Issues, 문서는 `docs/design`. `setup-matt-pocock-skills`는 건너뛴다.
- 결정은 `01` §7의 D 번호 한 곳에만 기록한다. ADR·GLOSSARY 파일은 만들지 않는다.
- 사용자가 위임한 빈곳은 grilling 질문과 답을 스스로 정리해 이슈 근거 댓글로 남긴다 (`01` D-60, 스킬 §2.4).
- PR 본문은 pr 스킬 형식에 game-design 스킬 §6 필수 항목(검사 출력 전체, D 번호와 결정 주체·근거 링크)을 더한다.

## 결정 (D 번호)

- 새 번호 = `01` §7 표의 마지막 번호 + 1. 쓰기 전에 열린 PR 브랜치의 `01`까지 확인해 Claude와 번호가 겹치지 않게 한다.
- 네가 정한 위임 결정은 결정 주체를 "사용자 위임"으로 쓰고, 근거 댓글에 작성자가 Codex임을 밝힌다.
- 사용자에게 선택지로 묻는 것은 세 가지다: MVP 범위(`01` §1)를 넓히는 것, 되돌리기 어려운 것(플랫폼·엔진·출시), 사용자가 직접 고르겠다고 한 것. 나머지 빈곳은 최선안으로 정하고 근거를 남긴다.

## 코드

- 코어: `DarkLegion/Assets/_Project/Scripts/Core`. UnityEngine 없이(noEngineReferences) C# 9 문법만 쓴다.
- 테스트: `DarkLegion/Assets/_Project/Tests/EditMode`. 실행은 `dotnet test tools/dotnet/DarkLegion.Core.Tests` (경고도 오류).
- tdd 스킬대로 빨강 → 초록, 한 조각씩. 기대값은 손으로 계산한 숫자로 쓴다 (`32` §11.2).
- 규칙이 비어 구현이 막히면 D 번호로 정한 뒤 구현한다.

## PR 전에 전부 통과

```bash
python3 tools/check_design.py                      # 0 failed
python3 tools/build_ops_xlsx.py --check            # 0 sheet(s)
python3 tools/build_master_xlsx.py --check         # 0 sheet(s)
python3 tools/export_game_data.py --check --out <임시 폴더>   # 0 file(s)
dotnet test tools/dotnet/DarkLegion.Core.Tests     # 0 failed
```

CI(`.github/workflows/checks.yml`)도 같은 검사를 돌린다. 실패하면 원인을 고쳐 초록으로 만든다.

## 브랜치·PR과 Claude와의 분담

- 브랜치를 만들어 PR로 올린다. 병합은 리뷰한 다른 사람이나 사용자가 한다 (사용자가 명시적으로 지시하면 예외, `01` D-60).
- 열린 PR이 서로 쌓여 있다 (`base`가 다른 PR 브랜치). 새 작업은 그 작업이 기대는 브랜치 위에서 시작하고 PR `base`도 그 브랜치로 한다.
- 이슈를 잡을 때 먼저 그 이슈에 "Codex 작업 중" 댓글을 남기고, 이미 누가 잡은 이슈는 다른 것을 고른다.
- `01` §7 표와 `36` 목록은 둘 다 자주 고치는 파일이다. 작게 나눠 빨리 PR을 올려 충돌을 줄인다.
- 사람이 할 일(AI 그림 시험, Unity 설치, 플레이 측정 #28)은 사용자에게 남긴다.

## 지킬 선

- 사용자 이메일·회사 정보는 저장소 밖 서비스와 커밋에 넣지 않는다.
- 커밋과 PR에는 모델 이름을 적지 않는다.
- CI는 테스트를 고쳐서 통과시킨다. 테스트를 끄거나 건너뛰지 않는다.
- 남의 브랜치는 merge로 따라간다. rebase·force-push는 하지 않는다.
