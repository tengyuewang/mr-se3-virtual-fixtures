# Toward In-Situ SE(3) Virtual Fixture Generation and Editing: A Mixed Reality-Driven Approach

This is the public project repository accompanying our work on mixed reality (MR)-driven generation and editing of full SE(3) virtual fixtures for physical human-robot interaction. The paper describes a system combining MR-based motion specification, SE(3) path generation, interactive Laplacian editing, and robot-side virtual fixture enforcement through task-space impedance control.

**This repository provides the Meta Quest 3 MR interface and supporting study materials:** the Unity project, its setup and architecture documentation, a prebuilt Quest 3 APK, and the user-study consent form, protocol, and questionnaires.

**Prebuilt Quest 3 APK:** [Download version 1.1.0](https://github.com/tengyuewang/mr-se3-virtual-fixtures/releases/download/quest3-apk-2026-11-11/mr-se3-virtual-fixtures.apk) | [Release notes and SHA-256 checksum](https://github.com/tengyuewang/mr-se3-virtual-fixtures/releases/tag/quest3-apk-2026-11-11)

## Repository contents

```text
mr-se3-virtual-fixtures/
|-- README.md                       # Repository overview and release status
|-- LICENSE                         # License for original project code
|-- Supplement/                     # User-study materials (PDF)
|   |-- NASA-TLX_and_SUS_Questionnaire.pdf
|   |-- Participant_Information_and_Consent.pdf
|   `-- Participant_Study_Protocol.pdf
`-- Unity-Quest3/                   # Meta Quest 3 Unity project
    |-- README.md                   # Unity setup, usage, and code guide
    |-- Assets/                     # C# scripts, scene, prefabs, and resources
    |-- Packages/                   # Unity dependencies and MRTK archives
    |-- ProjectSettings/            # Unity project configuration
    |-- Documentation/              # Detailed setup and architecture
    `-- THIRD_PARTY_NOTICES.md       # Third-party component notices
```

The folders above contain the published source, documentation, and study materials. The prebuilt APK is hosted separately under GitHub Releases.

## Available now

| Component | What is included | Entry point |
| --- | --- | --- |
| Meta Quest 3 MR interface | Unity project with C# code for virtual-object interaction, pose sampling, SE(3) path generation and visualization, path editing, and Unity-side ROS communication. | [Unity-Quest3](Unity-Quest3/) |
| Prebuilt Meta Quest 3 APK | Available: version 1.1.0, built November 11, 2026, for ARM64. The 104.05 MiB APK is hosted as a GitHub Release asset. | [Download APK](https://github.com/tengyuewang/mr-se3-virtual-fixtures/releases/download/quest3-apk-2026-11-11/mr-se3-virtual-fixtures.apk) / [Installation guide](Unity-Quest3/Documentation/SETUP.md#prebuilt-apk) |
| Unity project guide | Required versions, opening the project, APK build steps, ROS connection settings, parameters, and source-code guide. | [Unity README](Unity-Quest3/README.md) |
| Setup and architecture documentation | Current Quest deployment instructions, calibration options, coordinate frames, and Unity runtime data flow. | [Setup](Unity-Quest3/Documentation/SETUP.md) / [Architecture](Unity-Quest3/Documentation/ARCHITECTURE.md) |
| Validation utilities | Focused Edit Mode tests and Unity menu commands for project validation and APK building. | [Tests](Unity-Quest3/Assets/Tests/Editor/) / [Release tools](Unity-Quest3/Assets/Editor/ReleaseAudit.cs) |
| Participant information and consent | Participant-facing study information and a blank informed consent form. | [Consent PDF](Supplement/Participant_Information_and_Consent.pdf) |
| Participant study protocol | Equipment, study conditions, familiarization, path-authoring, task execution, and questionnaire procedures. | [Protocol PDF](Supplement/Participant_Study_Protocol.pdf) |
| Study questionnaires | Blank NASA-TLX and System Usability Scale (SUS) forms, with open-ended feedback prompts. | [Questionnaire PDF](Supplement/NASA-TLX_and_SUS_Questionnaire.pdf) |

To install the interface directly on a headset, follow the [prebuilt APK instructions](Unity-Quest3/Documentation/SETUP.md#prebuilt-apk); Unity Editor is not required. To work with the source, follow the [Unity project README](Unity-Quest3/README.md) and **select `Unity-Quest3/` in Unity Hub**, rather than the repository root.

## License

The original project code is released under the [MIT License](LICENSE). Third-party components retain their own licenses; see the [Unity project's third-party notices](Unity-Quest3/THIRD_PARTY_NOTICES.md).
