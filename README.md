# Slideshow

Macro Deck 3 plugin that displays random pictures from a folder on a button. Images change at a configurable interval; pressing the button skips to another image.

## Use

Install the plugin in Macro Deck 3. Macro Deck starts and authorizes it. Add the **Slideshow** action to a button and enter an image folder path **on the computer running Macro Deck**. Set **Seconds between images** (1–3600, default 10) and **Image size in pixels** (64–1024, default 512). These are per-button settings, so different slideshow buttons can have different speeds and sizes. Subfolders are included; PNG, JPEG and WebP files are supported. Install `ffmpeg` on that computer and ensure it is on `PATH` so the plugin can render button icons. The selected folder must be readable by Macro Deck. An empty or missing folder produces no image.

The plugin reads images in the chosen local folder and invokes local `ffmpeg` to convert them to button icons. It does not upload images or connect to external services. No account or token is needed for an installed plugin.

The plugin's icon and documentation were created with AI assistance. The running plugin does not use AI or generate AI content; it displays images you select.

To test before publishing, start Macro Deck, create a one-time token under **Developer Tools → Plugin tokens**, save it as `MacroDeck:Plugin:EnrollmentToken` in the **source project's .NET User Secrets**, and run the **Macro Deck - Real Host** debug profile in `src/ReviewedImageSlideshow/Properties/launchSettings.json`. Remove the token from User Secrets after pairing; subsequent debug launches reuse the ignored `.macrodeck-dev-state/`. Add two slideshow buttons with different folders, intervals and sizes. Verify each changes at its own interval, and pressing a button skips its image. Test an invalid folder and remove `ffmpeg` from `PATH` to confirm icons are unavailable rather than crashing the host. Store installations need no developer enrollment token.

## Build and publish

Requires the .NET 10 SDK and the [Macro Deck plugin CLI](https://docs.macro-deck.app/). The packaged plugin uses the .NET 10 runtime supplied by Macro Deck 3. Supported targets: Windows x64, macOS arm64, Linux x64 (with `ffmpeg` installed).

```bash
dotnet build
dotnet test
macrodeck-plugin test --project src/ReviewedImageSlideshow --report markdown --output conformance.md
macrodeck-plugin build --source src/ReviewedImageSlideshow --output ./artifacts
macrodeck-plugin inspect --artifact ./artifacts/com.yuziiv.slideshow-1.0.0.macroDeckPlugin
```

Publish the source as a public repository at [YuziIV/MacroDeck-slideshow](https://github.com/YuziIV/MacroDeck-slideshow). In the [Creator Portal](https://docs.macro-deck.app/creator-portal/publish-plugin/), create a **Plugin / Integration** Project with Package ID `com.yuziiv.slideshow`, then connect this repository under **Builds**. Publishing a GitHub release tagged `v1.0.0` runs `.github/workflows/release.yml`, which builds and uploads the plugin without a publishing secret. In the Portal, select the build, create a release, add it to a submission and submit for review. Check that the Portal owner shown for the Project matches `publisher.name` in the manifest. The Store requires passing conformance reports on every declared platform and a supported Macro Deck SDK. Generated artifacts and local debug state are ignored by Git; upload source, not `bin/`, `obj/` or a local credential.

**Before tagging a release**, run `dotnet list src/ReviewedImageSlideshow package` and compare the resolved `MacroDeck.Sdk` version with the [Store minimum](https://api.macro-deck.app/api/v1/public/dependency-policy/sdk). As of September 2026 the public feed resolves `3.0.0-preview.10`, below the required `3.0.0-beta.12`; do not submit that build.

The installed package is run and authorized by Macro Deck. The `Properties/launchSettings.json` profile is only for interactive developer debugging against a locally running host; it is not used by store installs.

## License

MIT. See [LICENSE](LICENSE).
