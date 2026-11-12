# 작업 규칙 (Claude Code)

## 기본으로 쓰는 스킬 (사용자 지시, 2026-11-09)

`mattpocock-skills` 플러그인(`.claude/settings.json`)은 해당 작업에서 **따로 요청이 없어도 기본으로** 불러 쓴다.

| 작업 | 스킬 |
|---|---|
| 설계 규칙·수치·데이터 작업 전부 | `game-design` (저장소 스킬, 항상 먼저) |
| 코드 구현 (`DarkLegion/`, `tools/`) | `mattpocock-skills:tdd` — 테스트를 먼저 쓰고 통과시킨다 |
| 구현 PR을 올리기 전 자체 검토 | `mattpocock-skills:code-review` |
| PR 본문 작성 | `mattpocock-skills:pr` |
| 테스트 실패·버그 | `mattpocock-skills:diagnosing-bugs` |
| 결정 전에 규칙의 빈틈 찾기 | `mattpocock-skills:grilling` — 단, 사용자가 결정을 위임한 항목은 사용자에게 묻지 않고 Claude가 스스로 질문·답을 정리해 근거로 남긴다. 위임 결정은 근거 댓글(위임 문장 원문·정한 내용·이유)을 남기면 확정이다. 사용자 본인 승인 댓글은 요구하지 않는다 (`01` D-60, game-design 스킬 §2.4) |
| 구조·인터페이스 설계 | `mattpocock-skills:codebase-design` |

## 충돌할 때

- 저장소 규칙이 이긴다: `.claude/skills/game-design/SKILL.md`, `docs/design/01_MVP_설계_동결_선언.md` §4·§7.
- 결정은 `01` §7의 D 번호로만 기록한다. `domain-modeling`의 ADR·GLOSSARY 파일을 따로 만들지 않는다 (결정 로그가 둘이 되면 어긋난다).
- PR 본문에는 `pr` 스킬 형식과 함께 game-design 스킬 §6의 필수 항목(검사 출력 전체, D 번호와 결정 주체·근거)을 넣는다.
- 작성자는 병합하지 않는다 (사용자가 명시적으로 지시한 경우만 예외).
