<#
.SYNOPSIS
    Gets an access token from the development realm with the OAuth 2.0 device authorization grant (RFC 8628).

.DESCRIPTION
    The CLI client has no password grant. This script asks Keycloak for a device code, opens the
    verification page in your browser (sign in there, with OTP for admin/operator users), polls until
    you approve, and prints the access token. Works with Windows PowerShell 5.1 and PowerShell 7.

.EXAMPLE
    $token = ./scripts/get-token.ps1 -Scope "catalog.write identity.write"
    Invoke-RestMethod http://localhost:5080/api/v1/catalog/products -Headers @{ Authorization = "Bearer $token" }

.EXAMPLE
    ./scripts/get-token.ps1 -Scope "platform.ops"   # operators: dead-letter endpoints
#>
param(
    [string] $Authority = "http://localhost:8080/realms/modular-monolith",
    [string] $ClientId = "modular-monolith-cli",
    # Optional scopes to request on top of the client's defaults (catalog.read, identity.read, privacy.read, profile, email).
    [string] $Scope = "catalog.write identity.write"
)

$ErrorActionPreference = "Stop"

$device = Invoke-RestMethod -Method Post -Uri "$Authority/protocol/openid-connect/auth/device" `
    -ContentType "application/x-www-form-urlencoded" `
    -Body @{ client_id = $ClientId; scope = "openid $Scope" }

Write-Host "Open $($device.verification_uri_complete) and approve the sign-in (code $($device.user_code))." -ForegroundColor Cyan
try { Start-Process $device.verification_uri_complete } catch { }

$interval = [Math]::Max([int]$device.interval, 5)
$deadline = (Get-Date).AddSeconds([int]$device.expires_in)
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds $interval
    try {
        $token = Invoke-RestMethod -Method Post -Uri "$Authority/protocol/openid-connect/token" `
            -ContentType "application/x-www-form-urlencoded" `
            -Body @{ grant_type = "urn:ietf:params:oauth:grant-type:device_code"; client_id = $ClientId; device_code = $device.device_code }
        return $token.access_token
    }
    catch {
        $problem = $null
        try { $problem = ($_.ErrorDetails.Message | ConvertFrom-Json).error } catch { }
        if ($problem -eq "authorization_pending") { continue }
        if ($problem -eq "slow_down") { $interval += 5; continue }
        throw
    }
}

throw "The device code expired before it was approved."
