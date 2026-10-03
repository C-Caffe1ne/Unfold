; Build with Scripts/package-windows-installer.py (NSIS 3 Unicode).
Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "WinVer.nsh"
!include "x64.nsh"

!ifndef APP_VERSION
  !error "APP_VERSION is required"
!endif
!ifndef PAYLOAD
  !error "PAYLOAD is required"
!endif
!ifndef OUTPUT
  !error "OUTPUT is required"
!endif
!ifndef APP_ICON
  !error "APP_ICON is required"
!endif
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\DokhuStudio.Unfold"
!define LANGUAGE_KEY "Software\DokhuStudio\Unfold"

Name "Unfold Beta v${APP_NUMERIC_VERSION}"
OutFile "${OUTPUT}"
InstallDir "$LOCALAPPDATA\Programs\Unfold"
RequestExecutionLevel user
SetCompressor /SOLID lzma
SetCompressorDictSize 32
ShowInstDetails show
ShowUninstDetails show
VIProductVersion "${APP_NUMERIC_VERSION}.0"
VIAddVersionKey /LANG=1033 "ProductName" "Unfold"
VIAddVersionKey /LANG=1033 "ProductVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1033 "FileDescription" "Unfold per-user installer"
VIAddVersionKey /LANG=1033 "FileVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1033 "CompanyName" "Dokhu Studio"
VIAddVersionKey /LANG=1033 "LegalCopyright" "Dokhu Studio"

!define MUI_ICON "${APP_ICON}"
!define MUI_UNICON "${APP_ICON}"
!define MUI_ABORTWARNING
!define MUI_LANGDLL_REGISTRY_ROOT HKCU
!define MUI_LANGDLL_REGISTRY_KEY "${LANGUAGE_KEY}"
!define MUI_LANGDLL_REGISTRY_VALUENAME "InstallerLanguage"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\app\Unfold.exe"
!define MUI_FINISHPAGE_RUN_NOTCHECKED
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_WELCOME
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH
!insertmacro MUI_LANGUAGE "Korean"
!insertmacro MUI_LANGUAGE "English"

LangString MainComponent ${LANG_KOREAN} "Unfold (필수)"
LangString MainComponent ${LANG_ENGLISH} "Unfold (required)"
LangString DesktopComponent ${LANG_KOREAN} "바탕화면 바로가기"
LangString DesktopComponent ${LANG_ENGLISH} "Desktop shortcut"
LangString CloseApp ${LANG_KOREAN} "Unfold가 실행 중이거나 데이터 폴더에 접근할 수 없습니다. 트레이 메뉴에서 Unfold를 종료한 뒤 다시 시도해 주세요."
LangString CloseApp ${LANG_ENGLISH} "Unfold is running or its data folder is unavailable. Quit Unfold from the tray menu, then retry."
LangString WrongPlatform ${LANG_KOREAN} "Windows 10 이상, 64비트 환경에서 설치할 수 있습니다."
LangString WrongPlatform ${LANG_ENGLISH} "Windows 10 or later, 64-bit, is required."
LangString InvalidLocation ${LANG_KOREAN} "설치된 폴더의 제거 프로그램으로 실행해 주세요."
LangString InvalidLocation ${LANG_ENGLISH} "Run the uninstaller from its installed folder."

Var AppLock
Var DataDirectory

; The application holds this same file with FileShare.None. Keep the lock for the
; whole install/uninstall so another app cannot start while its files are replaced.
!macro LockFunctions PREFIX
Function ${PREFIX}AcquireAppLock
  ReadEnvStr $DataDirectory "UNFOLD_DATA_DIR"
  ${If} $DataDirectory == ""
    StrCpy $DataDirectory "$LOCALAPPDATA\Unfold"
  ${EndIf}
  CreateDirectory "$DataDirectory"
retry:
  System::Call 'kernel32::CreateFileW(w "$DataDirectory\.instance.lock", i 0xC0000000, i 0, p 0, i 4, i 0x80, p 0) p .r0'
  StrCpy $AppLock $0
  ${If} $AppLock == -1
    MessageBox MB_RETRYCANCEL|MB_ICONEXCLAMATION "$(CloseApp)" /SD IDCANCEL IDRETRY retry
    SetErrorLevel 1
    Abort
  ${EndIf}
FunctionEnd

Function ${PREFIX}ReleaseAppLock
  ${If} $AppLock != ""
  ${AndIf} $AppLock != -1
    System::Call 'kernel32::CloseHandle(p $AppLock)'
    StrCpy $AppLock ""
  ${EndIf}
FunctionEnd
!macroend
!insertmacro LockFunctions ""
!insertmacro LockFunctions "un."

Function .onInit
  !insertmacro MUI_LANGDLL_DISPLAY
  ${IfNot} ${AtLeastWin10}
    MessageBox MB_OK|MB_ICONSTOP "$(WrongPlatform)" /SD IDOK
    SetErrorLevel 1
    Abort
  ${EndIf}
  ${IfNot} ${RunningX64}
    MessageBox MB_OK|MB_ICONSTOP "$(WrongPlatform)" /SD IDOK
    SetErrorLevel 1
    Abort
  ${EndIf}
  SetRegView 64
  SetShellVarContext current
  ; A fixed managed installation path cannot overlap the user's data folder.
  StrCpy $INSTDIR "$LOCALAPPDATA\Programs\Unfold"
  Call AcquireAppLock
FunctionEnd

Function .onInstSuccess
  Call ReleaseAppLock
FunctionEnd
Function .onGUIEnd
  Call ReleaseAppLock
FunctionEnd

Section "$(MainComponent)" Main
  SectionIn RO
  ; Only the installer-managed runtime is replaced. Settings/history/custom pets
  ; live separately in LOCALAPPDATA\Unfold and are never removed here.
  RMDir /r "$INSTDIR\app"
  SetOutPath "$INSTDIR\app"
  File /r "${PAYLOAD}/*"
  SetOutPath "$INSTDIR"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\Unfold"
  CreateShortcut "$SMPROGRAMS\Unfold\Unfold.lnk" "$INSTDIR\app\Unfold.exe"
  CreateShortcut "$SMPROGRAMS\Unfold\Unfold 제거.lnk" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayName" "Unfold"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "Publisher" "Dokhu Studio"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\app\Unfold.exe,0"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "${UNINSTALL_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegStr HKCU "${UNINSTALL_KEY}" "URLInfoAbout" "https://unfoldpet.dokhustudio.com"
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1
  ${GetSize} "$INSTDIR\app" "/S=0K" $0 $1 $2
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "EstimatedSize" $0
  WriteRegStr HKCU "${LANGUAGE_KEY}" "InstallerLanguage" "$LANGUAGE"
  ; Preserve an already enabled login launch when migrating from the portable app.
  ReadRegStr $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "Unfold"
  ${If} $0 != ""
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "Unfold" '"$INSTDIR\app\Unfold.exe" --background'
  ${EndIf}
SectionEnd

Section /o "$(DesktopComponent)" DesktopShortcut
  CreateShortcut "$DESKTOP\Unfold.lnk" "$INSTDIR\app\Unfold.exe"
SectionEnd

Function un.onInit
  SetRegView 64
  SetShellVarContext current
  !insertmacro MUI_UNGETLANGUAGE
  ; NSIS runs its uninstall stub from TEMP, while INSTDIR retains the original folder.
  ${If} $INSTDIR != "$LOCALAPPDATA\Programs\Unfold"
    MessageBox MB_OK|MB_ICONSTOP "$(InvalidLocation)" /SD IDOK
    SetErrorLevel 1
    Abort
  ${EndIf}
  Call un.AcquireAppLock
FunctionEnd
Function un.onUninstSuccess
  Call un.ReleaseAppLock
FunctionEnd
Function un.onGUIEnd
  Call un.ReleaseAppLock
FunctionEnd

Section "Uninstall"
  ReadRegStr $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "Unfold"
  ${If} $0 == '"$INSTDIR\app\Unfold.exe" --background'
    DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "Unfold"
  ${EndIf}
  DeleteRegKey HKCU "${UNINSTALL_KEY}"
  DeleteRegValue HKCU "${LANGUAGE_KEY}" "InstallerLanguage"
  DeleteRegKey /ifempty HKCU "${LANGUAGE_KEY}"
  Delete "$SMPROGRAMS\Unfold\Unfold.lnk"
  Delete "$SMPROGRAMS\Unfold\Unfold 제거.lnk"
  RMDir "$SMPROGRAMS\Unfold"
  Delete "$DESKTOP\Unfold.lnk"
  RMDir /r "$INSTDIR\app"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  ; User settings, break history, custom pets and OS login credentials are preserved.
SectionEnd
