; -*- coding: utf-8 -*-
; Copyright Kind Computers. Licensed under the MIT License.
; Generated includes are produced from the verified portable payload by scripts/build.ps1.
Unicode true
ManifestSupportedOS all
RequestExecutionLevel admin
SetCompressor /SOLID lzma
SetCompressorDictSize 32
SetDateSave off
CRCCheck on
XPStyle on

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"
!include "Sections.nsh"

!ifndef PAYLOAD_DIR
 !error "PAYLOAD_DIR is required."
!endif
!ifndef OUTPUT_FILE
 !error "OUTPUT_FILE is required."
!endif
!ifndef APP_VERSION
 !error "APP_VERSION is required."
!endif
!ifndef FILE_VERSION
 !error "FILE_VERSION is required."
!endif
!ifndef INSTALL_FILES
 !error "INSTALL_FILES is required."
!endif
!ifndef UNINSTALL_FILES
 !error "UNINSTALL_FILES is required."
!endif
!ifndef VALIDATE_FILES
 !error "VALIDATE_FILES is required."
!endif
!ifndef UNVALIDATE_FILES
 !error "UNVALIDATE_FILES is required."
!endif

!define PRODUCT_NAME "Game Garage"
!define PRODUCT_ID "KindComputers.GameGarage"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_ID}"
Name "${PRODUCT_NAME} ${APP_VERSION} (Beta)"
Caption "${PRODUCT_NAME} ${APP_VERSION} (Beta)"
OutFile "${OUTPUT_FILE}"
InstallDir "$PROGRAMFILES64\Game Garage"
ShowInstDetails show
ShowUninstDetails show
VIProductVersion "${FILE_VERSION}"
VIAddVersionKey /LANG=1033 "ProductName" "Game Garage"
VIAddVersionKey /LANG=1033 "CompanyName" "Kind Computers"
VIAddVersionKey /LANG=1033 "FileDescription" "Game Garage ${APP_VERSION} (Beta) Setup"
VIAddVersionKey /LANG=1033 "FileVersion" "${FILE_VERSION}"
VIAddVersionKey /LANG=1033 "ProductVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1033 "LegalCopyright" "Kind Computers. MIT License."

Var RegisteredDir
Var FailureText
Var DeleteFailed
Var UpgradeMode
Var RunningResult
Var SessionHandle
Var OwnStartMenuShortcut
Var OwnDesktopShortcut

!define MUI_ICON "${__FILEDIR__}\..\..\GameGarage\GameGarage\Resources\KindComputersIcon.ico"
!define MUI_UNICON "${__FILEDIR__}\..\..\GameGarage\GameGarage\Resources\KindComputersIcon.ico"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "$(WelcomeTitle)"
!define MUI_WELCOMEPAGE_TEXT "$(WelcomeText)"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "${PAYLOAD_DIR}\LICENSE"
!insertmacro MUI_PAGE_COMPONENTS
!define MUI_PAGE_CUSTOMFUNCTION_LEAVE VerifyDirectoryPage
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_TITLE "$(FinishTitle)"
!define MUI_FINISHPAGE_TEXT "$(FinishText)"
!define MUI_FINISHPAGE_RUN
!define MUI_FINISHPAGE_RUN_FUNCTION LaunchApplication
!define MUI_FINISHPAGE_RUN_TEXT "$(LaunchText)"
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH
!insertmacro MUI_LANGUAGE "English"

LangString WelcomeTitle ${LANG_ENGLISH} "Game Garage ${APP_VERSION} (Beta)"
LangString WelcomeText ${LANG_ENGLISH} "Install Game Garage for all users.$\r$\n$\r$\nThis beta supports Windows 11 x64 with an English Windows installation. Setup does not run checks, repairs, drive maintenance, or restart Windows.$\r$\n$\r$\nClose Game Garage and its RAM worker before installing or upgrading."
LangString FinishTitle ${LANG_ENGLISH} "Game Garage is installed"
LangString FinishText ${LANG_ENGLISH} "Game Garage ${APP_VERSION} (Beta) is ready. Opening the app lets you choose when to verify your system."
LangString LaunchText ${LANG_ENGLISH} "Open Game Garage"
LangString RequiredFiles ${LANG_ENGLISH} "Game Garage (required)"
LangString DesktopShortcut ${LANG_ENGLISH} "Desktop shortcut"
LangString PlatformRequired ${LANG_ENGLISH} "This beta requires Windows 11 x64 (build 22000 or later). Setup has made no changes."
LangString EnglishRequired ${LANG_ENGLISH} "This beta requires an English Windows installation. Setup has made no changes."
LangString UnsafeLocation ${LANG_ENGLISH} "Choose a dedicated folder on a local fixed drive. Drive roots, Windows folders, temporary or user-profile folders, and redirected paths cannot be used."
LangString ExistingLocation ${LANG_ENGLISH} "Game Garage is already registered at:$\r$\n$RegisteredDir$\r$\n$\r$\nReinstall in that folder, or uninstall the existing copy first."
LangString EmptyLocation ${LANG_ENGLISH} "The selected folder contains files and is not the registered Game Garage installation. Choose an empty folder."
LangString RegistrationInvalid ${LANG_ENGLISH} "The existing Game Garage installation cannot be safely identified. Restore its registered uninstaller or remove it through Windows Installed apps before trying again."
LangString CloseApplications ${LANG_ENGLISH} "Game Garage, its RAM worker, or another process is using files in this installation.$\r$\n$\r$\nCancel any running checks, close Game Garage, and select Retry. Setup will not stop processes or restart Windows."
LangString RunningCheckFailed ${LANG_ENGLISH} "Setup could not check whether the installed files are in use. Close Game Garage and its RAM worker, then select Retry."
LangString PreviousRemovalFailed ${LANG_ENGLISH} "The previous Game Garage installation could not be removed completely. Close applications using its files, then try again. No new payload was installed."
LangString InstallFailed ${LANG_ENGLISH} "Game Garage could not be installed completely. Check free space and folder permissions, then run Setup again in the same folder."
LangString UninstallFailed ${LANG_ENGLISH} "Some Game Garage files could not be removed. Close applications using those files, then run the uninstaller again. Its registration has been retained."
LangString LaunchFailed ${LANG_ENGLISH} "Game Garage could not be opened. You can open it later from the Start menu."
LangString ShortcutCollision ${LANG_ENGLISH} "A Game Garage shortcut already exists and is not owned by this installation. Move or rename that shortcut before installing. Setup has not replaced it."
LangString InstallingStatus ${LANG_ENGLISH} "Installing Game Garage ${APP_VERSION} (Beta)"
LangString RemovingPrevious ${LANG_ENGLISH} "Removing the previous Game Garage payload"
LangString PreservingOtherFiles ${LANG_ENGLISH} "Unrelated files and nonempty folders are preserved."

; Stable failure categories, including /S: 10 platform/build, 11 language, 20 destination,
; 21 payload path, 22 shortcut path/collision, 23 files in use, 24 usage check failure,
; 26 uninstall registration mismatch, 30 previous removal, 31 payload/uninstaller write,
; 32 initial metadata, 35 shortcut/completion write, 40 payload delete, 41 shortcut delete,
; 42 registration removal. /SD suppresses prompts in silent mode.
!macro Fail Code Message
 SetErrorLevel ${Code}
 MessageBox MB_OK|MB_ICONSTOP "${Message}" /SD IDOK
 Abort
!macroend

; Validate every owned directory before the first payload mutation. The build generates
; these calls separately from its File/Delete lists, including the empty root "".
!macro ValidatePayloadDirectory Relative
 Push "$INSTDIR\${Relative}"
 Call CheckSafeDirectory
 ${If} $FailureText != ""
  !insertmacro Fail 21 "$FailureText"
 ${EndIf}
!macroend
!macro un.ValidatePayloadDirectory Relative
 Push "$INSTDIR\${Relative}"
 Call un.CheckSafeDirectory
 ${If} $FailureText != ""
  !insertmacro Fail 21 "$FailureText"
 ${EndIf}
!macroend

!macro ValidatePayloadFile Relative
 Push "$INSTDIR\${Relative}"
 Call CheckSafeFile
 ${If} $FailureText != ""
  !insertmacro Fail 21 "$FailureText"
 ${EndIf}
!macroend
!macro un.ValidatePayloadFile Relative
 Push "$INSTDIR\${Relative}"
 Call un.CheckSafeFile
 ${If} $FailureText != ""
  !insertmacro Fail 21 "$FailureText"
 ${EndIf}
!macroend

; Missing payload files are harmless; a failed deletion of an existing file is not.
!macro DeleteOwnedFile Relative
 ${If} ${FileExists} "$INSTDIR\${Relative}"
  ClearErrors
  Delete "$INSTDIR\${Relative}"
  ${If} ${Errors}
   StrCpy $DeleteFailed 1
  ${EndIf}
 ${EndIf}
!macroend

; Shared checks deliberately contain no OS-version or language gate. Uninstallation
; must remain possible if Windows settings change after installation.
!macro SharedFunctions Prefix
Function ${Prefix}CheckSafeDirectory
 Exch $0
 Push $1
 Push $2
 Push $3
 StrCpy $FailureText ""
 ; NSIS GetFullPathName expands existing long names and rejects an absent leaf.
 ; The native API canonicalizes a not-yet-created installation path as well.
 System::Call 'kernel32::GetFullPathNameW(w r0, i ${NSIS_MAX_STRLEN}, w .r1, p 0) i.r2'
 ${If} $2 == 0
 ${OrIf} $2 >= ${NSIS_MAX_STRLEN}
  StrCpy $FailureText "$(UnsafeLocation)"
  Goto safe_done
 ${EndIf}
 StrCpy $0 $1
 safe_parent:
 StrLen $1 $0
 ${If} $1 < 3
  StrCpy $FailureText "$(UnsafeLocation)"
  Goto safe_done
 ${EndIf}
 ; Win32 may normalize trailing spaces/dots differently from the displayed path.
 StrCpy $2 $0 1 -1
 ${If} $2 == " "
 ${OrIf} $2 == "."
  StrCpy $FailureText "$(UnsafeLocation)"
  Goto safe_done
 ${EndIf}
 System::Call 'kernel32::GetFileAttributesW(w r0) i.r1 ?e'
 Pop $2
 ${If} $1 == -1
  ${If} $2 != 2
  ${AndIf} $2 != 3
   StrCpy $FailureText "$(UnsafeLocation)"
   Goto safe_done
  ${EndIf}
 ${Else}
  IntOp $2 $1 & 0x400
  IntOp $3 $1 & 0x10
  ${If} $2 != 0
  ${OrIf} $3 == 0
   StrCpy $FailureText "$(UnsafeLocation)"
   Goto safe_done
  ${EndIf}
 ${EndIf}
 StrLen $1 $0
 ${If} $1 == 3
  Goto safe_done
 ${EndIf}
 ${GetParent} "$0" $0
 StrLen $1 $0
 ${If} $1 == 2
  StrCpy $0 "$0\"
 ${EndIf}
 Goto safe_parent
 safe_done:
 Pop $3
 Pop $2
 Pop $1
 Pop $0
FunctionEnd

Function ${Prefix}CheckSafeFile
 Exch $0
 Push $1
 Push $2
 StrCpy $FailureText ""
 System::Call 'kernel32::GetFileAttributesW(w r0) i.r1 ?e'
 Pop $2
 ${If} $1 == -1
  ${If} $2 != 2
  ${AndIf} $2 != 3
   StrCpy $FailureText "$(UnsafeLocation)"
  ${EndIf}
 ${Else}
  ; A target filename may not be a reparse point or a directory.
  IntOp $2 $1 & 0x410
  ${If} $2 != 0
   StrCpy $FailureText "$(UnsafeLocation)"
  ${EndIf}
 ${EndIf}
 Pop $2
 Pop $1
 Pop $0
FunctionEnd

Function ${Prefix}CheckShortcutPaths
 ; Callers set ownership/intent flags; unowned shortcuts are never inspected or removed.
 ${If} $OwnStartMenuShortcut == 1
  Push "$SMPROGRAMS\Game Garage"
  Call ${Prefix}CheckSafeDirectory
  ${If} $FailureText != ""
   Return
  ${EndIf}
  Push "$SMPROGRAMS\Game Garage\Game Garage.lnk"
  Call ${Prefix}CheckSafeFile
  ${If} $FailureText != ""
   Return
  ${EndIf}
 ${EndIf}
 ${If} $OwnDesktopShortcut == 1
  Push "$DESKTOP"
  Call ${Prefix}CheckSafeDirectory
  ${If} $FailureText != ""
   Return
  ${EndIf}
  Push "$DESKTOP\Game Garage.lnk"
  Call ${Prefix}CheckSafeFile
 ${EndIf}
FunctionEnd

Function ${Prefix}ValidateLocation
 StrCpy $FailureText ""
 ; Only drive-absolute local paths are accepted, never UNC/device/relative paths.
 StrCpy $0 $INSTDIR 2 1
 ${If} $0 != ":\"
  StrCpy $FailureText "$(UnsafeLocation)"
  Return
 ${EndIf}
 StrCpy $0 $INSTDIR 1
 System::Call '"$SYSDIR\shlwapi.dll"::PathIsRootW(w "$INSTDIR") i.r1'
 ${If} $1 != 0
  StrCpy $FailureText "$(UnsafeLocation)"
  Return
 ${EndIf}
 System::Call 'kernel32::GetFullPathNameW(w "$INSTDIR", i ${NSIS_MAX_STRLEN}, w .r0, p 0) i.r1'
 ${If} $1 == 0
 ${OrIf} $1 >= ${NSIS_MAX_STRLEN}
  StrCpy $FailureText "$(UnsafeLocation)"
  Return
 ${EndIf}
 StrCpy $INSTDIR $0
 StrLen $0 $INSTDIR
 ${If} $0 <= 3
  StrCpy $FailureText "$(UnsafeLocation)"
  Return
 ${EndIf}
 StrCpy $0 $INSTDIR 3
 System::Call 'kernel32::GetDriveTypeW(w r0) i.r1'
 ${If} $1 != 3
  StrCpy $FailureText "$(UnsafeLocation)"
  Return
 ${EndIf}
 ; Exclude alternate streams and wildcard/device syntax after the drive prefix.
 StrCpy $0 2
 location_chars:
 StrCpy $1 $INSTDIR 1 $0
 ${If} $1 == ""
  Goto location_chars_done
 ${EndIf}
 ${If} $1 == ":"
 ${OrIf} $1 == "*"
 ${OrIf} $1 == "?"
 ${OrIf} $1 == "/"
  StrCpy $FailureText "$(UnsafeLocation)"
  Return
 ${EndIf}
 IntOp $0 $0 + 1
 Goto location_chars
 location_chars_done:
 ${If} $INSTDIR == $PROGRAMFILES64
 ${OrIf} $INSTDIR == $PROGRAMFILES32
 ${OrIf} $INSTDIR == $COMMONFILES64
 ${OrIf} $INSTDIR == $COMMONFILES32
 ${OrIf} $INSTDIR == $SMPROGRAMS
 ${OrIf} $INSTDIR == $DESKTOP
 ${OrIf} $INSTDIR == $APPDATA
 ${OrIf} $INSTDIR == $LOCALAPPDATA
  StrCpy $FailureText "$(UnsafeLocation)"
  Return
 ${EndIf}
 ; Reject these complete trees, with a trailing separator boundary.
 StrCpy $1 "$INSTDIR\"
 StrCpy $0 "$WINDIR\"
 StrLen $2 $0
 StrCpy $3 $1 $2
 ${If} $3 == $0
  StrCpy $FailureText "$(UnsafeLocation)"
  Return
 ${EndIf}
 StrCpy $0 "$TEMP\"
 StrLen $2 $0
 StrCpy $3 $1 $2
 ${If} $3 == $0
  StrCpy $FailureText "$(UnsafeLocation)"
  Return
 ${EndIf}
 StrCpy $0 "$PROFILE\"
 StrLen $2 $0
 StrCpy $3 $1 $2
 ${If} $3 == $0
  StrCpy $FailureText "$(UnsafeLocation)"
  Return
 ${EndIf}
 Push "$INSTDIR"
 Call ${Prefix}CheckSafeDirectory
FunctionEnd

Function ${Prefix}CheckRunning
 ; Only inspect the target installation, not other portable copies or namesakes.
 StrCpy $RunningResult 0
 ${IfNot} ${FileExists} "$INSTDIR\GameGarage.exe"
 ${AndIfNot} ${FileExists} "$INSTDIR\StabilityTest.exe"
  Return
 ${EndIf}
 StrCpy $RunningResult 2
 StrCpy $SessionHandle 0
 System::Call '"$SYSDIR\rstrtmgr.dll"::RmStartSession(*i .r0, i 0, w .r1) i.r2'
 ${If} $2 != 0
  Return
 ${EndIf}
 StrCpy $SessionHandle $0
 ; The registered filename array and its UTF-16 buffers outlive the native call.
 System::Call '*(&w${NSIS_MAX_STRLEN} "$INSTDIR\GameGarage.exe") p.r0'
 System::Call '*(&w${NSIS_MAX_STRLEN} "$INSTDIR\StabilityTest.exe") p.r1'
 System::Call '*(p r0, p r1) p.r2'
 ${If} $0 != 0
 ${AndIf} $1 != 0
 ${AndIf} $2 != 0
  System::Call '"$SYSDIR\rstrtmgr.dll"::RmRegisterResources(i $SessionHandle, i 2, p r2, i 0, p 0, i 0, p 0) i.r3'
 ${Else}
  StrCpy $3 14
 ${EndIf}
 System::Free $2
 System::Free $1
 System::Free $0
 ${If} $3 == 0
  System::Call '"$SYSDIR\rstrtmgr.dll"::RmGetList(i $SessionHandle, *i .r0, *i 0 r1, p 0, *i .r2) i.r3'
  ${If} $3 == 234
   StrCpy $RunningResult 1
  ${ElseIf} $3 == 0
   ${If} $0 == 0
    StrCpy $RunningResult 0
   ${Else}
    StrCpy $RunningResult 1
   ${EndIf}
  ${EndIf}
 ${EndIf}
 System::Call '"$SYSDIR\rstrtmgr.dll"::RmEndSession(i $SessionHandle) i.r0'
 ; No RmShutdown/RmRestart calls: the user controls active checks and processes.
FunctionEnd

Function ${Prefix}EnsureStopped
 running_retry:
 Call ${Prefix}CheckRunning
 ${If} $RunningResult == 0
  Return
 ${EndIf}
 ${If} $RunningResult == 1
  StrCpy $FailureText "$(CloseApplications)"
  SetErrorLevel 23
 ${Else}
  StrCpy $FailureText "$(RunningCheckFailed)"
  SetErrorLevel 24
 ${EndIf}
 MessageBox MB_RETRYCANCEL|MB_ICONEXCLAMATION "$FailureText" /SD IDCANCEL IDRETRY running_retry
 Abort
FunctionEnd
!macroend
!insertmacro SharedFunctions ""
!insertmacro SharedFunctions "un."

Function ValidateDestination
 Call ValidateLocation
 ${If} $FailureText != ""
  Return
 ${EndIf}
 ${If} $RegisteredDir != ""
  ${If} $INSTDIR != $RegisteredDir
   StrCpy $FailureText "$(ExistingLocation)"
   Return
  ${EndIf}
  ReadRegDWORD $0 HKLM "${UNINSTALL_KEY}" "InstallerSchema"
  ${If} $0 != 1
  ${OrIfNot} ${FileExists} "$INSTDIR\uninstall.exe"
   StrCpy $FailureText "$(RegistrationInvalid)"
  ${EndIf}
  Return
 ${EndIf}
 ; An unregistered destination must be absent or genuinely empty.
 ClearErrors
 FindFirst $0 $1 "$INSTDIR\*"
 ${IfNot} ${Errors}
  directory_next:
  ${If} $1 != "."
  ${AndIf} $1 != ".."
  ${AndIf} $1 != ""
   StrCpy $FailureText "$(EmptyLocation)"
   Goto directory_done
  ${EndIf}
  ClearErrors
  FindNext $0 $1
  ${IfNot} ${Errors}
   Goto directory_next
  ${EndIf}
  directory_done:
  FindClose $0
 ${Else}
  System::Call 'kernel32::GetFileAttributesW(w "$INSTDIR") i.r0'
  ${If} $0 != -1
   StrCpy $FailureText "$(EmptyLocation)"
  ${EndIf}
 ${EndIf}
FunctionEnd

Function VerifyDirectoryPage
 Call ValidateDestination
 ${If} $FailureText != ""
  MessageBox MB_OK|MB_ICONEXCLAMATION "$FailureText" /SD IDOK
  Abort
 ${EndIf}
FunctionEnd

Function .onVerifyInstDir
 Call ValidateDestination
 ${If} $FailureText != ""
  SetErrors
 ${Else}
  ClearErrors
 ${EndIf}
FunctionEnd

Function .onInit
 StrCpy $OwnStartMenuShortcut 0
 StrCpy $OwnDesktopShortcut 0
 SetShellVarContext all
 SetRegView 64
 ; The bundled helper compares the native machine architecture numerically.
 ; Startup exits: 10 = unsupported architecture/build; 11 = unsupported language.
 ${IfNot} ${IsNativeAMD64}
  SetErrorLevel 10
  MessageBox MB_OK|MB_ICONSTOP "$(PlatformRequired)" /SD IDOK
  Quit
 ${EndIf}
 ReadRegStr $0 HKLM "Software\Microsoft\Windows NT\CurrentVersion" "CurrentBuildNumber"
 ${If} $0 < 22000
  SetErrorLevel 10
  MessageBox MB_OK|MB_ICONSTOP "$(PlatformRequired)" /SD IDOK
  Quit
 ${EndIf}
 ; Installed system UI language matches the app's initial supported locale boundary.
 System::Call 'kernel32::GetSystemDefaultUILanguage() i.r0'
 IntOp $0 $0 & 0x3ff
 ${If} $0 != 9
  SetErrorLevel 11
  MessageBox MB_OK|MB_ICONSTOP "$(EnglishRequired)" /SD IDOK
  Quit
 ${EndIf}
 ReadRegDWORD $OwnStartMenuShortcut HKLM "${UNINSTALL_KEY}" "StartMenuShortcut"
 ReadRegDWORD $OwnDesktopShortcut HKLM "${UNINSTALL_KEY}" "DesktopShortcut"
 ReadRegStr $RegisteredDir HKLM "${UNINSTALL_KEY}" "InstallLocation"
 ${If} $RegisteredDir != ""
  GetFullPathName $RegisteredDir "$RegisteredDir"
  ${If} $INSTDIR == "$PROGRAMFILES64\Game Garage"
   StrCpy $INSTDIR $RegisteredDir
  ${EndIf}
 ${EndIf}
 Call RestoreDesktopSelection
FunctionEnd

Section "$(RequiredFiles)" CoreFiles
 SectionIn RO
 SetShellVarContext all
 SetRegView 64
 Call ValidateDestination
 ${If} $FailureText != ""
  !insertmacro Fail 20 "$FailureText"
 ${EndIf}
 !include "${VALIDATE_FILES}"
 !insertmacro ValidatePayloadFile "uninstall.exe"
 Call ValidateShortcutDestinations
 ${If} $FailureText != ""
  !insertmacro Fail 22 "$FailureText"
 ${EndIf}
 Call EnsureStopped
 ${If} $RegisteredDir != ""
  DetailPrint "$(RemovingPrevious)"
  ; _?= keeps the old uninstaller in this process, so ExecWait waits for all cleanup.
  ; It must be last and unquoted. /UPGRADE retains registration and uninstall.exe.
  ClearErrors
  ExecWait '"$INSTDIR\uninstall.exe" /S /UPGRADE _?=$INSTDIR' $0
  ${If} ${Errors}
   !insertmacro Fail 30 "$(PreviousRemovalFailed)"
  ${EndIf}
  ${If} $0 != 0
   !insertmacro Fail 30 "$(PreviousRemovalFailed)"
  ${EndIf}
 ${EndIf}
 ; Repeat path/running checks after prior uninstall and before any replacement.
 !include "${VALIDATE_FILES}"
 !insertmacro ValidatePayloadFile "uninstall.exe"
 Call EnsureStopped
 DetailPrint "$(InstallingStatus)"
 ClearErrors
 SetOutPath "$INSTDIR"
 WriteUninstaller "$INSTDIR\uninstall.exe"
 ${If} ${Errors}
  !insertmacro Fail 31 "$(InstallFailed)"
 ${EndIf}
 ; Register an incomplete installation first, so interrupted extraction can be
 ; retried at the same location or removed with this exact generated file list.
 ClearErrors
 WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayName" "Game Garage"
 WriteRegStr HKLM "${UNINSTALL_KEY}" "Publisher" "Kind Computers"
 WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayVersion" "${APP_VERSION}"
 WriteRegStr HKLM "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
 WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\GameGarage.exe"
 WriteRegStr HKLM "${UNINSTALL_KEY}" "UninstallString" '$\"$INSTDIR\uninstall.exe$\"'
 WriteRegStr HKLM "${UNINSTALL_KEY}" "QuietUninstallString" '$\"$INSTDIR\uninstall.exe$\" /S'
 WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoModify" 1
 WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoRepair" 1
 WriteRegDWORD HKLM "${UNINSTALL_KEY}" "InstallerSchema" 1
 WriteRegStr HKLM "${UNINSTALL_KEY}" "InstallState" "Incomplete"
 WriteRegDWORD HKLM "${UNINSTALL_KEY}" "StartMenuShortcut" 0
 WriteRegDWORD HKLM "${UNINSTALL_KEY}" "DesktopShortcut" 0
 ${If} ${Errors}
  !insertmacro Fail 32 "$(InstallFailed)"
 ${EndIf}
 SetOverwrite on
 ClearErrors
 !include "${INSTALL_FILES}"
 ${If} ${Errors}
  !insertmacro Fail 31 "$(InstallFailed)"
 ${EndIf}
 ClearErrors
 SetOutPath "$INSTDIR"
 CreateDirectory "$SMPROGRAMS\Game Garage"
 CreateShortcut "$SMPROGRAMS\Game Garage\Game Garage.lnk" "$INSTDIR\GameGarage.exe" "" "$INSTDIR\GameGarage.exe"
 WriteRegDWORD HKLM "${UNINSTALL_KEY}" "StartMenuShortcut" 1
 WriteRegStr HKLM "${UNINSTALL_KEY}" "InstallState" "Complete"
 ${If} ${Errors}
  !insertmacro Fail 35 "$(InstallFailed)"
 ${EndIf}
 SetErrorLevel 0
SectionEnd

Section /o "$(DesktopShortcut)" DesktopLink
 ClearErrors
 CreateShortcut "$DESKTOP\Game Garage.lnk" "$INSTDIR\GameGarage.exe" "" "$INSTDIR\GameGarage.exe"
 WriteRegDWORD HKLM "${UNINSTALL_KEY}" "DesktopShortcut" 1
 ${If} ${Errors}
  !insertmacro Fail 35 "$(InstallFailed)"
 ${EndIf}
SectionEnd

Function RestoreDesktopSelection
 ${If} $OwnDesktopShortcut == 1
  !insertmacro SelectSection ${DesktopLink}
 ${EndIf}
FunctionEnd

Function ValidateShortcutDestinations
 StrCpy $FailureText ""
 ${If} ${FileExists} "$SMPROGRAMS\Game Garage\Game Garage.lnk"
 ${AndIf} $OwnStartMenuShortcut != 1
  StrCpy $FailureText "$(ShortcutCollision)"
  Return
 ${EndIf}
 SectionGetFlags ${DesktopLink} $0
 IntOp $0 $0 & ${SF_SELECTED}
 ${If} $0 != 0
  ${If} ${FileExists} "$DESKTOP\Game Garage.lnk"
  ${AndIf} $OwnDesktopShortcut != 1
   StrCpy $FailureText "$(ShortcutCollision)"
   Return
  ${EndIf}
 ${EndIf}
 ; Validate the locations that this installation intends to write.
 Push $OwnStartMenuShortcut
 Push $OwnDesktopShortcut
 StrCpy $OwnStartMenuShortcut 1
 StrCpy $OwnDesktopShortcut $0
 Call CheckShortcutPaths
 Pop $OwnDesktopShortcut
 Pop $OwnStartMenuShortcut
FunctionEnd

Function LaunchApplication
 IfSilent launch_done
 ClearErrors
 Exec '"$INSTDIR\GameGarage.exe"'
 ${If} ${Errors}
  MessageBox MB_OK|MB_ICONINFORMATION "$(LaunchFailed)" /SD IDOK
 ${EndIf}
 launch_done:
FunctionEnd

Function un.onInit
 ; Deliberately no platform or locale gate: removal always remains available.
 SetShellVarContext all
 SetRegView 64
 StrCpy $UpgradeMode 0
 ${GetParameters} $0
 ClearErrors
 ${GetOptions} "$0" "/UPGRADE" $1
 ${IfNot} ${Errors}
  StrCpy $UpgradeMode 1
 ${EndIf}
 Call un.ValidateLocation
 ${If} $FailureText != ""
  SetErrorLevel 20
  MessageBox MB_OK|MB_ICONSTOP "$FailureText" /SD IDOK
  Quit
 ${EndIf}
 ReadRegDWORD $OwnStartMenuShortcut HKLM "${UNINSTALL_KEY}" "StartMenuShortcut"
 ReadRegDWORD $OwnDesktopShortcut HKLM "${UNINSTALL_KEY}" "DesktopShortcut"
 ReadRegStr $RegisteredDir HKLM "${UNINSTALL_KEY}" "InstallLocation"
 ${If} $RegisteredDir != ""
  GetFullPathName $RegisteredDir "$RegisteredDir"
 ${EndIf}
 ${If} $RegisteredDir != $INSTDIR
  SetErrorLevel 26
  MessageBox MB_OK|MB_ICONSTOP "$(RegistrationInvalid)" /SD IDOK
  Quit
 ${EndIf}
FunctionEnd

Section "Uninstall"
 SetShellVarContext all
 SetRegView 64
 !include "${UNVALIDATE_FILES}"
 !insertmacro un.ValidatePayloadFile "uninstall.exe"
 Call un.CheckShortcutPaths
 ${If} $FailureText != ""
  !insertmacro Fail 22 "$FailureText"
 ${EndIf}
 Call un.EnsureStopped
 StrCpy $DeleteFailed 0
 DetailPrint "$(PreservingOtherFiles)"
 !include "${UNINSTALL_FILES}"
 ; Empty-directory removal may fail because unrelated files remain; this is fine.
 ClearErrors
 ${If} $DeleteFailed != 0
  !insertmacro Fail 40 "$(UninstallFailed)"
 ${EndIf}
 ${If} $OwnStartMenuShortcut == 1
  ${If} ${FileExists} "$SMPROGRAMS\Game Garage\Game Garage.lnk"
   ClearErrors
   Delete "$SMPROGRAMS\Game Garage\Game Garage.lnk"
   ${If} ${Errors}
    !insertmacro Fail 41 "$(UninstallFailed)"
   ${EndIf}
  ${EndIf}
  RMDir "$SMPROGRAMS\Game Garage"
 ${EndIf}
 ${If} $OwnDesktopShortcut == 1
  ${If} ${FileExists} "$DESKTOP\Game Garage.lnk"
   ClearErrors
   Delete "$DESKTOP\Game Garage.lnk"
   ${If} ${Errors}
    !insertmacro Fail 41 "$(UninstallFailed)"
   ${EndIf}
  ${EndIf}
 ${EndIf}
 ${If} $UpgradeMode == 0
  ClearErrors
  DeleteRegKey HKLM "${UNINSTALL_KEY}"
  ${If} ${Errors}
   !insertmacro Fail 42 "$(UninstallFailed)"
  ${EndIf}
  SetOutPath "$TEMP"
  Delete "$INSTDIR\uninstall.exe"
  RMDir "$INSTDIR"
 ${EndIf}
 SetErrorLevel 0
SectionEnd
