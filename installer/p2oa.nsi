; ============================================================================
;  p2oa Installer
;  Requires:
;    - NSIS 3.x              https://nsis.sourceforge.io
;    - EnVar plugin          https://nsis.sourceforge.io/EnVar_plug-in
;      (copy EnVar.dll to NSIS\Plugins\x86-unicode\)
;
;  Build:
;    1. Publish the project first:
;         dotnet publish ..\PostmanOpenAPIConverter -c Release -r win-x64 --self-contained
;    2. Compile this script:
;         makensis p2oa.nsi
; ============================================================================

!include "MUI2.nsh"
!include "x64.nsh"

; ----------------------------------------------------------------------------
;  Metadata
; ----------------------------------------------------------------------------

Name              "p2oa"
OutFile           "p2oa-setup.exe"
Unicode           True
InstallDir        "$PROGRAMFILES64\p2oa"
InstallDirRegKey  HKLM "Software\p2oa" "InstallDir"
RequestExecutionLevel admin

!define PRODUCT_NAME  "p2oa"
!define PUBLISHER     "Paulo Santos"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\p2oa"
!define EXE_SRC       "..\PostmanOpenAPIConverter\bin\publish\p2oa.exe"

; PRODUCT_VERSION can be overridden at compile time: makensis /DPRODUCT_VERSION=1.0.26.0309b p2oa.nsi
; VI_VERSION must be strictly numeric (x.x.x.x) for the PE header.
; Pass it separately when PRODUCT_VERSION includes a letter suffix.
!ifndef PRODUCT_VERSION
  !define PRODUCT_VERSION "1.0.0.0"
!endif
!ifndef VI_VERSION
  !define VI_VERSION "${PRODUCT_VERSION}"
!endif

VIProductVersion                  "${VI_VERSION}"
VIAddVersionKey "ProductName"     "${PRODUCT_NAME}"
VIAddVersionKey "FileDescription" "p2oa Installer"
VIAddVersionKey "FileVersion"     "${PRODUCT_VERSION}"
VIAddVersionKey "ProductVersion"  "${PRODUCT_VERSION}"
VIAddVersionKey "LegalCopyright"  "Copyright (c) ${PUBLISHER}"

; ----------------------------------------------------------------------------
;  MUI Settings
; ----------------------------------------------------------------------------

!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_NOAUTOCLOSE

; Optional: swap in an ICO file once one is available
; !define MUI_ICON   "..\resources\p2oa.ico"
; !define MUI_UNICON "..\resources\p2oa.ico"

; Welcome page
!define MUI_WELCOMEPAGE_TITLE     "Welcome to the p2oa Setup Wizard"
!define MUI_WELCOMEPAGE_TEXT      "p2oa converts Postman collections to OpenAPI \
specifications and to Postman\u2019s Git-compatible YAML format.$\r$\n$\r$\n\
p2oa.exe will be installed and added to your system PATH so you can \
run it from any terminal.$\r$\n$\r$\nClick Next to continue."

; Finish page
!define MUI_FINISHPAGE_TITLE      "Installation Complete"
!define MUI_FINISHPAGE_TEXT       "p2oa has been installed successfully.$\r$\n$\r$\n\
Open a new terminal and run:$\r$\n$\r$\n    p2oa --help"

; ----------------------------------------------------------------------------
;  Installer pages
; ----------------------------------------------------------------------------

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE     "..\LICENSE"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

; ----------------------------------------------------------------------------
;  Uninstaller pages
; ----------------------------------------------------------------------------

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; ----------------------------------------------------------------------------
;  Language
; ----------------------------------------------------------------------------

!insertmacro MUI_LANGUAGE "English"

; ============================================================================
;  Install Section
; ============================================================================

Section "p2oa (required)" SecMain

  SectionIn RO   ; always selected, cannot be deselected

  SetOutPath "$INSTDIR"
  File "${EXE_SRC}"

  ; Add install directory to the system PATH
  EnVar::SetHKLM
  EnVar::AddValue "PATH" "$INSTDIR"
  Pop $0
  ${If} $0 <> 0
    MessageBox MB_ICONEXCLAMATION "Could not update the system PATH (error $0).$\r$\nYou may need to add $\"$INSTDIR$\" to PATH manually."
  ${EndIf}

  ; Register with Programs & Features
  WriteRegStr   HKLM "${UNINSTALL_KEY}" "DisplayName"     "${PRODUCT_NAME}"
  WriteRegStr   HKLM "${UNINSTALL_KEY}" "DisplayVersion"  "${PRODUCT_VERSION}"
  WriteRegStr   HKLM "${UNINSTALL_KEY}" "Publisher"       "${PUBLISHER}"
  WriteRegStr   HKLM "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr   HKLM "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\uninstall.exe"'
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoModify"        1
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoRepair"        1

  ; Write install location so future upgrades find it
  WriteRegStr   HKLM "Software\p2oa" "InstallDir" "$INSTDIR"

  ; Write the uninstaller
  WriteUninstaller "$INSTDIR\uninstall.exe"

SectionEnd

; ============================================================================
;  Uninstall Section
; ============================================================================

Section "Uninstall"

  ; Remove install directory from the system PATH
  EnVar::SetHKLM
  EnVar::DeleteValue "PATH" "$INSTDIR"
  Pop $0

  ; Remove files
  Delete "$INSTDIR\p2oa.exe"
  Delete "$INSTDIR\uninstall.exe"

  ; Remove directory
  RMDir "$INSTDIR"

  ; Clean up registry
  DeleteRegKey HKLM "${UNINSTALL_KEY}"
  DeleteRegKey HKLM "Software\p2oa"

SectionEnd
