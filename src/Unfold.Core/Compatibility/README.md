# 이전 데이터 호환

일반 사용자 화면에서 제거한 픽셀 에디터·내 루틴·업무 프로필의 저장 계약만 유지한다.
네임스페이스와 JSON 필드 이름은 기존 그대로다.

- `AppSettings.Personalization.cs`: 이전 루틴·프로필 값의 검증과 불변 스냅샷.
  `AppSettings.Recovery.cs`, `BreakSession`, 현재 알림과 기록·CSV에서 사용한다.
- `PiskelCodec.cs`, `PixelDocument.cs`: 이전 픽셀 펫 원본의 읽기·왕복 검증.
- `CharacterLibrary.PixelSources.cs`: 원본의 충돌 검사·저장·재열기.
  격리된 `--smoke-test`와 라이브러리·재생·복구 회귀 검사에서 사용한다.

현재 PNG·GIF·GLB 펫 로딩, 미디어 펫 만들기와 펫 팩 설치는 기존 런타임에서 수행한다.
이 폴더는 편집 화면을 제공하지 않는다. 파일을 다시 저장할 때도 이전 설정·기록·펫을 보존한다.
