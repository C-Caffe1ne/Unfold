# 구현 보고서 — QA-2 버전 정합성

## 원인

`src/Unfold.Desktop/Unfold.Desktop.csproj:6`의 `<Version>`이 `0.2.0`에 머물러 있었다.
Git 이력에는 v0.2.1 라벨 커밋 2건(`5e91545`, `0b72a36`)이 있고 `artifacts/`에
v0.2.1 산출물이 없어, 배포물이 어느 커밋을 반영하는지 추적할 수 없었다.

이 필드는 버전 단일 출처다. `Scripts/publish-desktop.ps1:15`가 아카이브 이름에,
`Scripts/make-macos-bundle.sh:11,35`가 `CFBundleShortVersionString`에 사용한다.

## 변경

- `src/Unfold.Desktop/Unfold.Desktop.csproj:6` — `<Version>0.2.0</Version>` → `<Version>0.2.1</Version>`

단일 출처이므로 패키징 스크립트 2개는 수정 없이 새 값을 읽는다.

## 소유 파일

- `src/Unfold.Desktop/Unfold.Desktop.csproj`

## 검증

| 검사 | 결과 |
|---|---|
| `dotnet test Unfold.slnx -c Release` | **216 통과 / 0 실패 / 0 건너뜀** |
| 빌드 | 오류 0 |
| 버전 소비처 | `publish-desktop.ps1`, `make-macos-bundle.sh` 모두 csproj에서 정규식으로 읽음 — 하드코딩 없음 확인 |
| 테스트의 버전 단정 | 없음 (grep 확인) |

회귀 검사는 추가하지 않았다. 이 변경은 동작이 아니라 메타데이터 값이고,
단일 출처 계약 자체는 기존 스크립트가 이미 강제한다.

## 미검증·위험

- **테스트 개수 불일치:** 실측 216인데 `docs/verification.md`가 가리키는 검증 문서와
  `42_release_qa.md`는 215라고 기록한다. 작업 트리 파일은 그 사이 변경되지 않았다.
  원인 미상이며 이 변경과 무관하다. 문서 수치 정정이 필요하다.
- **산출물 미생성.** v0.2.1 배포물과 `SHA256SUMS-v0.2.1.txt`는 아직 없다.
  `dotnet publish`는 이번에 실행하지 않았다.
- `make-macos-bundle.sh:36`의 `CFBundleVersion`은 여전히 `3`으로 하드코딩돼 있다.
  이번 Task 범위 밖이라 손대지 않았다.
