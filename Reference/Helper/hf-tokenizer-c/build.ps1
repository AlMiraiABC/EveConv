param(
    [Parameter(Mandatory = $false, Position = 0)]
    [string]$Profile = "release",
    [Parameter(Mandatory = $false)]
    [string[]]$Targets = @(
        # windows
          "x86_64-pc-windows-gnu"
        # , "i686-pc-windows-gnu"
        #, "aarch64-pc-windows-gnullvm"
        # linux(glibc)
        , "x86_64-unknown-linux-gnu"
        , "i686-unknown-linux-gnu"
        , "armv7-unknown-linux-gnueabihf"
        , "aarch64-unknown-linux-gnu"
        , "loongarch64-unknown-linux-gnu"
        # linux(musl)
        # , "x86_64-unknown-linux-musl"
        # , "i686-unknown-linux-musl"
        # , "armv7-unknown-linux-musleabihf"
        # , "aarch64-unknown-linux-musl"
        # , "loongarch64-unknown-linux-gnu"
    ),
    [switch]$Clean = $false,
    [bool]$Build = $true,
    [bool]$Bindgen = $true,
    [bool]$Copy = $true,
    [string]$CopyDist = "../EveConv.HuggingFaceFastTokenizer/dll/"
)

if ($Clean) {
    cargo clean;
}

# build on host to get csbindgen.
if ($Bindgen) {
    cargo build --$Profile;
}

# build for targets
if ($Build) {
    foreach ($target in $Targets) {
        Write-Information "Building for target: $target"
        cross build --$Profile --target $target;
        Write-Information "Finished building for target: $target"
    }
}

$currentDir = Get-Location
$artfExts = @(".dll", ".so", ".dylib")
# copy artifacts
if ($Copy) {
    foreach ($target in $Targets) {
        $ProfilePath = Join-Path $currentDir "target/$target/$Profile"
        if (-not (Test-Path $ProfilePath)) {
            Write-Warning "Build failed of target $target."
            continue
        }
        $artifacts = Get-ChildItem -Path $ProfilePath -File | Where-Object { $_.Name -like "*hf_tokenizers_c.*" -and $_.Extension -in $artfExts } -ErrorAction SilentlyContinue
        if (-not $artifacts) {
            Write-Warning "Build failed of target $target."
            continue
        }
        $destination = Join-Path $currentDir "../EveConv.HuggingFaceFastTokenizer/dll/$target"
        if (-not (Test-Path $destination)) {
            New-Item -ItemType Directory -Path $destination -Force | Out-Null
        }
        foreach ($artifact in $artifacts) {
            Copy-Item -Path $artifact.FullName -Destination $destination -Force
        }
        Write-Information "Copied $($artifacts.Count) artifacts to $destination"
    }
}
