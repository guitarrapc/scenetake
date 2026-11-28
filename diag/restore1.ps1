# Written by scenetake: notes the shell as it started, then restores the state of the commands before this one (ShellState.cs).
$global:__scenetake_base = @{ Variables = @{}; Functions = @{}; Aliases = @{}; Environment = @{} }
foreach ($__scenetake_item in Get-Variable -Scope Global) { $global:__scenetake_base.Variables[$__scenetake_item.Name] = $(if ($__scenetake_item.Name -like '*Preference') { "$($__scenetake_item.Value)" } else { $null }) }
foreach ($__scenetake_item in Get-ChildItem function:) { $global:__scenetake_base.Functions[$__scenetake_item.Name] = $__scenetake_item.Definition }
foreach ($__scenetake_item in Get-Alias) { $global:__scenetake_base.Aliases[$__scenetake_item.Name] = $__scenetake_item.Definition }
foreach ($__scenetake_item in Get-ChildItem env:) { $global:__scenetake_base.Environment[$__scenetake_item.Name] = $__scenetake_item.Value }
Remove-Variable -Name __scenetake_item -ErrorAction SilentlyContinue
