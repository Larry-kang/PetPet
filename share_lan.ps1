# Check for Admin privileges
if (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Warning "Please run this script as Administrator to open the Firewall port!"
    Start-Process powershell.exe "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    exit
}

# Open Firewall Port 5000
$port = 5000
$ruleName = "Allow PetPet Web"

$exists = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
if (!$exists) {
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -LocalPort $port -Protocol TCP -Action Allow
    Write-Host "✅ Firewall Rule '$ruleName' created for Port $port." -ForegroundColor Green
}
else {
    Write-Host "✅ Firewall Rule already exists." -ForegroundColor Gray
}

# Get Local IP
$ip = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.InterfaceAlias -notlike "*Loopback*" -and $_.InterfaceAlias -notlike "*vEthernet*" -and $_.IPAddress -notlike "169.254*" } | Select-Object -First 1).IPAddress

if (!$ip) {
    # Fallback usually for some VPNs or complex setups, try simpler filter
    $ip = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.InterfaceAlias -like "*Wi-Fi*" -or $_.InterfaceAlias -like "*Ethernet*" } | Select-Object -First 1).IPAddress
}

$url = "http://$($ip):$port"

Write-Host "`n==============================================" -ForegroundColor Cyan
Write-Host " 🚀 您的區網分享連結 (LAN Access Link)" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "請將此連結傳給同事："
Write-Host ""
Write-Host "   $url" -ForegroundColor Green
Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
