; ExplorerWeb Installer with .NET 8 Runtime Auto-Download
; Author: Hadi Cahyadi <cumulus13@gmail.com>

!define APP_NAME "ExplorerWeb"
!define APP_VERSION "1.1.0"
!define APP_PUBLISHER "Hadi Cahyadi"
!define APP_URL "https://github.com/cumulus13/ExplorerWeb"
!define DOTNET_VERSION "8.0"
!define DOTNET_DOWNLOAD_URL_X64 "https://download.visualstudio.microsoft.com/download/pr/836cce68-d7d4-4027-955a-bab6d921d68c/24ec3a2df6785b128a9bbde2faeeed1e/dotnet-runtime-8.0.3-win-x64.exe"
!define DOTNET_DOWNLOAD_URL_X86 "https://download.visualstudio.microsoft.com/download/pr/f0a786df-9956-481b-b5f3-b054b2edd294/af3c7af3d4444ace418c6f73b7264ea6/dotnet-runtime-8.0.3-win-x86.exe"

; Include Modern UI
!include "MUI2.nsh"
!include "x64.nsh"
!include "LogicLib.nsh"

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
!define MUI_FINISHPAGE_RUN_TEXT "Launch ${APP_NAME} in the system tray"
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
  
  ; Simple check: just look for registry key existence
  ClearErrors
  ${If} ${RunningX64}
    ReadRegStr $0 HKLM "SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost" "Version"
  ${Else}
    ReadRegStr $0 HKLM "SOFTWARE\dotnet\Setup\InstalledVersions\x86\sharedhost" "Version"
  ${EndIf}
  
  ${If} ${Errors}
    ; Try alternative registry location
    ClearErrors
    EnumRegKey $0 HKLM "SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.NETCore.App" 0
    ${If} ${Errors}
      DetailPrint ".NET ${DOTNET_VERSION} Runtime not found"
      StrCpy $DotNetInstalled "0"
    ${Else}
      DetailPrint ".NET Runtime found"
      StrCpy $DotNetInstalled "1"
    ${EndIf}
  ${Else}
    DetailPrint ".NET Runtime found: $0"
    StrCpy $DotNetInstalled "1"
  ${EndIf}
FunctionEnd

Function DownloadAndInstallDotNet
  DetailPrint "Downloading .NET ${DOTNET_VERSION} Runtime..."
  
  ; Set temp path for installer
  StrCpy $DotNetInstallerPath "$TEMP\dotnet-runtime-8.0.3-installer.exe"
  
  ; Download using NSISdl
  NSISdl::download /TIMEOUT=30000 "$DownloadUrl" "$DotNetInstallerPath"
  Pop $R0
  
  ${If} $R0 == "success"
    DetailPrint "Download complete. Installing .NET Runtime..."
    
    ; Run installer with silent flags
    DetailPrint "Running: $DotNetInstallerPath /install /quiet /norestart"
    ExecWait '"$DotNetInstallerPath" /install /quiet /norestart' $0
    
    ${If} $0 == 0
      DetailPrint ".NET Runtime installed successfully"
      Delete "$DotNetInstallerPath"
    ${ElseIf} $0 == 3010
      DetailPrint ".NET Runtime installed (reboot required)"
      Delete "$DotNetInstallerPath"
    ${Else}
      DetailPrint "Installation returned code: $0"
      MessageBox MB_ICONEXCLAMATION|MB_OK ".NET Runtime installation failed (Error: $0).$\n$\nPlease install manually from:$\n$DownloadUrl"
      Delete "$DotNetInstallerPath"
      Abort
    ${EndIf}
  ${Else}
    DetailPrint "Download failed: $R0"
    MessageBox MB_ICONEXCLAMATION|MB_YESNO "Failed to download .NET Runtime.$\n$\nWould you like to open the download page in your browser?" IDYES OpenBrowser IDNO SkipBrowser
    
    OpenBrowser:
      ExecShell "open" "$DownloadUrl"
    
    SkipBrowser:
    Abort
  ${EndIf}
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
  CreateShortcut "$SMPROGRAMS\${APP_PUBLISHER}\Uninstall ${APP_NAME}.lnk" "$INSTDIR\Uninstall.exe"
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
  RMDir /r "$INSTDIR"
  
  ; Remove shortcuts
  Delete "$SMPROGRAMS\${APP_PUBLISHER}\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_PUBLISHER}\Uninstall ${APP_NAME}.lnk"
  RMDir "$SMPROGRAMS\${APP_PUBLISHER}"
  Delete "$DESKTOP\${APP_NAME}.lnk"
  
  ; Remove registry keys
  DeleteRegKey HKLM "Software\${APP_PUBLISHER}\${APP_NAME}"
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
  
  MessageBox MB_ICONINFORMATION "${APP_NAME} has been uninstalled successfully."
SectionEnd
