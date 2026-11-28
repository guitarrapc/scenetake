# Written by scenetake: notes the shell as it started, then restores the state of the commands before this one (ShellState.cs).
$global:__scenetake_base = @{ Variables = @{}; Functions = @{}; Aliases = @{}; Environment = @{} }
foreach ($__scenetake_item in Get-Variable -Scope Global) { $global:__scenetake_base.Variables[$__scenetake_item.Name] = $(if ($__scenetake_item.Name -like '*Preference') { "$($__scenetake_item.Value)" } else { $null }) }
foreach ($__scenetake_item in Get-ChildItem function:) { $global:__scenetake_base.Functions[$__scenetake_item.Name] = $__scenetake_item.Definition }
foreach ($__scenetake_item in Get-Alias) { $global:__scenetake_base.Aliases[$__scenetake_item.Name] = $__scenetake_item.Definition }
foreach ($__scenetake_item in Get-ChildItem env:) { $global:__scenetake_base.Environment[$__scenetake_item.Name] = $__scenetake_item.Value }
Remove-Variable -Name __scenetake_item -ErrorAction SilentlyContinue
try {
    $__scenetake_state = Import-Clixml -LiteralPath '__ROOT__\powershell-state.xml'
    try { Set-Location -LiteralPath $__scenetake_state.Location -ErrorAction Stop } catch { [IO.File]::AppendAllText('__ROOT__\2\restore.err', "could not return to $($__scenetake_state.Location): it no longer exists or cannot be entered`n") }
    foreach ($__scenetake_name in $__scenetake_state.Environment.Keys) { [Environment]::SetEnvironmentVariable($__scenetake_name, $__scenetake_state.Environment[$__scenetake_name]) }
    foreach ($__scenetake_name in $__scenetake_state.RemovedEnvironment) { [Environment]::SetEnvironmentVariable($__scenetake_name, [NullString]::Value) }
    foreach ($__scenetake_name in $__scenetake_state.Variables.Keys) { Set-Variable -Scope Global -Name $__scenetake_name -Value $__scenetake_state.Variables[$__scenetake_name] -Force -ErrorAction SilentlyContinue }
    foreach ($__scenetake_name in $__scenetake_state.Functions.Keys) { Set-Item -LiteralPath "function:global:$__scenetake_name" -Value ([scriptblock]::Create($__scenetake_state.Functions[$__scenetake_name])) -ErrorAction SilentlyContinue }
    foreach ($__scenetake_name in $__scenetake_state.Aliases.Keys) { Set-Alias -Scope Global -Name $__scenetake_name -Value $__scenetake_state.Aliases[$__scenetake_name] -Force -ErrorAction SilentlyContinue }
} catch {
    [IO.File]::AppendAllText('__ROOT__\2\restore.err', "could not restore the shell state: $($_.Exception.Message)`n")
}
Remove-Variable -Name __scenetake_state, __scenetake_name -ErrorAction SilentlyContinue