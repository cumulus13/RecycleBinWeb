# RecycleBinWeb

A professional web UI for the Windows Recycle Bin — list, search, restore, and permanently delete files directly from your browser. Built with **ASP.NET Core 8** and pure HTML/CSS/JS.

---

## Features

| Feature | Description |
|---|---|
| **Real Recycle Bin** | Reads actual `$Recycle.Bin` metadata from all fixed drives |
| **List & Grid view** | Toggle between Explorer-style list and icon grid |
| **Search** | Live filter by filename or original path |
| **Sort** | By date, name, size, or type |
| **Drive filter** | Filter by C:, D:, etc. |
| **Type filter** | Sidebar: Folders, Images, Video, Audio, Documents, Code, Archives |
| **Restore** | Move file/folder back to its original location |
| **Permanent delete** | Remove from Recycle Bin forever |
| **Bulk select** | Checkbox multi-select + bulk restore/delete |
| **Empty Bin** | One-click empty entire Recycle Bin |
| **Stats** | Item count + total size with fill bar |
| **Auto-refresh** | Silently refreshes every 10 seconds |
| **Confirm dialogs** | Safety prompts before destructive actions |
| **Toast notifications** | Non-blocking feedback for all actions |

---

[![Screenshot](https://raw.githubusercontent.com/cumulus13/recyclebinweb/master/screenshoot.png)](https://raw.githubusercontent.com/cumulus13/recyclebinweb/master/screenshoot.png)

---

## Prerequisites

- Windows 10 / 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download)

```powershell
dotnet --version   # must be 8.x.x
```

---

## Setup & Run

```powershell
cd RecycleBinWeb
dotnet restore
dotnet run
```

Browser opens automatically at **http://localhost:5051**

---

## Publish as standalone .exe

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -o .\publish
.\publish\RecycleBinWeb.exe
```

---

## Troubleshooting

**Empty bin shown but Windows shows items**
→ Run terminal as **Administrator** — some SID folders under `$Recycle.Bin` require elevated access.

**Restore fails with "Access denied"**
→ The original folder no longer exists or requires admin rights. Run as Administrator.

**Items from other user accounts not visible**
→ By design — each Windows user has their own SID folder. Run as that user or as Administrator.

## 👤 Author
        
[Hadi Cahyadi](mailto:cumulus13@gmail.com)
    

[![Buy Me a Coffee](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://www.buymeacoffee.com/cumulus13)

[![Donate via Ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/cumulus13)
 
[Support me on Patreon](https://www.patreon.com/cumulus13)