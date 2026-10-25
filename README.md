# 💠 ReFanS v1.2.3

<p align="center">
  <b>Modern Reference & Moodboard Curator for Creators, Musicians, and Game Developers</b><br/>
  <i>Created by <b>KeibaBoy5503</b></i>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows%2010%2F11%20(x64)-007ACC?style=for-the-badge&logo=windows" />
  <img src="https://img.shields.io/badge/.NET-10.0%20WPF-512BD4?style=for-the-badge&logo=dotnet" />
 
>  Latest version : v1.2.3

---

## 🌐 Overview (概要 / 소개)

**ReFanS** is an all-in-one visual board, audio & video player, typography, and reference file management desktop application built with WPF (.NET 10). It combines high-speed masonry layouts, real-time audio/video scrubbing, hierarchical color-sorted tagging.

* **🇯🇵 日本語 (Default):** 音楽・効果音・画像・動画・フォント・URL・文書キュレーションカードなどを視覚的に整理・タグ・分類できるイメージボード＆リファレンス管理ツールです。
* **🇰🇷 한국어:** 음악, 효과음, 이미지/비디오, 폰트, URL, 문서, 큐레이션 카드 등을 한곳에서 시각적으로 정리, 태그하고 분류할 수 있는 레퍼런스 관리 프로그램입니다.

---

## ✨ Key Features (主な機能 / 핵심 기능)

### 1. 🖼️ Masonry Imageboard & Multi-Format Lightbox
* **Masonry Grid & Curation View:** Switch seamlessly between curation cards and borderless Imageboard mode.
* **Instant Lightbox (`Space` / `Double-Click`):** Preview high-resolution images, animated GIFs, videos (`MP4`/`WebM`), PDF documents (`WebView2`), and formatted Markdown (`.md`) notes.
* **Editing Workspace (`Space in Lightbox`):** You can provide a detailed description or profile of the reference on a large screen.
* **Free Manual Reordering:** Drag and drop cards directly within the gallery (in Manual Sort mode) to arrange your moodboard in any exact order, or drag them into left-hand folders.

### 2. 🎧 Media Preview & Apple Music Metadata Integration
* **Media Preview Player:** Play media files ( `MP3, WAV, MP4 )` on the central image board. Also possible to play larger on the lightbox screen.
* **Audio Dual Waveform:** Automatically renders Peak + RMS waveforms for `.wav` and `.mp3` files with click-and-drag instant frame scrubbing.
* **Apple Music / iTunes API Search:** Automatically fetch high-res album artwork, release years, composer metadata, and 30-second audio previews.

### 3. 📁 Interactive Hierarchy & 100-Color Palette & RGB Picker
* **Smart Folder Dragging:** Drag folders to reorder(top/bottom edges) or drop directly onto the center of another folder to nest it as a subfolder.
* **10x10 Curated Palette + 2D RGB Spectrum:** Customize folder icons, folder text, and hierarchical tags (`Parent/Subtag`) using 100 presets (Vivid, Pastel, Gray Tone) or the live RGB spectrum picker.
* **Library Classification:** Library automatically sorts files by file type.

### 4. 🏷️ Tag, Filter Manager & Bookmark, File Lock
* **Tag Drag&Drop:** By dragging the tags on the left into the file, you can quickly apply tags.
* **Tag Managing:** Can Manage tags’ name, color, and hierarchy in a single window.
* **Bookmark & File Lock:** You can bookmarks and lock files using the button at the top right.
( Folder locking is also available ). 
Prevents files or folders from being modified

### 5. 🔍 Advanced Search & Image color extraction
* **Advanced Search:** You can search by selecting Specific element ( `folder / title / tags / file type / creation date` ) using the checkboxes at the bottom of the search bar.
* **Image color extraction:** Extract and display the five representative colors of the image file.
Each color code can be copied to the clipboard and is tagged in the filter list for management.

### 6. 📸 Screen & Region Snipping Tool
* **Full-Screen (`📷`) & Region Crop (`✂️`) Capture:** Automatically minimizes the window, captures the screen or custom dragged region, extracts dominant color swatches, and registers the reference immediately.
* **Thumbnail Switching:** Supports quickly changing thumbnails using ones copied to the clipboard.

### 7. 🎨 Custom Themes & 3 Languages (Persistent Preferences)
* **4 Default Themes:** `Default Dark`, `Gray Tone`, `Pure White` (with auto-contrast text), and `Pastel Purple`.
* **Custom Themes:** Supports a skin feature that colors from a palette, RGB map, or choose a background from an image file.
* **Languages:** **日本語 (Default)**, **한국어**, **English**. All UI labels, context menus, dialogs, sort states, and view modes are automatically saved and restored on launch.

### 8. 🗛 URL Viewer & Automatic Font Detection
* **URL Viewer:** Enter the URL by adding it using the + button at the top center, then press Space to enter zoom mode and access the website.
* **Automatic Font Detection:** Automatically detects the fonts in document files to identify which fonts are used.


---

## ⌨️ Keyboard & Mouse Shortcuts (ショートカット / 단축키)

| Action | Shortcut / Gesture |
| :--- | :--- |
| **Open Lightbox Preview** | `Space` or `Double-Click` on card |
| **Open Full Focus Editor** | Press `Space` again inside Lightbox |
| **Multi-Select Lasso (Marquee)** | Drag on empty gallery space (`Ctrl + Click` to toggle, `Ctrl + A` for All) |
| **Reorder Cards / Move to Folder** | Drag card onto another card (Manual Sort) or onto a Left Sidebar Folder |
| **Nest Folder into Subfolder** | Drag folder onto the **center** of target folder (`Drop on Top/Bottom` to reorder) |
| **Zoom Card Size** | `Ctrl + Mouse Wheel` |
| **Delete / Permanent Delete** | `Delete` (Move to Trash) / `Shift + Delete` (Immediate Delete) |

---

## 🚀 Installation & Patch Guide (インストールと更新 / 설치 및 패치 방법)

1. Download the latest release ZIP from the **[Releases](../../releases)** page and extract it.
2. Run **`ReFanS.exe`** (Self-contained single executable — no additional `.NET` installation required).
3. **Updating (Patching) to a Newer Version:**
   * All your data (`database.json`, `folders.json`, `custom_tags.json`, `user_prefs.json`, and `Images/` & `Media/` folders) are stored separately from the executable.
   * When a new patch is released, simply **overwrite `ReFanS.exe`** in your existing folder (or keep your Custom Workspace Directory in Settings) and all your data will remain intact!

---
<p align="center">Designed & Developed by <b>KeibaBoy5503</b></p>
<p align="center">Copyright  <b>KeibaBoy5503</b>. All rights reserved.</p>
