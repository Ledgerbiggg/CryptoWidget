# 写 version.json 的 notes 更新说明（发布流程用）。
# 用法: powershell -File scripts/update_version.ps1 -NotesFile .\notes.txt
# 说明: PS5.1 无 System.Text.Json，用原生 JSON + .NET 写入 UTF-8 无 BOM（PS 会把非 ASCII 转 \uXXXX，
#       CI 端 build.yml 已用 Python json.load 读取会自动还原，不再乱码）
param(
    [Parameter(Mandatory = $true)][string]$NotesFile
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot
$vj = Join-Path $repo 'version.json'
$prj = Join-Path $repo 'CryptoWidget.Shell\CryptoWidget.Shell.csproj'

if (-not (Test-Path $NotesFile)) { Write-Error "notes 文件不存在: $NotesFile"; exit 1 }
if (-not (Test-Path $vj)) { Write-Error "version.json 不存在: $vj"; exit 1 }

# 版本号唯一真相源是 csproj 的 <Version>
$c = Get-Content -Path $prj -Raw -Encoding utf8
$m = [regex]::Match($c, '<Version>(.*?)</Version>')
if (-not $m.Success) { Write-Error '未在 csproj 中找到 <Version>'; exit 1 }
$nv = $m.Groups[1].Value

# notes 从文件读取（保留换行与 Unicode），对象方式更新字段后整体序列化
$notes = [System.IO.File]::ReadAllText($NotesFile, [System.Text.Encoding]::UTF8)
$obj = Get-Content $vj -Raw -Encoding UTF8 | ConvertFrom-Json
$obj.version = $nv
$obj.notes = $notes
$out = $obj | ConvertTo-Json -Depth 5
# UTF-8 无 BOM 写入（PS5.1 的 Set-Content -Encoding utf8 带 BOM，必须用 .NET 写入）
[System.IO.File]::WriteAllText($vj, $out, [System.Text.UTF8Encoding]::new($false))

Write-Host "[update_version] version=$nv notes=$($notes.Length) chars -> version.json (UTF-8 no BOM)"
