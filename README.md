# 🏛️ Interactive Museum Mixed Reality (Museum Exploration MR)

[![Unity 2022.3 LTS](https://img.shields.io/badge/Unity-2022.3.39f1%20LTS-black?style=for-the-badge&logo=unity&logoColor=white)](https://unity.com/)
[![Meta XR SDK](https://img.shields.io/badge/Meta%20XR%20SDK-v201.0.0-0668E1?style=for-the-badge&logo=meta&logoColor=white)](https://developer.oculus.com/)
[![Meta MRUK](https://img.shields.io/badge/MRUK-MR%20Utility%20Kit-blueviolet?style=for-the-badge)](https://developer.oculus.com/documentation/unity/unity-mr-utility-kit-overview/)
[![Platform](https://img.shields.io/badge/Platform-Meta%20Quest%202%20%7C%20Pro%20%7C%203-0081FB?style=for-the-badge&logo=oculus&logoColor=white)](https://www.meta.com/quest/)
[![Render Pipeline](https://img.shields.io/badge/Render%20Pipeline-URP%2014.0-FF4088?style=for-the-badge)](https://unity.com/srp/universal-render-pipeline)
[![Input](https://img.shields.io/badge/Input-XR%20Hands%20%26%20Touch%20Controllers-success?style=for-the-badge)](https://docs.unity3d.com/Packages/com.unity.xr.hands@1.8/manual/index.html)

> An immersive Mixed Reality (MR) cultural heritage application designed for **Meta Quest 3, Quest Pro, and Quest 2**.  
> Transforming physical spaces into interactive living museum galleries celebrating the rich historical legacy of **Lembaga Muzium Negeri Terengganu** (Terengganu State Museum, Malaysia).

---

## 📖 Table of Contents

- [🌟 Project Overview](#-project-overview)
- [✨ Key Features](#-key-features)
- [🏛️ Virtual Galleries & Artifacts](#️-virtual-galleries--artifacts)
- [📜 Sejarah Terengganu & "Live Photo" Exhibit](#-sejarah-terengganu--live-photo-exhibit)
- [🎮 Educational Mini-Games & Leaderboard](#-educational-mini-games--leaderboard)
- [⌚ Wearable Wrist Watch HUD](#-wearable-wrist-watch-hud)
- [📷 Spatial QR Code Scanner & Integration](#-spatial-qr-code-scanner--integration)
- [🎨 Design & Theming (Glassmorphic Aesthetics)](#-design--theming-glassmorphic-aesthetics)
- [🛠️ System Architecture & Codebase Overview](#️-system-architecture--codebase-overview)
- [🖥️ In-Editor Curator Tools](#️-in-editor-curator-tools)
- [🚀 Getting Started & Installation](#-getting-started--installation)
- [⌨️ Editor Simulation & Debug Controls](#️-editor-simulation--debug-controls)
- [📦 Technical Specifications & Dependencies](#-technical-specifications--dependencies)
- [🤝 Cultural Heritage Attribution & Acknowledgements](#-cultural-heritage-attribution--acknowledgements)

---

## 🌟 Project Overview

**Museum Exploration MR** brings cultural heritage to life by blending physical environments with augmented 3D historical artifacts, interactive educational timelines, and multimodal archival media. 

Visitors wearing a Meta Quest headset can walk freely in their physical surroundings (or an actual museum exhibition hall) via high-fidelity **stereoscopic color passthrough**. As they explore, spatial QR markers and diegetic wrist interactions anchor digital exhibition rooms, interactive 3D replicas, Malay and English voice narrations, and cultural mini-games directly into the room.

```
       [ Physical Environment / Museum Hall ]
                         │
        (High-Definition Color Passthrough)
                         ▼
        [ Meta MRUK Scene & Spatial Trackables ]
                         │
   ┌─────────────────────┼─────────────────────┐
   ▼                     ▼                     ▼
[ 3D Artifacts ]    [ Sejarah Panels ]    [ Wrist Watch HUD ]
• 360° Rotation     • "Live Photo" Clip   • Gallery Chooser
• Narration Audio   • Story Narration     • Mini-Games Menu
• Acoustic Sounds   • Multi-panel Layout  • Dynamic Themes
```

---

## ✨ Key Features

### 🕶️ Stereoscopic Passthrough & Spatial Anchoring
- **High-Fidelity Passthrough**: Keeps users safe and aware of their physical surroundings while overlaying digital exhibits.
- **Meta MRUK Scene Tracking**: Real-time integration with Meta XR MR Utility Kit for anchor trackables, surface alignment, and spatial awareness.
- **Smart Staggered Placement**: Spawns floating exhibition cards directly in front of the visitor at eye level, automatically offsetting multiple open panels horizontally so displays never overlap.

### ✋ Natural Hand Tracking & 6DoF Controller Modality
- **Dual Modality Support**: Powered by `Unity XR Hands (1.8.0)` and `XR Interaction Toolkit (2.5.4)`. Seamless runtime switching between direct hand tracking and 6DoF Meta Touch controllers via `HandModalityForcer`.
- **Intuitive Gesture Control**: Pinch-to-select buttons, pinch-and-drag to rotate 3D artifacts freely in mid-air, and hand-ray reticles for distant interactions.
- **Interactive Onboarding Tutorial (`TutorialManager`)**: Step-by-step tutorial with animated 3D ghost hands (`TutorialGestureGizmo`) teaching pinch, drag, and rotation gestures with audio guidance (`TutorialAudioFeedback`).

### ⌚ Diegetic Wearable Wrist Watch HUD (`WristWatch`)
- Attached directly to the visitor's left wrist joint via `XRHandSubsystem` joint poses.
- Raising the wrist reveals a floating diegetic interface for:
  - **Ruang / Room Chooser**: Jump between virtual exhibition galleries.
  - **Sejarah / History Timeline**: Browse historical events and stories.
  - **Mini-Games**: Launch cultural quizzes and challenges.
  - **Theme Toggle**: Switch between Dark and Light museum UI themes on the fly.
  - **Glanceable Status**: Real-time clock and battery status.

### 🖼️ Multimodal Artifact Inspector (`Artifact.cs`)
- **Interactive 3D Inspector**: Spawns high-detail 3D models with 360-degree rotation and scaling.
- **High-Resolution Photo Gallery**: Image carousel showcasing archival museum photography.
- **Bilingual Audio Narration**: Narrations in **Bahasa Melayu** and **English** with audio scrubbers and play/pause controls.
- **Authentic Acoustic Audio**: Dedicated audio triggers for musical artifacts (e.g. authentic traditional royal Gamelan recordings).

### 🎬 "Live Photo" Historical Video Transitions (`HistoryPanel.cs`)
- Select any of the **7 Sejarah Terengganu** exhibition topics.
- **Live Photo Interaction**: Long-pinching/holding down on an archival historical photograph smoothly transitions into a 3-5 second animated archival video clip before elegantly reverting to the static photo upon release.

### 🎯 Cultural Mini-Games with Firestore Cloud Leaderboard
- **Game 1: Tebak Bayangan Artefak** (Silhouette 3D Guessing): Rotating 3D silhouette inspection where players identify artifacts against timed multiple-choice rounds.
- **Game 2: Susun Langkah & Kisah** (Sequential Ordering):
  - *Batik Crafting*: Sequence the 5 authentic stages of Batik Canting Tulis (wax waxing, coloring, background dyeing, boiling, drying).
  - *Historical Timelines*: Chronologically order events for Terengganu economic and royal milestones.
- **Competitive Leaderboard (`LeaderboardPanel.cs`)**: Persisted local high scores via `PlayerPrefs` and cloud synchronization via **Firebase Firestore REST API**.

### 📱 Physical QR Code System (`QRCodeScanner.cs`)
- Scan physical QR tags in an exhibition room using Meta Quest cameras.
- Instantly triggers corresponding room waypoints, artifact models, and mini-games.
- Includes a full Unity Editor simulation mode (`QRCodeScannerDebugger`) for rapid testing without wearing a headset.

---

## 🏛️ Virtual Galleries & Artifacts

The exhibition is categorized into 4 distinct thematic galleries based on the collections of Lembaga Muzium Negeri Terengganu:

| Gallery | Name | Description | Featured Artifacts |
|:---|:---|:---|:---|
| **Galeri 1** | **Galeri Tekstil** | Traditional Terengganu weaving, royal batik, and dyeing traditions. | • **Batik Canting Tulis**<br>• **Kain Pelangi**<br>• **Kain Songket** |
| **Galeri 2** | **Galeri Seni** | Performing arts, traditional theatre, and royal music heritage. | • **Gamelan Terengganu** (with authentic audio)<br>• **Wayang Kulit** (Shadow Puppet) |
| **Galeri 3** | **Serambi Mandalika** | Epigraphy, Islamic heritage, and historical inscriptions. | • **Batu Bersurat Terengganu** (1303 AD UNESCO Memory of the World) |
| **Galeri 4** | **Galeri Kraf** | Royal weaponry, brass casting, and traditional artisanal craft. | • **Keris Terengganu**<br>• **Barangan Tembaga Acuan Kayu**<br>• **Tudung Saji Besar** |

Each artifact is accompanied by a 3D model prefab, historical time period, dimensions, material information, high-res photo gallery, and narration audio.

---

## 📜 Sejarah Terengganu & "Live Photo" Exhibit

The Sejarah Terengganu wing features 7 comprehensive historical topics, each equipped with archival photos, narrated voiceovers in Bahasa Melayu, and "Live Photo" video clips:

1. **Asal Usul "Taring Anu"**: Legends and etymological origins of the name Terengganu.
2. **Tragedi Megat Panji Alam**: The heroic and tragic epic of Megat Panji Alam.
3. **Zaman Penjajahan**: The eras of colonial treaties, Siam relations, and foreign administration.
4. **Rombongan Kelantan**: Historical diplomatic and royal delegations between Terengganu and Kelantan.
5. **Pemberontakan Tani (1928)**: The peasant resistance movement led by Haji Abdul Rahman Limbong.
6. **Infrastruktur & Pembangunan**: The modernization of communication, bridges, transport, and coastal administration.
7. **Ekonomi Tradisional**: Mining, boat building (*perahu besar*), maritime trade, and fishing industries.

---

## 🎮 Educational Mini-Games & Leaderboard

```
┌─────────────────────────────────────────────────────────────┐
│                    MINI-GAMES & LEADERBOARD                 │
├──────────────────────────────┬──────────────────────────────┤
│ 🎮 Game 1: Tebak Bayangan    │ 🎮 Game 2: Susun Langkah     │
│ • Inspect rotating 3D black  │ • Drag & arrange cards in    │
│   silhouette in real space   │   chronological sequence     │
│ • Identify correct artifact  │ • Round 1: Batik Canting     │
│ • Instant feedback & reveal  │ • Rounds 2-5: Sejarah Events │
├──────────────────────────────┴──────────────────────────────┤
│ 🏆 Leaderboard System:                                      │
│ • Live speedrun timer (mm:ss)                               │
│ • Local storage (PlayerPrefs) + Firebase Firestore REST     │
└─────────────────────────────────────────────────────────────┘
```

---

## ⌚ Wearable Wrist Watch HUD

The `WristWatch.cs` system provides a diegetic mixed reality interface pinned to the user's left wrist:
- **Joint-Tracking Anchoring**: Tracks the `XRHandSubsystem` wrist joint when hand tracking is active, and the left controller transform when using controllers.
- **Fixed-Scale Locking**: Enforces fixed physical dimensions so hand distance doesn't scale UI elements.
- **Smart Debouncing**: Eliminates accidental double-clicks caused by simultaneous UI pointer and physical collider triggers.
- **Context-Aware Visibility**: Automatically hides during intro video and tutorial steps so visitors stay focused.

---

## 📷 Spatial QR Code Scanner & Integration

The application bridges physical museum displays with mixed reality:

1. **Physical Museum Mode**: Curators place printed QR codes next to physical artifacts. The Meta Quest headset detects the QR code (`Meta.XR.MRUtilityKit.MRUKTrackable`), calculates its 3D pose, and anchors the digital panel and 3D model beside the physical pedestal.
2. **Standalone Mode**: Visitors can browse all rooms and artifacts directly from the Wrist Watch HUD without physical markers.
3. **Editor Simulation**: Developers can simulate scans in the Unity Editor using hotkeys without needing a physical headset or printed codes.

---

## 🎨 Design & Theming (Glassmorphic Aesthetics)

The user interface was built to evoke a modern, elegant museum exhibition feel using custom shaders and styling:

- **Dual Theme Support (`ThemeManager.cs`)**:
  - **Dark Mode**: Frosted glassmorphism (`GlassUI.shader`) with deep navy/black backgrounds, translucent panels, and subtle purple/gold accents.
  - **Light Mode (Olive-Sage)**: Authentic museum Olive-Sage (`#90937E`) translucent glass panels with luminous soft borders (`#C5D0B2`) and warm neutral subcards (`#747968`).
- **Typography**: Refined classical museum aesthetics using the **Cardo** serif typeface paired with clean sans-serif UI elements.
- **Audio Scenery (`BGMManager.cs`, `UIButtonAudio.cs`)**: Gentle ambient background music and soft acoustic click feedback.

---

## 🛠️ System Architecture & Codebase Overview

```
Assets/
├── Asset/
│   ├── Artifak Photo/           # Archival museum photography & exhibit images
│   ├── Audio/
│   │   ├── BGM/                 # Ambient background music
│   │   ├── Narrator/            # Narration audio (Bahasa Melayu & English)
│   │   └── SFX/                 # UI click and gesture feedback sounds
│   ├── MR Icon/                 # Custom UI vector icons & menu glyphs
│   └── Video/                   # Historical 3-5s clips for Live Photo exhibits
├── Prefabs/
│   ├── ArtifactPanelPrefab      # Floating 3D artifact inspector panel
│   ├── GameOptionsPrefab        # Mini-games host container
│   ├── model_artifact_*         # 3D optimized artifact models
│   └── RoomListButton           # Dynamic gallery chooser buttons
├── Resources/
│   └── MuseumData/
│       ├── Artifacts/           # ArtifactData ScriptableObjects
│       ├── DataSejarah/         # HistoryData ScriptableObjects
│       └── Rooms/               # RoomData ScriptableObjects
└── Script/
    ├── Core/
    │   ├── ArtifactManager.cs   # Global artifact spawning & lifecycle controller
    │   ├── RoomManager.cs       # Exhibition gallery loading & waypoint system
    │   ├── HistoryManager.cs    # Sejarah panels & multi-panel manager
    │   ├── MainMenu.cs          # Video intro, name entry & passthrough transition
    │   ├── ThemeManager.cs      # Dark/Light theme coordinator
    │   ├── TutorialManager.cs   # Interactive onboarding gesture sequence
    │   ├── QRCodeScanner.cs     # MRUK QR code detector & event dispatcher
    │   └── BGMManager.cs        # Audio atmosphere manager
    ├── Data/
    │   ├── ArtifactData.cs      # ScriptableObject schema for artifacts
    │   ├── HistoryData.cs       # ScriptableObject schema for history exhibits
    │   └── RoomData.cs          # ScriptableObject schema for galleries
    ├── Interactions/
    │   ├── Artifact.cs          # Artifact UI panel interaction controller
    │   ├── RotateArtifact.cs    # Direct pinch/drag 3D rotation logic
    │   ├── HandModalityForcer.cs# Hands <-> Controllers runtime arbiter
    │   └── PinchClickStep.cs    # Tutorial gesture practice components
    ├── MiniGames/
    │   ├── BaseGame.cs          # Abstract base class (timer, lifecycle, scores)
    │   ├── Game1GuessName.cs    # Silhouette 3D identification game
    │   └── Game2OrderProcess.cs # Batik & historical timeline sequence game
    ├── UI/
    │   ├── WristWatch.cs        # Diegetic wrist-attached HUD
    │   ├── HistoryPanel.cs      # "Live Photo" media player & story panel
    │   ├── HistoryListPanel.cs  # 2-column Sejarah topic grid chooser
    │   ├── LeaderboardPanel.cs  # High score panel (Local & Firestore API)
    │   └── HandRayReticle.cs    # Custom reticle visual for distant pointing
    └── Editor/
        ├── MuseumDataManagerWindow.cs # Visual CMS for rooms, artifacts & QR codes
        ├── QRCodeGeneratorEditor.cs   # Standalone QR generator window
        └── TutorialPanelBuilder.cs    # Scene hierarchy setup utility
```

---

## 🖥️ In-Editor Curator Tools

The project includes custom Unity Editor tools accessible under the **Tools > Museum MR** menu:

### 1. Room & Artifact Data Manager (`Tools > Museum MR > Room & Artifact Data Manager`)
A visual content management window for museum curators and developers:
- Create, edit, and organize rooms and artifacts without touching code or raw YAML files.
- Assign 3D models, photos, narration clips, and acoustic instrument tracks.
- One-click batch generation of high-resolution QR codes mapped to IDs.

### 2. QR Code Generator (`Tools > Museum MR > QR Code Generator`)
- Generates high-resolution PNG QR codes for any room ID (`room_1`, `room_2`) or artifact ID (`artifact_batik`, etc.).
- Saves directly to the project's `QRCodes/` folder for immediate printing on exhibition placards.

---

## 🚀 Getting Started & Installation

### Prerequisites
- **Unity 2022.3.39f1 LTS** (or higher within 2022.3 LTS).
- **Android Build Support** (with Android SDK & NDK tools installed via Unity Hub).
- **Meta Quest 2, Quest Pro, or Quest 3** headset connected via USB-C or Meta Quest Link.
- **Meta Quest Developer Mode** enabled on the headset.

### Setup Instructions

1. **Clone the Repository**:
   ```bash
   git clone https://github.com/wilymanhoward/Museum-Exploration-MR.git
   ```

2. **Open in Unity Hub**:
   - Launch Unity Hub.
   - Click **Add** and select the cloned project folder.
   - Ensure Editor version is set to **2022.3.39f1**.

3. **Build Settings & Platform Switch**:
   - Go to **File > Build Settings**.
   - Select **Android** and click **Switch Platform** (Texture Compression: ASTC).
   - Ensure the main scene `Assets/Scenes/1.unity` is checked in the build list.

4. **Verify XR Plug-in Management**:
   - Go to **Edit > Project Settings > XR Plug-in Management**.
   - Under Android tab, verify **OpenXR** is checked.
   - Ensure the **Meta Quest Support** feature group is enabled.

5. **Deploy to Meta Quest**:
   - Connect your headset via USB-C.
   - In **Build Settings**, select your Quest headset under **Run Device**.
   - Click **Build And Run**.

---

## ⌨️ Editor Simulation & Debug Controls

Test the entire application in the Unity Editor without putting on a headset using built-in simulation hotkeys:

| Key | Simulated Action |
|:---|:---|
| `1` | Scan Galeri Tekstil (`room_textile`) |
| `2` | Scan Galeri Kraf (`room_craft`) |
| `3` | Scan Serambi Mandalika (`room_mandalika`) |
| `4` | Scan Galeri Seni (`room_art`) |
| `5` | Scan Ruang Sejarah (`room_history`) |
| `6` | Scan Batik Canting (`artifact_batik`) |
| `7` | Scan Kain Songket (`artifact_sutera`) |
| `8` | Scan Batu Bersurat Terengganu (`artifact_batu`) |
| `9` | Launch Game 1: Tebak Bayangan Artefak (`game_1`) |
| `G` | Launch Game 2: Susun Langkah & Kisah (`game_2`) |
| `0` | Launch Game 3 (`game_3`) |
| `X` | Simulate QR code lost / walk away |

---

## 📦 Technical Specifications & Dependencies

| Package | Version | Purpose |
|:---|:---|:---|
| **com.meta.xr.sdk.core** | 201.0.0 | Meta Quest core runtime & passthrough layers |
| **com.meta.xr.mrutilitykit** | 201.0.0 | Scene understanding, spatial anchors, and QR trackables |
| **com.unity.render-pipelines.universal** | 14.0.11 | High-performance mobile VR graphics rendering (URP) |
| **com.unity.xr.hands** | 1.8.0 | Native hand tracking subsystem & joint poses |
| **com.unity.xr.interaction.toolkit** | 2.5.4 | Spatial UI interactors, rays, and gesture handling |
| **com.unity.xr.openxr** | 1.11.0 | OpenXR standard runtime interface for Quest |
| **com.unity.ugui** | 1.0.0 | World-space Canvas UI and TextMeshPro typography |

---

## 🤝 Cultural Heritage Attribution & Acknowledgements

- **Lembaga Muzium Negeri Terengganu (Terengganu State Museum)**: Historical artifacts, archival narratives, and cultural documentation.
- **UNESCO Memory of the World**: *Batu Bersurat Terengganu* historical inscription records.
- **Jabatan Warisan Negara Malaysia**: Traditional craft preservation and intangible cultural heritage documentation.

---

<div align="center">
  <sub>Preserving heritage through spatial computing. Built with ❤️ for Meta Quest Mixed Reality.</sub>
</div>
