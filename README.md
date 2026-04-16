# ExplorerWeb — File Explorer Web App

A full-featured web File Explorer for Windows — browse, manage, copy, rename, delete, and preview files in your browser.

---

[![Screenshot](https://raw.githubusercontent.com/cumulus13/explorerweb/master/screenshot.png)](https://raw.githubusercontent.com/cumulus13/explorerweb/master/screenshot.png)

---

## Run

```powershell
cd ExplorerWeb
dotnet restore
dotnet run
```

Opens at **http://localhost:5054**

## Features

| Feature | Description |
|---|---|
| **Browse** | Navigate any folder on your PC — all drives listed |
| **Address bar** | Click to edit path directly, or click breadcrumbs |
| **Quick Access** | Desktop, Documents, Downloads, Pictures, Music, Videos |
| **Drive sidebar** | All drives with free space bar chart |
| **List / Grid / Compact** | Three view modes |
| **Sort** | By name, date, size, type — ascending/descending |
| **Show/hide hidden** | Toggle hidden & system files |
| **Search** | Search by filename in current folder (recursive) |
| **New Folder** | Create folders inline |
| **Rename** | F2 or toolbar button |
| **Delete** | Move to Recycle Bin (Del) or permanent (Shift+Del) |
| **Copy / Cut / Paste** | Ctrl+C, Ctrl+X, Ctrl+V — works across folders |
| **Select all** | Ctrl+A, or header checkbox |
| **Multi-select** | Checkbox or Ctrl+click |
| **Properties** | Alt+Enter — size, dates, attributes |
| **Preview pane** | Text files, images inline; download button for others |
| **Download** | Direct file download via browser |
| **Keyboard shortcuts** | F2 rename, F5 refresh, Del/Shift+Del delete, Alt+←→↑ navigate, Ctrl+C/X/V copy/cut/paste |

## Keyboard Shortcuts

| Key | Action |
|---|---|
| F2 | Rename selected |
| F5 | Refresh |
| Del | Move to Recycle Bin |
| Shift+Del | Permanently delete |
| Ctrl+C | Copy |
| Ctrl+X | Cut |
| Ctrl+V | Paste |
| Ctrl+A | Select all |
| Alt+← | Back |
| Alt+→ | Forward |
| Alt+↑ | Up |
| Alt+Enter | Properties |
| Escape | Deselect / close dialogs |

## Publish

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -o .\publish
.\publish\ExplorerWeb.exe
```

## 👤 Author
        
[Hadi Cahyadi](mailto:cumulus13@gmail.com)
    

[![Buy Me a Coffee](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://www.buymeacoffee.com/cumulus13)

[![Donate via Ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/cumulus13)
 
[Support me on Patreon](https://www.patreon.com/cumulus13)