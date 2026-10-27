# Third-party notices

This Unity project contains or resolves third-party software. Paths below are relative to `Unity-Quest3`. Each component remains subject to its own license. This file is an inventory, not a replacement for the license text distributed with each package. The license for original project code is in [../LICENSE](../LICENSE).

| Component | Source in this project | License / notice location |
| --- | --- | --- |
| Unity packages and engine modules | `Packages/manifest.json` | Unity package metadata and Unity terms |
| Meta XR SDK and MR Utility Kit | Unity Package Manager | Package metadata supplied by Meta |
| Mixed Reality Toolkit (MRTK) packages | Referenced archives in `Packages/MixedReality` | `package/LICENSE.md` inside each archive |
| Math.NET Numerics 5.0.0 | `Assets/Packages/MathNet.Numerics.5.0.0` | MIT; declared in `MathNet.Numerics.nuspec` |
| Unity ROS-TCP Connector | Git dependency pinned in `Packages/manifest.json` | Repository/package license at the pinned revision |
| Unity URDF Importer | Git dependency pinned in `Packages/manifest.json` | Repository/package license at the pinned revision |
| Flexiv robot description and meshes | `Assets/URDF/rizon/flexiv_description` | Apache-2.0; declared in `package.xml` |

Before a public release, the maintainers should verify that all redistributed binary assets permit redistribution, retain required copyright notices, and add any license texts required by their terms.
