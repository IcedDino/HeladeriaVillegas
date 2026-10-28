Unicode true
RequestExecutionLevel admin
SetCompressor /SOLID lzma

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"

!ifndef APP_VERSION
  !define APP_VERSION "1.0.0"
!endif
!ifndef PUBLISH_DIR
  !define PUBLISH_DIR "${__FILEDIR__}\..\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish"
!endif
!ifndef PREREQUISITES_DIR
  !define PREREQUISITES_DIR "${__FILEDIR__}\..\artifacts\prerequisites"
!endif

!define APP_NAME "Heladeria Villegas POS"
!define APP_EXE "HeladeriaPOS.Maui.exe"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\HeladeriaVillegasPOS"
!define PREREQ_VC "vc_redist.x64.exe"
!define PREREQ_WEBVIEW "MicrosoftEdgeWebView2RuntimeInstallerX64.exe"

Name "${APP_NAME}"
OutFile "${__FILEDIR__}\..\artifacts\installer\HeladeriaVillegas-Setup-x64.exe"
InstallDir "$PROGRAMFILES64\Heladeria Villegas POS"
InstallDirRegKey HKLM "${UNINSTALL_KEY}" "InstallLocation"
ShowInstDetails show
ShowUninstDetails show
BrandingText "Nerqova"

VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey "CompanyName" "Nerqova"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "FileDescription" "Instalador de ${APP_NAME}"
VIAddVersionKey "FileVersion" "${APP_VERSION}"
VIAddVersionKey "ProductVersion" "${APP_VERSION}"
VIAddVersionKey "OriginalFilename" "HeladeriaVillegas-Setup-x64.exe"
VIAddVersionKey "LegalCopyright" "Copyright 2026 Nerqova"

!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_TITLE "Instalacion completada"
!define MUI_FINISHPAGE_TEXT "${APP_NAME} quedo instalado. Al terminar se abrira con permisos de administrador."
!define MUI_FINISHPAGE_RUN
!define MUI_FINISHPAGE_RUN_FUNCTION "LaunchInstalledAppElevated"
!define MUI_FINISHPAGE_RUN_TEXT "Iniciar ${APP_NAME} como administrador"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Spanish"

Var VCInstalled
Var WebViewInstalled
Var DependencyExitCode
Var RuntimeVersion

Section "${APP_NAME}" SEC_APP
  SectionIn RO
  SetShellVarContext all

!ifdef UPDATE_ONLY
  InitPluginsDir
  SetOutPath "$PLUGINSDIR"
  File /oname=check-light-update.ps1 "${__FILEDIR__}\check-light-update.ps1"
  File /oname=retained-files.json "${PUBLISH_DIR}\retained-files.json"
  ExecWait '"$SYSDIR\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "$PLUGINSDIR\check-light-update.ps1" -InstallDirectory "$INSTDIR" -ManifestPath "$PLUGINSDIR\retained-files.json" -LogPath "$TEMP\HeladeriaVillegas-update-check.log"' $DependencyExitCode
  ${If} $DependencyExitCode != 0
    SetErrorLevel 1
    IfSilent +2
    MessageBox MB_ICONSTOP "No se pudo verificar la actualizacion o el respaldo. Cierra la caja y revisa $TEMP\HeladeriaVillegas-update-check.log. Tus datos no se modificaron."
    Abort
  ${EndIf}
!else
  SetOutPath "$PLUGINSDIR"
  File /oname=${PREREQ_VC} "${PREREQUISITES_DIR}\${PREREQ_VC}"
  File /oname=${PREREQ_WEBVIEW} "${PREREQUISITES_DIR}\${PREREQ_WEBVIEW}"

  Call CheckVisualCpp
  ${If} $VCInstalled != 1
    DetailPrint "Instalando Microsoft Visual C++ x64..."
    ExecWait '"$PLUGINSDIR\${PREREQ_VC}" /install /quiet /norestart' $DependencyExitCode
    ${If} $DependencyExitCode == 3010
    ${OrIf} $DependencyExitCode == 1641
      SetRebootFlag true
    ${ElseIf} $DependencyExitCode != 0
      MessageBox MB_ICONSTOP "No se pudo instalar Microsoft Visual C++ x64. Codigo: $DependencyExitCode"
      Abort
    ${EndIf}
  ${EndIf}

  Call CheckWebView2
  ${If} $WebViewInstalled != 1
    DetailPrint "Instalando Microsoft Edge WebView2..."
    ExecWait '"$PLUGINSDIR\${PREREQ_WEBVIEW}" /silent /install' $DependencyExitCode
    ${If} $DependencyExitCode == 3010
    ${OrIf} $DependencyExitCode == 1641
      SetRebootFlag true
    ${ElseIf} $DependencyExitCode != 0
      MessageBox MB_ICONSTOP "No se pudo instalar Microsoft Edge WebView2. Codigo: $DependencyExitCode"
      Abort
    ${EndIf}
  ${EndIf}

!endif
  SetOutPath "$INSTDIR"
  File /r "${PUBLISH_DIR}\*.*"

  CreateDirectory "$SMPROGRAMS\Heladeria Villegas POS"
  CreateShortCut "$SMPROGRAMS\Heladeria Villegas POS\Heladeria Villegas POS.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0 SW_SHOWNORMAL "" "${APP_NAME}" "$INSTDIR"
  CreateShortCut "$SMPROGRAMS\Heladeria Villegas POS\Desinstalar.lnk" "$INSTDIR\uninstall.exe"

  WriteUninstaller "$INSTDIR\uninstall.exe"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "Publisher" "Nerqova"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\${APP_EXE}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\uninstall.exe"'
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoRepair" 1
SectionEnd

Section /o "Crear acceso directo en el escritorio" SEC_DESKTOP
  SetShellVarContext all
  CreateShortCut "$DESKTOP\Heladeria Villegas POS.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0 SW_SHOWNORMAL "" "${APP_NAME}" "$INSTDIR"
SectionEnd

Section "Uninstall"
  SetShellVarContext all
  Delete "$DESKTOP\Heladeria Villegas POS.lnk"
  Delete "$SMPROGRAMS\Heladeria Villegas POS\Heladeria Villegas POS.lnk"
  Delete "$SMPROGRAMS\Heladeria Villegas POS\Desinstalar.lnk"
  RMDir "$SMPROGRAMS\Heladeria Villegas POS"
  DeleteRegKey HKLM "${UNINSTALL_KEY}"
  RMDir /r "$INSTDIR"
SectionEnd

Function CheckVisualCpp
  StrCpy $VCInstalled 0
  ClearErrors
  ReadRegDWORD $0 HKLM "Software\Microsoft\VisualStudio\14.0\VC\Runtimes\x64" "Installed"
  IfErrors check_vc_done
  ReadRegDWORD $1 HKLM "Software\Microsoft\VisualStudio\14.0\VC\Runtimes\x64" "Major"
  IfErrors check_vc_done
  ReadRegDWORD $2 HKLM "Software\Microsoft\VisualStudio\14.0\VC\Runtimes\x64" "Minor"
  IfErrors check_vc_done
  StrCmp $0 1 0 check_vc_done
  ${If} $1 > 14
    StrCpy $VCInstalled 1
  ${ElseIf} $1 == 14
    ${If} $2 >= 38
      StrCpy $VCInstalled 1
    ${EndIf}
  ${EndIf}
check_vc_done:
FunctionEnd

Function CheckWebView2
  StrCpy $WebViewInstalled 0
  StrCpy $RuntimeVersion ""
  ReadRegStr $RuntimeVersion HKLM "Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" "pv"
  StrCmp $RuntimeVersion "" check_webview_user
  StrCmp $RuntimeVersion "0.0.0.0" check_webview_user webview_present
check_webview_user:
  ReadRegStr $RuntimeVersion HKCU "Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" "pv"
  StrCmp $RuntimeVersion "" check_webview_done
  StrCmp $RuntimeVersion "0.0.0.0" check_webview_done webview_present
webview_present:
  StrCpy $WebViewInstalled 1
check_webview_done:
FunctionEnd

Function LaunchInstalledAppElevated
  SetOutPath "$INSTDIR"
  Exec '"$INSTDIR\${APP_EXE}"'
FunctionEnd

Function .onInit
  SetRegView 64
  ; Accept both the original Inno helper and the NSIS helper command lines.
  ${GetParameters} $R0
  ClearErrors
  ${GetOptions} $R0 "/VERYSILENT" $R1
  ${IfNot} ${Errors}
    SetSilent silent
  ${EndIf}
  ClearErrors
  ${GetOptions} $R0 "/DIR=" $R1
  ${IfNot} ${Errors}
    StrCpy $INSTDIR $R1
  ${EndIf}
FunctionEnd

Function un.onInit
  SetRegView 64
  SetShellVarContext all
FunctionEnd
