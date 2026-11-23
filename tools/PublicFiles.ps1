# SPDX-License-Identifier: Apache-2.0
function Get-PublicProjectFiles([string]$ProjectRoot) {
    $names = @('README.md','README.en.md','README.zh-CN.md','VERSION','LICENSE','NOTICE','ASSET-LICENSE.md','CONTRIBUTING.md','CONTRIBUTING.zh-CN.md','SECURITY.md','SECURITY.zh-CN.md','CHANGELOG.md','CHANGELOG.zh-CN.md','.gitignore','.gitattributes','.editorconfig','build.ps1')
    $names += @('tools/BuildOriginalCharacters.cs','tools/BuildOriginalCharacters.Custom.cs','tools/PrepareCompanionRig.cs','tools/BuildCompanionReview.py','tools/BuildAppIcon.cs','tools/PublicFiles.ps1','tools/TestRepository.ps1','tools/ExportOpenSource.ps1')
    $names += @('tests/smoke-test.ps1','tests/integration-task-test.ps1','tests/IntegrationTaskTests.cs','tests/desktop-visibility-test.ps1','tests/DesktopVisibilityTests.cs','tests/icon-shortcut-test.ps1','tests/IconShortcutTests.cs','tests/claude-background-test.ps1','tests/ClaudeBackgroundTests.cs')
    $names += @('tests/original-assets-test.ps1','tests/companion-motion-test.ps1','tests/CompanionMotionTests.cs','tools/WalkingPreview.js')
    $names += @('tests/reminder-performance-test.ps1','tests/ReminderPerformanceTests.cs','tools/BuildReminderPreviews.cs','tools/BuildReminderPreviews.ps1')
    $names += @('tools/BuildReadmeMedia.cs','tools/BuildProductMedia.cs','tools/BuildReadmeMedia.ps1','tests/release-source-test.ps1')
    $names += @('tests/startup-registration-test.ps1','tests/StartupRegistrationTests.cs')
    $names += @('tests/profile-settings-test.ps1','tests/ProfileSettingsTests.cs')
    $names += @('tests/installer-configuration-test.ps1','tests/InstallerConfigurationTests.cs')
    $names += @('tests/workshop-test.ps1','tests/WorkshopTests.cs')
    $names += @('tests/chat-bubble-test.ps1','tests/ChatBubbleTests.cs')
    $names += @('tests/chat-drag-test.ps1','tests/ChatDragTests.cs')
    $names += @('tests/hall-scene-install-test.ps1','tests/HallSceneTests.cs')
    $names += @('tests/hall-layout-test.ps1','tests/HallLayoutTests.cs')
    $names += @('tests/artifact-travel-test.ps1','tests/ArtifactTravelTests.cs')
    $names += @('tests/drag-gait-test.ps1','tests/DragGaitTests.cs')
    $names += @('tools/CharacterPackTool.cs','tools/ValidateCharacterPack.ps1','tools/ExportCharacterPack.ps1','tests/character-pack-test.ps1','tests/CharacterPackTests.cs')
    $names += @('tools/BuildWukongPack.py','tools/PreviewWukongPack.py','tests/WukongImportTests.cs')
    $names += @('tests/quick-team-task-test.ps1','tests/QuickTeamTaskTests.cs','tests/quick-task-test.ps1','tests/QuickTaskTests.cs','tests/QuickTaskStub.cs')
    $names += @('tests/quick-task-shell-test.ps1','tests/QuickTaskShellTests.cs')
    $names += @('tools/BuildShellPackage.py','tools/BuildShellExtension.ps1','tests/workshop-presentation-test.ps1','tests/WorkshopPresentationTests.cs')
    $names += @('tools/BuildBrand.py','tools/BuildBrandOverview.py')
    $names += @('tests/DefaultCompanionTests.cs','tests/default-companions-test.ps1','tests/AboutPresentationTests.cs','tests/about-presentation-test.ps1')
    $names += @('tools/RebuildCompanionPack.ps1')
    $names += @('tests/WalkingArmLayersTests.cs','tests/walking-arm-layers-test.ps1')
    $names += @('tests/native-shell-test.ps1','tests/NativeShellTests.cs','tests/NativeQuickTaskStub.cs')
    $names += @('tests/NativeShellContextFixture.cpp')
    foreach ($name in $names) {
        $file = Join-Path $ProjectRoot $name
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw ('Required public file missing: '+$name) }
        Get-Item -LiteralPath $file -Force
    }
    foreach ($folder in @('src/DesktopTasks','docs','.github','art')) {
        Get-ChildItem -LiteralPath (Join-Path $ProjectRoot $folder) -Recurse -File -Force |
            Where-Object { $_.FullName.Substring($ProjectRoot.Length + 1) -notmatch '(?:^|[\\/])(?:bin|obj)(?:[\\/]|$)' }
    }
}
