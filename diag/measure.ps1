# Times how scenetake starts Windows PowerShell and pwsh for one step, piece by piece.
param([int]$Repeat = 3)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$root = Join-Path ([IO.Path]::GetTempPath()) "scenetake-state-diag"
Remove-Item -Recurse -Force $root -ErrorAction Ignore
$null = New-Item -ItemType Directory "$root\1", "$root\2"
$utf8 = [Text.UTF8Encoding]::new($false)
foreach ($n in 1, 2) {
    foreach ($f in 'restore', 'save') {
        [IO.File]::WriteAllText("$root\$n\$f.ps1", ([IO.File]::ReadAllText("$here\$f$n.ps1")).Replace('__ROOT__', $root), $utf8)
    }
    [IO.File]::WriteAllText("$root\$n\command.ps1", "`$plain = 'v'; Write-Output hello", $utf8)
    Copy-Item "$here\run.ps1" "$root\$n\run.ps1"
    # run.ps1 without the parse of command.ps1.
    [IO.File]::WriteAllText("$root\$n\run-noparse.ps1",
        ". ([scriptblock]::Create([IO.File]::ReadAllText(`$__scenetake_dir + '/restore.ps1'))); `$__scenetake_ok = `$null; . ([scriptblock]::Create([IO.File]::ReadAllText(`$__scenetake_dir + '/command.ps1') + `"``n```$__scenetake_ok = ```$?`"))`n" +
        ". ([scriptblock]::Create([IO.File]::ReadAllText(`$__scenetake_dir + '/save.ps1'))); if (`$__scenetake_ok -eq `$false) { exit 1 }", $utf8)
}

$prefix = 'try { [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new() } catch { }; '
function Get-Variants([string]$dir) {
    $lit = "'" + $dir.Replace("'", "''") + "'"
    [ordered]@{
        'exit'        = 'exit 0'
        'old'         = $prefix + ". ([scriptblock]::Create([IO.File]::ReadAllText($lit + '\restore.ps1'))); `$__scenetake_ok = `$false; try { `$plain = 'v'; Write-Output hello`n`$__scenetake_ok = `$? } finally { . ([scriptblock]::Create([IO.File]::ReadAllText($lit + '\save.ps1'))) }; if (-not `$__scenetake_ok) { exit 1 }"
        'new'         = $prefix + "`$__scenetake_dir = $lit; . ([scriptblock]::Create([IO.File]::ReadAllText(`$__scenetake_dir + '/run.ps1')))"
        'new-noparse' = $prefix + "`$__scenetake_dir = $lit; . ([scriptblock]::Create([IO.File]::ReadAllText(`$__scenetake_dir + '/run-noparse.ps1')))"
        'new-inline'  = $prefix + "`$__scenetake_dir = $lit; " + [IO.File]::ReadAllText("$dir\run.ps1")
        'parse-only'  = $prefix + "`$t = [IO.File]::ReadAllText($lit + '\command.ps1'); `$a = [System.Management.Automation.Language.Parser]::ParseInput(`$t, [ref]`$null, [ref]`$null); 'parsed'"
        'restore'     = $prefix + ". ([scriptblock]::Create([IO.File]::ReadAllText($lit + '\restore.ps1')))"
        'restore+save'= $prefix + ". ([scriptblock]::Create([IO.File]::ReadAllText($lit + '\restore.ps1'))); . ([scriptblock]::Create([IO.File]::ReadAllText($lit + '\save.ps1')))"
    }
}

function Invoke-Shell([string]$exe, [string]$text) {
    $psi = [Diagnostics.ProcessStartInfo]::new($exe)
    foreach ($a in '-NoLogo', '-NoProfile', '-Command', $text) { $psi.ArgumentList.Add($a) }
    $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true; $psi.RedirectStandardInput = $true; $psi.UseShellExecute = $false
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $p = [Diagnostics.Process]::Start($psi)
    $p.StandardInput.Close()
    $out = $p.StandardOutput.ReadToEndAsync(); $err = $p.StandardError.ReadToEndAsync()
    if (-not $p.WaitForExit(120000)) { $p.Kill($true); return [pscustomobject]@{ Ms = 120000; Code = 'timeout'; Out = ''; Err = '' } }
    $p.WaitForExit()
    [pscustomobject]@{ Ms = $watch.ElapsedMilliseconds; Code = $p.ExitCode; Out = $out.Result.Trim(); Err = $err.Result.Trim() }
}

$shells = @(
    @{ Name = 'powershell'; Exe = "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" }
    @{ Name = 'pwsh'; Exe = (Get-Command pwsh -ErrorAction Ignore).Source }
) | Where-Object { $_.Exe -and (Test-Path $_.Exe) }

foreach ($shell in $shells) {
    "== $($shell.Name): $($shell.Exe)"
    foreach ($step in 1, 2) {
        $dir = "$root\$step"
        if ($step -eq 2) {
            if (-not (Test-Path "$root\1\state.xml")) { "   no state.xml from step 1"; continue }
            Copy-Item "$root\1\state.xml" "$root\powershell-state.xml" -Force
        }
        foreach ($entry in (Get-Variants $dir).GetEnumerator()) {
            $runs = foreach ($i in 1..$Repeat) { Invoke-Shell $shell.Exe $entry.Value }
            $ms = ($runs.Ms | Sort-Object)
            $first = $runs[0]
            "   step{0} {1,-13} ms={2,-24} code={3} out={4} err={5}" -f $step, $entry.Key, ($ms -join ','), $first.Code, ($first.Out -replace '\s+', ' '), ($first.Err -replace '\s+', ' ').Substring(0, [Math]::Min(160, $first.Err.Length))
        }
    }
}
