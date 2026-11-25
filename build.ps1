param([string] $OutputPath = (Join-Path $PSScriptRoot 'build\GameAlmanac.exe'))

$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'src'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path -LiteralPath (Join-Path $framework 'csc.exe'))) {
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
}
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The Windows .NET Framework C# compiler is required.'
}

$output = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($output)) | Out-Null
$references = @(
    'System.Xaml.dll',
    'System.Windows.Forms.dll',
    'WPF\WindowsBase.dll',
    'WPF\PresentationCore.dll',
    'WPF\PresentationFramework.dll'
)
$sources = @(
    'Program.cs', 'Domain.cs', 'Sqlite.cs', 'LibraryLocation.cs', 'LibraryStore.cs',
    'Ui.cs', 'Dialogs.cs', 'StorageWindow.cs', 'MainWindow.cs', 'DetailWindow.cs'
)
$arguments = @('/nologo', '/utf8output', '/codepage:65001', '/optimize+', '/target:winexe', '/platform:anycpu', '/main:GameAlmanac.Program')
$arguments += "/out:$output"
$arguments += "/resource:$(Join-Path $source 'Theme.xaml'),GameAlmanac.Theme.xaml"
$arguments += $references | ForEach-Object { "/reference:$(Join-Path $framework $_)" }
$arguments += $sources | ForEach-Object { Join-Path $source $_ }

& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'GameAlmanac build failed.' }
Write-Output "Built $output"
