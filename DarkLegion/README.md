# DarkLegion (Unity 프로젝트)

> 설계 정본: `docs/design/` (`01` D-39 엔진, D-46 폴더, `32` §11 테스트). 이슈 #34.

## 지금 들어 있는 것

Unity 프로젝트 파일(`ProjectSettings/`, `Packages/`)은 아직 없다. Unity Editor가 있어야 만들 수 있어서, 먼저 Unity 없이 돌아가는 전투 규칙 코어만 넣었다.

| 경로 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Core/` | `DarkLegion.Core` (asmdef, `noEngineReferences: true`). 피해 공식(D-01), `BattleRandom`·노드 시드, 액션 큐(Main·Trigger·Death, 한도), `BattleContext` 승패 판정(30턴) |
| `Assets/_Project/Tests/EditMode/` | `DarkLegion.Tests.EditMode` (asmdef). NUnit 테스트. Unity Test Runner와 아래 dotnet 둘 다에서 돈다 |

## Unity 프로젝트 만들기 (#34, 한 번만)

이 폴더가 비어 있지 않아서 Unity Hub가 여기에 바로 새 프로젝트를 만들지 않는다. 다른 곳에 만든 뒤 옮긴다.

1. Unity Hub → New project → **Unity 6.3 LTS**, 템플릿 **Universal 2D**, 이름 `DarkLegion_new`, 위치는 저장소 밖 아무 곳
2. Hub에서 한 번 열었다가 닫는다 (기본 파일 생성)
3. `DarkLegion_new/`의 `Packages/`, `ProjectSettings/`를 이 폴더(`DarkLegion/`)로 복사한다. `Assets/` 안의 기본 파일(Settings, Scenes 등)은 `DarkLegion/Assets/`로 복사한다. `Library/`, `Temp/`, `Logs/`, `UserSettings/`는 복사하지 않는다
4. Unity Hub → Add → `DarkLegion/` 폴더를 연다
5. Edit → Project Settings → Editor: Asset Serialization **Force Text**, Version Control **Visible Meta Files** (D-39)
6. Package Manager에서 **Test Framework**, **Input System**, **TextMeshPro** 확인 (`31` #1)
7. Window → General → Test Runner → EditMode → Run All. 아래 dotnet 결과와 같은 수가 통과해야 한다
8. 저장소 루트 `.gitignore`에 Unity 항목(`DarkLegion/[Ll]ibrary/`, `DarkLegion/[Tt]emp/`, `DarkLegion/[Ll]ogs/`, `DarkLegion/[Uu]serSettings/`, `DarkLegion/[Bb]uild*/`, `DarkLegion/*.csproj`, `DarkLegion/*.sln`)을 더하고, 생성된 `.meta` 파일과 함께 커밋한다

## Unity 없이 테스트 (명령줄)

```
dotnet test tools/dotnet/DarkLegion.Core.Tests
```

- .NET 8 SDK가 필요하다. 이 프로젝트는 위 두 폴더의 `.cs`를 그대로 컴파일한다 (C# 9, Unity 6과 같은 문법 수준).
- Core가 `UnityEngine`을 쓰면 여기서 컴파일 오류가 난다. 그것이 의도다 (`32` §11.1).
