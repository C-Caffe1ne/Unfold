# 보관 자료

이 디렉터리는 현재 제품의 실행 지침이 아니다. 각 문서 상단의 상태 표시가 원문의
작업 지시·승인 조건·담당 배정보다 우선한다. 현재 작업은 [문서 안내](../README.md)와
[MVP 범위](../mvp.md)를 확인한다.

## Swift/Piskel 기록

- [이전 Swift 앱 안내](swift/legacy-swift.md)
- [이전 Swift 픽셀 에디터](swift/native-pixel-editor.md)
- [Swift 설계 기록](swift/superpowers/specs/)
- [Swift 구현 계획과 상태](swift/superpowers/plans/)
- [Piskel 인수인계 기록](swift/superpowers/handoffs/)
- [macOS 시스템 커서 실험](swift/validation/2026-09-08-macos-cursor-feasibility.md)

이 문서들은 서로 다른 시점의 Swift/Piskel 작업을 설명한다. 일부 기능은 당시에도
미완료였고, 이미 제거된 웹 리소스 경로도 등장한다. 당시 테스트 수와 OS 관찰은
현재 C# 앱으로 이월되지 않는다.

현재 checkout에서 Swift 소스·테스트, `Package.swift`, Xcode 앱·아이콘 설정,
Swift 전용 패키징 파일·스크립트·수동 CI·VS Code 실행 설정을 제거했다.
삭제 직전 기준 커밋은 `97f9a845db12b77581fcc863d354f9084ebe2c3b`이며 다음처럼 읽을 수 있다:

```sh
git show 97f9a845db12b77581fcc863d354f9084ebe2c3b:Package.swift
git show 97f9a845db12b77581fcc863d354f9084ebe2c3b:Sources/Unfold/App/AppDelegate.swift
```

기본 캐릭터 자산은 삭제하지 않았다. 원래 `Sources/Unfold/Resources/Characters/`에
있던 파일은 현재 [Assets/Characters](../../Assets/Characters/)에 있다.

## Garden 실험 기록

- [당시 팀 운영 계획](garden/team/README.md)
- [당시 세션 기록](garden/team/SESSIONS.md)
- [UI·자산 조사](garden/team/reports/B-01-presentation.md)
- [Windows·배포 조사](garden/team/reports/C-01-platform.md)
- [자동 검증 보고서](garden/team/reports/D-01-qa.md)
- [당시 미실행 Windows 양식](garden/windows-dogfooding-log.md)

이 자료의 기준은 `experiment/garden-windows` / `3098f20`이다. 기존 `docs/team/`에
있던 미추적 문서를 보존해 옮겼다. A-01 지시서는 있으나 정리 시점에 A-01 보고서
파일은 없었다. 세션 ID·작업자 소유권·Garden 착수 조건은 과거 기록이며, 현재 담당
배정이나 실행 중인 작업의 증거가 아니다. Garden 소스는 해당 브랜치에 남아 있다.
