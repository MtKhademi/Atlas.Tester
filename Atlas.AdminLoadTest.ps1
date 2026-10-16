# Local-only Windows admin/load test helper. It uses the public HTTP API and never touches the database.
# Run with: powershell.exe -Sta -ExecutionPolicy Bypass -File .\Atlas.AdminLoadTest.ps1

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$script:CreatedUsers = [System.Collections.Generic.List[object]]::new()
$script:CreatedRoles = [System.Collections.Generic.List[object]]::new()
$script:Token = $null

function New-Label([string]$text, [int]$x, [int]$y) {
    $label = [Windows.Forms.Label]::new()
    $label.Text = $text; $label.Location = [Drawing.Point]::new($x, $y); $label.AutoSize = $true
    return $label
}

function New-TextBox([string]$value, [int]$x, [int]$y, [int]$width = 300, [bool]$password = $false) {
    $box = [Windows.Forms.TextBox]::new()
    $box.Text = $value; $box.Location = [Drawing.Point]::new($x, $y); $box.Width = $width
    if ($password) { $box.UseSystemPasswordChar = $true }
    return $box
}

function Write-Log([string]$message) {
    $log.AppendText("$(Get-Date -Format 'HH:mm:ss')  $message`r`n")
    $log.SelectionStart = $log.TextLength; $log.ScrollToCaret()
    [Windows.Forms.Application]::DoEvents()
}

function Get-Headers {
    if (-not $script:Token) { throw 'Login first.' }
    return @{ Authorization = "Bearer $script:Token" }
}

function Test-Target([string]$baseUrl) {
    $uri = [Uri]$baseUrl
    if ($uri.Host -notin @('localhost', '127.0.0.1', '::1') -and -not $allowRemote.Checked) {
        throw 'Enable Allow remote target for a non-local destination.'
    }
}

$form = [Windows.Forms.Form]::new()
$form.Text = 'Atlas Admin Load Test (Development Only)'; $form.Size = [Drawing.Size]::new(760, 620)
$form.StartPosition = 'CenterScreen'

$form.Controls.Add((New-Label 'API URL' 20 20)); $baseUrl = New-TextBox 'http://localhost:5000' 150 16 430; $form.Controls.Add($baseUrl)
$form.Controls.Add((New-Label 'Admin email' 20 55)); $email = New-TextBox '' 150 51 300; $form.Controls.Add($email)
$form.Controls.Add((New-Label 'Admin password' 20 90)); $password = New-TextBox '' 150 86 300 $true; $form.Controls.Add($password)
$form.Controls.Add((New-Label 'User count' 20 125)); $count = New-TextBox '100' 150 121 80; $form.Controls.Add($count)
$form.Controls.Add((New-Label 'Concurrency' 250 125)); $concurrency = New-TextBox '10' 340 121 80; $form.Controls.Add($concurrency)
$allowRemote = [Windows.Forms.CheckBox]::new(); $allowRemote.Text = 'Allow remote target (use only in a test environment)'; $allowRemote.Location = [Drawing.Point]::new(150, 155); $allowRemote.AutoSize = $true; $form.Controls.Add($allowRemote)

$loginButton = [Windows.Forms.Button]::new(); $loginButton.Text = '1. Login'; $loginButton.Location = [Drawing.Point]::new(20, 195); $loginButton.Width = 110; $form.Controls.Add($loginButton)
$createButton = [Windows.Forms.Button]::new(); $createButton.Text = '2. Create users'; $createButton.Location = [Drawing.Point]::new(140, 195); $createButton.Width = 130; $form.Controls.Add($createButton)
$loadButton = [Windows.Forms.Button]::new(); $loadButton.Text = '3. Run load'; $loadButton.Location = [Drawing.Point]::new(280, 195); $loadButton.Width = 110; $form.Controls.Add($loadButton)
$cleanupButton = [Windows.Forms.Button]::new(); $cleanupButton.Text = '4. Disable created'; $cleanupButton.Location = [Drawing.Point]::new(400, 195); $cleanupButton.Width = 140; $form.Controls.Add($cleanupButton)
$roleButton = [Windows.Forms.Button]::new(); $roleButton.Text = 'Create custom roles'; $roleButton.Location = [Drawing.Point]::new(550, 195); $roleButton.Width = 150; $form.Controls.Add($roleButton)

$log = [Windows.Forms.TextBox]::new(); $log.Multiline = $true; $log.ScrollBars = 'Vertical'; $log.ReadOnly = $true; $log.Location = [Drawing.Point]::new(20, 245); $log.Size = [Drawing.Size]::new(700, 315); $form.Controls.Add($log)

$loginButton.Add_Click({
    try {
        Test-Target $baseUrl.Text.Trim()
        $response = Invoke-RestMethod "$($baseUrl.Text.TrimEnd('/'))/iam/api/auth/login" -Method Post -ContentType 'application/json' -Body (@{ emailOrPhone = $email.Text; password = $password.Text } | ConvertTo-Json)
        $script:Token = $response.accessToken
        Write-Log 'Admin login succeeded.'
    } catch { Write-Log "Login error: $($_.Exception.Message)" }
})

$roleButton.Add_Click({
    try {
        $headers = Get-Headers; $script:CreatedRoles.Clear(); $prefix = "custom.load.$([DateTime]::UtcNow.ToString('MMddHHmmss'))"
        $definitions = @(
            @{ Suffix = 'readonly'; Name = 'Load Readonly'; Permissions = @('dashboard.read') },
            @{ Suffix = 'users'; Name = 'Load Users'; Permissions = @('users.read', 'users.create') },
            @{ Suffix = 'oracle'; Name = 'Load Oracle'; Permissions = @('oracle.operations.read', 'oracle.operations.manage') },
            @{ Suffix = 'system'; Name = 'Load System'; Permissions = @('system.read', 'dashboard.read') },
            @{ Suffix = 'operator'; Name = 'Load Operator'; Permissions = @('dashboard.read', 'users.read', 'roles.read', 'oracle.operations.read') }
        )
        foreach ($definition in $definitions) {
            $request = @{ key = "$prefix.$($definition.Suffix)"; displayName = $definition.Name; permissionKeys = $definition.Permissions }
            $role = Invoke-RestMethod "$($baseUrl.Text.TrimEnd('/'))/admin/api/roles" -Method Post -Headers $headers -ContentType 'application/json' -Body ($request | ConvertTo-Json)
            $script:CreatedRoles.Add($role)
            Write-Log "Created role $($role.key) with $($definition.Permissions.Count) permissions."
        }
        Write-Log "Created $($script:CreatedRoles.Count) custom roles."
    } catch { Write-Log "Role creation error: $($_.Exception.Message)" }
})

$createButton.Add_Click({
    try {
        $total = [int]$count.Text; if ($total -lt 1 -or $total -gt 1000) { throw 'Count must be between 1 and 1000.' }
        $headers = Get-Headers; $script:CreatedUsers.Clear(); $prefix = "atlas-load-$([DateTime]::UtcNow.ToString('yyyyMMddHHmmss'))"
        $roles = if ($script:CreatedRoles.Count -gt 0) { @($script:CreatedRoles | ForEach-Object { ,@($_.key) }) } else { @(@('Supporter'), @('Admin'), @(), @('Supporter')) }
        for ($i = 1; $i -le $total; $i++) {
            $request = @{ firstName = 'Load'; lastName = "User$i"; email = "$prefix-$i@example.test"; mobileNumber = "0912$('{0:D7}' -f $i)"; roles = $roles[($i - 1) % $roles.Count]; productKeys = @('oracle') }
            $created = Invoke-RestMethod "$($baseUrl.Text.TrimEnd('/'))/admin/api/users" -Method Post -Headers $headers -ContentType 'application/json' -Body ($request | ConvertTo-Json)
            $script:CreatedUsers.Add($created)
            if ($i % 10 -eq 0) { Write-Log "$i users created." }
        }
        Write-Log "Created $total test users." 
    } catch { Write-Log "Create error: $($_.Exception.Message)" }
})

$loadButton.Add_Click({
    try {
        $headers = Get-Headers; $parallel = [int]$concurrency.Text; if ($parallel -lt 1 -or $parallel -gt 50) { throw 'Concurrency must be between 1 and 50.' }
        $jobs = 1..$parallel | ForEach-Object {
            Start-Job -ScriptBlock {
                param($url, $token)
                $h = @{ Authorization = "Bearer $token" }
                $sw = [Diagnostics.Stopwatch]::StartNew()
                try { Invoke-RestMethod "$url/admin/api/dashboard/summary" -Headers $h; $sw.Stop(); [pscustomobject]@{ Ok = $true; Ms = $sw.ElapsedMilliseconds } }
                catch { $sw.Stop(); [pscustomobject]@{ Ok = $false; Ms = $sw.ElapsedMilliseconds } }
            } -ArgumentList $baseUrl.Text.TrimEnd('/'), $script:Token
        }
        $results = $jobs | Wait-Job | Receive-Job; $jobs | Remove-Job
        $ok = @($results | Where-Object Ok).Count; $avg = if ($results.Count) { [math]::Round(($results | Measure-Object Ms -Average).Average, 1) } else { 0 }
        Write-Log "Load complete: $ok/$($results.Count) succeeded, average $avg ms on dashboard summary."
    } catch { Write-Log "Load error: $($_.Exception.Message)" }
})

$cleanupButton.Add_Click({
    try {
        $headers = Get-Headers; $done = 0
        foreach ($user in $script:CreatedUsers) {
            Invoke-RestMethod "$($baseUrl.Text.TrimEnd('/'))/admin/api/users/$($user.id)/status" -Method Patch -Headers $headers -ContentType 'application/json' -Body '{"isActive":false}' | Out-Null
            $done++
        }
        Write-Log "Disabled $done test users. Hard delete is not supported; records remain for audit."
    } catch { Write-Log "Cleanup error: $($_.Exception.Message)" }
})

Write-Log 'Development/Test only. Click Login first.'
[Windows.Forms.Application]::Run($form)
