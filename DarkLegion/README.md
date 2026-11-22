# DarkLegion (Unity 프로젝트)

> 설계 정본: `docs/design/` (`01` D-39 엔진, D-46 폴더, `32` §11 테스트). 이슈 #34.

## 지금 들어 있는 것

Unity 6.3 LTS(`6000.3.29f1`)용 Universal 2D(URP) 프로젝트다. 전투 코어는 Unity 없이도 테스트할 수 있다.

| 경로 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Core/` | `DarkLegion.Core` (asmdef, `noEngineReferences: true`). 피해 공식(D-01), `BattleRandom`·노드 시드, 액션 큐(Main·Trigger·Death, 한도), `BattleContext` 승패 판정(30턴) |
| `Assets/_Project/Tests/EditMode/` | `DarkLegion.Tests.EditMode` (asmdef). NUnit 테스트. Unity Test Runner와 아래 dotnet 둘 다에서 돈다 |

## Unity에서 열고 Git에 저장하기

1. Unity Hub의 **Projects → Add → Add project from disk**에서 현재 Git 작업 폴더의 `DarkLegion/`을 선택한다.
2. 에디터에서 저장한 씬과 에셋은 관련 `.meta` 파일과 함께 커밋한다. `Packages/`와 `ProjectSettings/`도 Git으로 관리한다.
3. **Window → General → Test Runner → EditMode → Run All**로 코어 테스트를 실행한다.

## Unity 없이 테스트 (명령줄)

```
dotnet test tools/dotnet/DarkLegion.Core.Tests
```

- .NET 8 SDK가 필요하다. 이 프로젝트는 위 두 폴더의 `.cs`를 그대로 컴파일한다 (C# 9, Unity 6과 같은 문법 수준).
- Core가 `UnityEngine`을 쓰면 여기서 컴파일 오류가 난다. 그것이 의도다 (`32` §11.1).
