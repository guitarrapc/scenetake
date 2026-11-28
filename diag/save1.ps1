# Written by scenetake: saves what this command changed in the shell, for the next command (ShellState.cs).
try {
    $__scenetake_base = $global:__scenetake_base
    $__scenetake_state = @{ Location = (Get-Location).Path; Environment = @{}; RemovedEnvironment = @(); Variables = @{}; Functions = @{}; Aliases = @{} }
    foreach ($__scenetake_item in Get-ChildItem env:) {
        if (-not $__scenetake_base.Environment.ContainsKey($__scenetake_item.Name) -or $__scenetake_base.Environment[$__scenetake_item.Name] -cne $__scenetake_item.Value) { $__scenetake_state.Environment[$__scenetake_item.Name] = $__scenetake_item.Value }
    }
    foreach ($__scenetake_name in $__scenetake_base.Environment.Keys) {
        if ($null -eq [Environment]::GetEnvironmentVariable($__scenetake_name)) { $__scenetake_state.RemovedEnvironment += $__scenetake_name }
    }
    $__scenetake_skip = @('LASTEXITCODE','Matches','StackTrace','_','PSItem','PWD','args','input','MyInvocation','PSBoundParameters','PSCmdlet','PSCommandPath','PSScriptRoot','this','foreach','switch','sender','EventArgs','Event','EventSubscriber','SourceArgs','SourceEventArgs')
    foreach ($__scenetake_item in Get-Variable -Scope Global) {
        $__scenetake_name = $__scenetake_item.Name
        if ($__scenetake_name -like '__scenetake*' -or $__scenetake_skip -contains $__scenetake_name) { continue }
        if ($__scenetake_item.Options -band [System.Management.Automation.ScopedItemOptions]::Constant) { continue }
        if ($__scenetake_base.Variables.ContainsKey($__scenetake_name)) {
            if ($__scenetake_name -notlike '*Preference' -or $__scenetake_base.Variables[$__scenetake_name] -ceq "$($__scenetake_item.Value)") { continue }
        }
        $__scenetake_state.Variables[$__scenetake_name] = $__scenetake_item.Value
    }
    foreach ($__scenetake_item in Get-ChildItem function:) {
        if (-not $__scenetake_base.Functions.ContainsKey($__scenetake_item.Name) -or $__scenetake_base.Functions[$__scenetake_item.Name] -cne $__scenetake_item.Definition) { $__scenetake_state.Functions[$__scenetake_item.Name] = $__scenetake_item.Definition }
    }
    foreach ($__scenetake_item in Get-Alias) {
        if (-not $__scenetake_base.Aliases.ContainsKey($__scenetake_item.Name) -or $__scenetake_base.Aliases[$__scenetake_item.Name] -cne $__scenetake_item.Definition) { $__scenetake_state.Aliases[$__scenetake_item.Name] = $__scenetake_item.Definition }
    }
    try {
        $__scenetake_state | Export-Clixml -LiteralPath '__ROOT__\1\state.xml' -Depth 4
    } catch {
        # A variable whose value cannot be written, such as a live connection: the rest still passes on.
        [IO.File]::AppendAllText('__ROOT__\1\restore.err', "could not pass on the variables of this step: $($_.Exception.Message)`n")
        $__scenetake_state.Variables = @{}
        $__scenetake_state | Export-Clixml -LiteralPath '__ROOT__\1\state.xml' -Depth 4
    }
    $__scenetake_lines = foreach ($__scenetake_name in $__scenetake_state.Environment.Keys) {
        [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($__scenetake_name)) + ' ' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes([string]$__scenetake_state.Environment[$__scenetake_name]))
    }
    [IO.File]::WriteAllLines('__ROOT__\1\exports.txt', [string[]]@($__scenetake_lines))
    # The folder, as text for the prompt's {dir}: the CLI reads no CliXml.
    [IO.File]::WriteAllText('__ROOT__\1\final.pwd', [string]$__scenetake_state.Location)
} catch {
    [IO.File]::AppendAllText('__ROOT__\1\restore.err', "could not save the shell state: $($_.Exception.Message)`n")
}