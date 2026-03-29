# Enabling Gesture-Based Control for Immersive Visualizations in CAVE Environments using Touch Tables

> **Master's Thesis** — University of Passau, 2026  
> **Author:** Wassim Sellami

## Overview

This project presents a gesture-based interaction system that uses a large-format multi-touch table to provide direct, intuitive control over 3D anatomical models displayed in a CAVE (Cave Automatic Virtual Environment) immersive display. The system bridges the gap between indirect 2D input and 3D spatial visualization, targeting medical education and anatomy teaching scenarios.

The work was conducted as a Master's thesis in Computer Science at the University of Passau and addresses four research questions covering gesture vocabulary design, system architecture and performance, interaction model reliability, and overall usability.

## Motivation

CAVE systems provide excellent visual immersion for complex 3D medical data, but their interaction has traditionally been governed by mouse-and-keyboard input. This forces users to constantly translate between indirect 2D input and 3D spatial reasoning, reducing immersion and drawing attention away from the anatomical content. This system replaces that paradigm with a natural, touch-based gesture interface on a large multi-touch table acting as a direct controller for the CAVE.

## System Architecture

The system follows a **client–server architecture** — both sides are built in **Unity (C#)**:

- **Touch Table Client — Android Scene:** A Unity project built and deployed as an Android application on the large-format multi-touch table. It captures raw touch events, performs local gesture recognition, and sends high-level gesture commands to the CAVE server over a persistent **WebSocket** connection. A local proxy mesh provides instant visual feedback to mask network latency.
- **CAVE Server — Cube Scene:** A Unity project running on the CAVE server machine. It is the authoritative source of truth for the 3D scene. Receives gesture commands, updates the scene graph, and renders the high-fidelity 3D models across all CAVE display walls.

This separation of concerns keeps network bandwidth low (only compact command identifiers and transform data are transmitted) while achieving the strict performance targets required for real-time immersive interaction.

## Gesture Vocabulary

The gesture set was designed through a systematic, referent-based process informed by multi-touch conventions, cognitive load research, and conflict-avoidance principles. A core finger-count mapping principle was applied:

| Finger Count | Category |
|---|---|
| 1 finger | Rotational control (Orbit, Continuous Spin) |
| 2 fingers | Scaling (Zoom in/out) |
| 3–5 fingers | Translation and special operations (Pan, Roll) |

### Full Gesture Table

| Operation | Gesture | Fingers | Type | Conflict Risk |
|---|---|---|---|---|
| Rotate / Orbit | 1-finger drag on model | 1 | Natural | Medium |
| Pan / Move | 3 or 4-finger drag | 3–4 | Natural | Low |
| Roll | 5-finger rotation twist | 5 | Learned | Low |
| Zoom In/Out | 2-finger pinch/spread | 2 | Natural | Low |
| Continuous Spin | 1-finger fast swipe → tap to stop | 1 | Natural/Learned | Medium |
| Snap to Preset View | Double-tap right/left screen half | 1×2 | Learned | Low |
| Slice (Cross-Section) | Long press outside model + drag | 1 | Learned | Medium |
| Destroy / Remove | Long press on selected part | 1 | Learned | Medium |
| Model Selection/Import | Quick flick / File picker UI | 1 | Natural/Learned | Low |
| System Controls (Reset, Undo, Axis, Density) | On-screen UI buttons/sliders | — | Learned | Low |

> Potential conflict between long-press gestures (Slice vs. Destroy) was mitigated by distinct touch targets (outside model vs. on model part) combined with clear visual feedforward (scissors / trash-can icons).

## Key Features

- **Direct model manipulation:** Gestures transform the model itself while the CAVE viewpoint stays fixed, creating a strong sense of tangibility.
- **Real-time volumetric rendering:** A custom GPU ray-marching shader handles volumetric datasets. Slicing is achieved by updating shader clipping plane properties (`PlanePos`, `PlaneNormal`) directly on the GPU — avoiding costly mesh regeneration.
- **Volumetric Density Control:** Dual sliders stream Hounsfield unit thresholds directly to the GPU shader, allowing real-time isolation of anatomical features (e.g., stripping soft tissue to reveal bone).
- **Undo/Redo History:** Implemented using the Command design pattern. Each operation is encapsulated as a command object; the server maintains a history stack for reversible actions.
- **Runtime Model Import:** Users can import polygonal and volumetric datasets (`.raw`, `.dat`) at runtime via a native file picker. Metadata and base64-encoded thumbnails are stored in a persistent local JSON registry.
- **Physics-based model loading:** Models are loaded by dragging their thumbnail and flicking it into the workspace.
- **Graceful failure handling:** WebSocket `OnClose`/`OnError` events automatically transition the UI back to the connection screen with an informative message.
- **Three-stage interaction flow:** Connection Screen → Model Selection Screen → Interaction Screen, reducing cognitive load at each step.

## Performance

Evaluated under typical operating conditions (N = 2,666 latency samples; N = 1,765 frame-rate samples):

| Metric | Mean | Min | Max | Target |
|---|---|---|---|---|
| End-to-end latency | 13.4 ms | 6.5 ms | 146.2 ms* | < 40 ms |
| Frame rate | 89.8 FPS | 37.4 FPS** | 103.8 FPS | 90 FPS |

*Peak latency occurred only during initial model loading.  
**Minimum FPS occurred only during computationally intensive volumetric slicing operations (synchronous hull mesh generation). Baseline performance consistently met the 90 Hz target.

## User Study Results

Nine participants (university students, mixed technical/domain backgrounds) completed 10 scripted interaction tasks covering the full gesture vocabulary.

- **Mean task completion time:** ~3 minutes for all 10 tasks
- **Gesture naturalness ratings** (1 = hard/unnatural, 10 = instinctive):

| Operation | Mean Rating | Notes |
|---|---|---|
| Continuous Spin | 9.67 | Most satisfying and fluid interaction |
| Destroy / Remove | 9.67 | On-screen menus prevented confusion with Slice |
| Rotate / Orbit | 9.44 | Very natural; users noted needing to lift finger to change axis |
| Slice | 9.44 | Clear execution; users wanted to physically drag sliced halves apart |
| Preset View | 9.28 | Highly satisfying for snapping to angles |
| Zoom In/Out | 9.22 | Universally understood |
| Load Model (Flick) | 9.22 | Satisfying physics metaphor |
| Pan / Move | 8.83 | Lowest rated; harder to execute consistently on large surface |

**Overall naturalness averaging > 8.8 / 10** across all gestures.

The anticipated conflict between Slice and Destroy long-press mechanics did **not** materialize: 7 out of 9 users explicitly reported no confusion between the two.

## Tech Stack

| Component | Technology |
|---|---|
| CAVE Server (Cube Scene) | Unity (C#) — desktop/server build |
| Touch Table Client (Android Scene) | Unity (C#) — Android build |
| Network Communication | WebSocket (persistent connection) |
| Volumetric Rendering | Custom GLSL/HLSL ray-marching shader |
| Mesh Slicing | EzySlice (open-source Unity library) |
| State Management Pattern | Command Pattern (Undo/Redo) |
| Data Serialization | JSON |

## Repository Structure

```
.
├── Assets/                          # Unity project assets (shared — contains both scenes)
├── Packages/                        # Unity package manifest
├── ProjectSettings/                 # Unity project settings
├── Touch Screen Controller_.../     # Additional touch table client resources
├── apk/                             # Pre-built APK for the Android scene (touch table)
├── websockettest_.../               # WebSocket test utilities
├── .gitignore
└── .vsconfig
```

## Getting Started

### Prerequisites

- **Unity** (for both scenes)
- A large-format multi-touch display (touch table) running Android
- A CAVE display system (or a standard monitor for development/testing)
- Both devices on the same local network

### Running the System

1. **Start the CAVE Server (Cube Scene):** Open the Unity project, switch to the **Cube Scene**, build and run it on the CAVE server machine. Note the machine's IP address.
2. **Deploy the Touch Table Client (Android Scene):** Switch to the **Android Scene**, build an Android APK and install it on the touch table device (or use the pre-built APK from `apk/`).
3. **Connect:** On the touch table, enter the CAVE server's IP address on the Connection Screen and tap Connect.
4. **Select a Model:** Choose a 3D anatomical model from the gallery or import a new one.
5. **Interact:** Use the gesture vocabulary to manipulate the model on the CAVE display.

## Research Questions Addressed

| RQ | Question | Outcome |
|---|---|---|
| RQ1 | How to design a comprehensive, intuitive, non-conflicting gesture vocabulary for CAVE-based 3D manipulation using a large multi-touch table? | 9-gesture vocabulary covering 11 operations; avg. naturalness 8.8/10 |
| RQ2 | How can the client–server architecture meet strict performance requirements (latency, update rate)? | Mean latency 13.4 ms (target < 40 ms); mean frame rate 89.8 FPS (target 90 FPS) |
| RQ3 | Which design principles for gesture definition, feedback, and conflict avoidance lead to a reliable recognition pipeline? | Temporal + spatial thresholds; visual feedforward; finger-count mapping; no confusion reported between Slice/Destroy |
| RQ4 | How usable and learnable is the system for typical users manipulating complex 3D medical models? | 10 tasks completed in ~3 min average; all gestures rated above 8.8/10 for naturalness |

## Citation

If you use this work, please cite:

```
Sellami, W. Enabling Gesture-Based Control for Immersive Visualizations in CAVE Environments
using Touch Tables. Master's Thesis, University of Passau, 2026.
https://github.com/WassimSellami/Enabling-Gesture-Based-Control-for-Immersive-Visualizations-in-CAVE-Environments-using-Touch-Tables
```

## License

This project was developed as part of a Master's thesis at the University of Passau. Please contact the author for licensing and usage inquiries.
