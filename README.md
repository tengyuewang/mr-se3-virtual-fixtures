# Toward In-Situ SE(3) Virtual Fixture Generation and Editing: A Mixed Reality-Driven Approach

**Tengyue Wang, Zhefan Lin, Leixin Chang, Songjie Xiao, Kaiwen Hong, Xianhao Chen, and Liangjing Yang**

*IEEE Robotics and Automation Letters*, accepted October 25, 2026.

This is the public project repository accompanying our work on mixed reality (MR)-driven generation and editing of full SE(3) virtual fixtures for physical human-robot interaction. The complete system combines MR-based motion specification, SE(3) path generation, interactive Laplacian editing, and robot-side virtual fixture enforcement through task-space impedance control.

**The repository is being released incrementally.** The Meta Quest 3 Unity project and its documentation are available now. Additional implementations, installable binaries, and supporting study materials will be uploaded in subsequent updates.

## Repository contents

```text
mr-se3-virtual-fixtures/
|-- README.md                       # Repository overview and release status
|-- LICENSE                         # License for original project code
`-- Unity-Quest3/                   # Meta Quest 3 Unity project
    |-- README.md                   # Unity setup, usage, and code guide
    |-- Assets/                     # C# scripts, scene, prefabs, and resources
    |-- Packages/                   # Unity dependencies and MRTK archives
    |-- ProjectSettings/            # Unity project configuration
    |-- Documentation/              # Detailed setup and architecture
    `-- THIRD_PARTY_NOTICES.md       # Third-party component notices
```

Only currently available content is shown above. Future components will be linked here when they are uploaded.

## Available now

| Component | What is included | Entry point |
| --- | --- | --- |
| Meta Quest 3 MR interface | Unity project with C# code for virtual-object interaction, pose sampling, SE(3) path generation and visualization, path editing, and Unity-side ROS communication. | [Unity-Quest3](Unity-Quest3/) |
| Unity project guide | Required versions, opening the project, APK build steps, ROS connection settings, parameters, and source-code guide. | [Unity README](Unity-Quest3/README.md) |
| Setup and architecture documentation | Current Quest deployment instructions, calibration options, coordinate frames, and Unity runtime data flow. | [Setup](Unity-Quest3/Documentation/SETUP.md) / [Architecture](Unity-Quest3/Documentation/ARCHITECTURE.md) |
| Validation utilities | Focused Edit Mode tests and Unity menu commands for project validation and APK building. | [Tests](Unity-Quest3/Assets/Tests/Editor/) / [Release tools](Unity-Quest3/Assets/Editor/ReleaseAudit.cs) |

To use the released Unity interface, follow the [Unity project README](Unity-Quest3/README.md). **Select `Unity-Quest3/` in Unity Hub**, rather than the repository root.

The paper's experiments used a **Meta Quest 3** and a **7-DoF Flexiv Rizon 4** manipulator. The current release contains the headset-side implementation; it does not yet include the complete robot-side system needed to reproduce the physical experiments. Generated C# ROS message classes and robot visualization assets are included, but they are not the ROS 2 workspace or robot controller.

## Release roadmap

The following materials will be added in subsequent updates. Items marked **To be added** are not currently available in this repository. No release dates have been set.

| Material | Status | Planned contents |
| --- | --- | --- |
| HoloLens 2 Unity project | **To be added** | An additional MR interface, including interaction, pose sampling, path generation, visualization, and editing. HoloLens 2 was not the headset used in the reported user study. |
| Prebuilt Meta Quest 3 APK | **To be added** | An installable application for direct deployment to the Quest 3. The current Unity project provides build instructions, but no prebuilt download is available yet. |
| Robot-side C++ implementation | **To be added** | Path deployment, virtual fixture enforcement, task-space impedance control, and the software interface for the Flexiv Rizon 4 used in the study. |
| ROS 2 packages | **To be added** | Packages and message definitions for exchanging generated paths, control parameters, and robot states between the Unity applications and robot-side modules. |
| Complete system documentation | **Partially available; to be extended** | Unity documentation is available. ROS 2 and robot build, launch, configuration, and end-to-end deployment instructions will accompany the remaining components. |
| Informed consent form | **To be added** | The participant-facing consent form used in the user study. |
| Participant study protocol | **To be added** | Familiarization, path-authoring and task-execution procedures, and condition-ordering instructions. |
| Study questionnaires | **To be added** | NASA-TLX, the System Usability Scale (SUS), and the open-ended feedback prompts administered to participants. |
| Remaining Unity authoring utilities | **Partially available; to be completed** | The status of additional operations described in the paper is documented in the [Unity guide](Unity-Quest3/README.md#scope-and-pending-utilities). |

The planned user-study materials are the forms, protocol, and questionnaire instruments. Participant-level recordings, raw questionnaire responses, and analysis scripts are not included in the current release. This README will be updated with the locations and download links of newly published components.

## Citation

If you use this work, please cite the accompanying paper. The entry below reflects the accepted manuscript; final DOI, volume, issue, and page information will be updated when available.

```bibtex
@article{wang2026mrse3vf,
  author  = {Wang, Tengyue and Lin, Zhefan and Chang, Leixin and Xiao, Songjie and Hong, Kaiwen and Chen, Xianhao and Yang, Liangjing},
  title   = {Toward In-Situ {SE(3)} Virtual Fixture Generation and Editing: A Mixed Reality-Driven Approach},
  journal = {IEEE Robotics and Automation Letters},
  year    = {2026},
  note    = {Accepted manuscript}
}
```

## License

The original project code is released under the [MIT License](LICENSE). Third-party components retain their own licenses; see the [Unity project's third-party notices](Unity-Quest3/THIRD_PARTY_NOTICES.md).
