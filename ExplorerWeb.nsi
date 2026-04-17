; ExplorerWeb Installer with .NET 8 Runtime Auto-Download
; Author: Hadi Cahyadi <cumulus13@gmail.com>

!define APP_NAME "ExplorerWeb"
!define APP_VERSION "1.0.2"
!define APP_PUBLISHER "Hadi Cahyadi"
!define APP_URL "https://github.com/cumulus13/ExplorerWeb"
!define DOTNET_VERSION "8.0"
!define DOTNET_DOWNLOAD_URL_X64 "https://download.visualstudio.microsoft.com/download/pr/836cce68-d7d4-4027-955a-bab6d921d68c/24ec3a2df6785b128a9bbde2faeeed1e/dotnet-runtime-8.0.3-win-x64.exe"
!define DOTNET_DOWNLOAD_URL_X86 "https://download.visualstudio.microsoft.com/download/pr/f0a786df-9956-481b-b5f3-b054b2edd294/af3c7af3d4444ace418c6f73b7264ea6/dotnet-runtime-8.0.3-win-x86.exe"

; Include Modern UI
!include "MUI2.nsh"
!include "x64.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "WordFunc.nsh"
!include "StrFunc.nsh"

; General settings
Name "${APP_NAME} ${APP_VERSION}"
OutFile "${APP_NAME}-Setup-${APP_VERSION}.exe"
InstallDir "$PROGRAMFILES64\${APP_PUBLISHER}\${APP_NAME}"
InstallDirRegKey HKLM "Software\${APP_PUBLISHER}\${APP_NAME}" "InstallDir"
RequestExecutionLevel admin

; Modern UI settings
!define MUI_ABORTWARNING
!define MUI_ICON "app.ico"
!define MUI_UNICON "app.ico"

; Pages
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\ExplorerWeb.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Launch ${APP_NAME}"
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; Languages
!insertmacro MUI_LANGUAGE "English"

; Variables
Var DotNetInstalled
Var DotNetInstallerPath
Var DownloadUrl

; ============================================================================
; FUNCTIONS
; ============================================================================

Function .onInit
  ; Check if running on 64-bit system
  ${If} ${RunningX64}
    StrCpy $DownloadUrl "${DOTNET_DOWNLOAD_URL_X64}"
  ${Else}
    StrCpy $DownloadUrl "${DOTNET_DOWNLOAD_URL_X86}"
  ${EndIf}
FunctionEnd

Function CheckDotNetRuntime
  DetailPrint "Checking for .NET ${DOTNET_VERSION} Runtime..."

  StrCpy $DotNetInstalled "0"
  StrCpy $3 0

  ${If} ${RunningX64}
    StrCpy $1 "SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.NETCore.App"
  ${Else}
    StrCpy $1 "SOFTWARE\dotnet\Setup\InstalledVersions\x86\sharedfx\Microsoft.NETCore.App"
  ${EndIf}

  loop:
    ClearErrors
    EnumRegKey $0 HKLM "$1" $3
    ${If} ${Errors}
      Goto done
    ${EndIf}

    DetailPrint "Found runtime version: $0"

    StrCpy $2 $0 2
    ${If} $2 == "8."
      StrCpy $DotNetInstalled "1"
      DetailPrint ".NET ${DOTNET_VERSION} Runtime found: $0"
      Goto done
    ${EndIf}

    IntOp $3 $3 + 1
    Goto loop

  done:
    ${If} $DotNetInstalled == "0"
      DetailPrint ".NET ${DOTNET_VERSION} Runtime not found"
    ${EndIf}
FunctionEnd

Function DownloadAndInstallDotNet
  DetailPrint "Downloading .NET ${DOTNET_VERSION} Runtime..."

  StrCpy $DotNetInstallerPath "$TEMP\dotnet-runtime-installer.exe"

  NSISdl::download "$DownloadUrl" "$DotNetInstallerPath"
  Pop $R0

  ${If} $R0 != "OK"
    MessageBox MB_ICONEXCLAMATION|MB_OK "Download failed: $R0"
    Abort
  ${EndIf}

  DetailPrint "Installing .NET Runtime silently..."
  ExecWait '"$DotNetInstallerPath" /install /quiet /norestart' $0

  ${If} $0 == 0
    DetailPrint ".NET installed successfully"
  ${ElseIf} $0 == 3010
    DetailPrint ".NET installed (reboot required)"
  ${Else}
    MessageBox MB_ICONSTOP "Installation failed with code: $0"
    Abort
  ${EndIf}

  Delete "$DotNetInstallerPath"
FunctionEnd

; ============================================================================
; INSTALLER SECTION
; ============================================================================

Section "Install" SecInstall
  ; Check for .NET Runtime
  Call CheckDotNetRuntime
  
  ${If} $DotNetInstalled == "0"
    MessageBox MB_ICONINFORMATION|MB_YESNO ".NET ${DOTNET_VERSION} Runtime is required but not installed.$\n$\nWould you like to download and install it now?" IDYES InstallDotNet IDNO SkipDotNet
    
    InstallDotNet:
      Call DownloadAndInstallDotNet
      Goto ContinueInstall
    
    SkipDotNet:
      MessageBox MB_ICONEXCLAMATION|MB_OK "Installation cannot continue without .NET ${DOTNET_VERSION} Runtime.$\n$\nPlease install it manually from:$\n$DownloadUrl"
      Abort
  ${EndIf}
  
  ContinueInstall:
  SetOutPath "$INSTDIR"
  
  ; Install files
  File /r "publish\framework-dependent\*.*"
  
  ; Create shortcuts
  CreateDirectory "$SMPROGRAMS\${APP_PUBLISHER}"
  CreateShortcut "$SMPROGRAMS\${APP_PUBLISHER}\${APP_NAME}.lnk" "$INSTDIR\ExplorerWeb.exe"
  CreateShortcut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\ExplorerWeb.exe"
  
  ; Write uninstaller
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  
  ; Write registry keys
  WriteRegStr HKLM "Software\${APP_PUBLISHER}\${APP_NAME}" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "Software\${APP_PUBLISHER}\${APP_NAME}" "Version" "${APP_VERSION}"
  
  ; Add to Add/Remove Programs
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "UninstallString" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayIcon" "$INSTDIR\ExplorerWeb.exe"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "Publisher" "${APP_PUBLISHER}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "URLInfoAbout" "${APP_URL}"
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "NoModify" 1
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "NoRepair" 1
  
  DetailPrint "Installation complete!"
SectionEnd

; ============================================================================
; UNINSTALLER SECTION
; ============================================================================

Section "Uninstall"
  ; Kill process if running
  nsExec::Exec 'taskkill /F /IM ExplorerWeb.exe'
  Sleep 500
  
  ; Remove files
  Delete "$INSTDIR\ExplorerWeb.exe"
  Delete "$INSTDIR\*.dll"
  Delete "$INSTDIR\*.json"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  
  ; Remove shortcuts
  Delete "$SMPROGRAMS\${APP_PUBLISHER}\${APP_NAME}.lnk"
  RMDir "$SMPROGRAMS\${APP_PUBLISHER}"
  Delete "$DESKTOP\${APP_NAME}.lnk"
  
  ; Remove registry keys
  DeleteRegKey HKLM "Software\${APP_PUBLISHER}\${APP_NAME}"
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
  
  MessageBox MB_ICONINFORMATION "${APP_NAME} has been uninstalled successfully."
SectionEnd