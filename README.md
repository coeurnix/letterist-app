# Letterist

Letterist is a Windows app for comic lettering. It is built for artists, letterers, translators, and small publishing teams who want a tool that stays focused on the actual job: bringing dialogue, captions, sound effects, and page structure together cleanly on finished art.

Download the latest release from [github.com/coeurnix/letterist-app/releases/latest](https://github.com/coeurnix/letterist-app/releases/latest).

<video src="https://raw.githubusercontent.com/coeurnix/letterist-app/main/letterist-small.mp4" controls muted playsinline width="960"></video>

[Open the demo video directly](https://raw.githubusercontent.com/coeurnix/letterist-app/main/letterist-small.mp4)

## Who It's For

Letterist is made for people who letter comics for real: solo creators, webcomic artists, manga makers, translators, editors, and anyone preparing pages for digital release or print. If you have ever tried to do comic lettering in a general-purpose design tool and wished the software understood balloons, tails, captions, templates, and page exports, this is the app.

## What It Does

- Build documents around real comic pages, with multi-page support, layers, guides, and panel-aware workflow.
- Place and edit dialogue balloons, captions, thoughts, bursts, whispers, and text-only sound effects.
- Shape tails, attach lettering to panels, and keep page composition readable while you work.
- Style text the way comics need it: all caps, inline emphasis, outlines, multiple strokes, gradients, shadows, glow, warping, and text paths.
- Reuse work with balloon styles, text styles, style libraries, balloon templates, and panel layout templates.
- Support translation and localization passes with a document model that handles replaced text cleanly, including CJK-friendly font support.
- Export pages for delivery in the formats creators actually need, including image exports, PDF, and comic archive workflows.
- Stay scriptable and testable through the built-in automation server, which makes Letterist practical for repeatable production pipelines.

## Getting Started

1. Download the latest Windows release from [github.com/coeurnix/letterist-app/releases/latest](https://github.com/coeurnix/letterist-app/releases/latest).
2. Unzip the release folder anywhere you like.
3. Run `Letterist.exe`. No installer is required.
4. Create a document, load your page art, place balloons and captions, then export the finished page set.

## Building From Source

Most people should use the packaged release. If you want to build Letterist yourself, you will need Windows, Visual Studio 2022 with `.NET desktop development`, and the .NET 9 SDK.

```powershell
.\build.ps1
.\release.ps1
```

The source, issue tracker, and releases all live at [github.com/coeurnix/letterist-app](https://github.com/coeurnix/letterist-app).
