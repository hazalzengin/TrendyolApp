<#
    Trendyol Fiyat Takip uygulamasını yerel IIS'e kurar ve sürekli çalışacak şekilde ayarlar.
    Tekrar çalıştırıldığında uygulamayı günceller; takip edilen ürünler (App_Data) korunur.

    Kullanım:  Install-IIS.cmd dosyasına çift tıklayın  (veya)  .\Install-IIS.ps1 -Port 8080
#>
param(
    [int]$Port = 8080,
    [string]$SiteName = "TrendyolFiyatTakip",
    [string]$TargetPath = "C:\inetpub\TrendyolFiyatTakip"
)

$ErrorActionPreference = "Stop"

# Yönetici değilsek kendimizi yönetici olarak yeniden başlat.
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Start-Process powershell.exe -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -NoExit -File `"$PSCommandPath`" -Port $Port -SiteName `"$SiteName`" -TargetPath `"$TargetPath`""
    exit
}

$projectPath = Join-Path $PSScriptRoot "TrendyolApp"
function Step($text) { Write-Host "`n==> $text" -ForegroundColor Cyan }

# 1) IIS ve ASP.NET 4.x özellikleri
Step "IIS özellikleri kontrol ediliyor (ilk kurulumda birkaç dakika sürebilir)"
$features = @(
    "IIS-WebServerRole", "IIS-WebServer", "IIS-CommonHttpFeatures", "IIS-DefaultDocument", "IIS-StaticContent",
    "IIS-HttpErrors", "IIS-RequestFiltering", "IIS-NetFxExtensibility45", "IIS-ISAPIExtensions", "IIS-ISAPIFilter",
    "IIS-ASPNET45", "NetFx4Extended-ASPNET45", "IIS-ApplicationInit", "IIS-ManagementConsole"
)
$missing = $features | Where-Object { (Get-WindowsOptionalFeature -Online -FeatureName $_).State -ne "Enabled" }
if ($missing) {
    Enable-WindowsOptionalFeature -Online -All -NoRestart -FeatureName $missing | Out-Null
    Write-Host "Etkinleştirildi: $($missing -join ', ')"
} else {
    Write-Host "Tüm özellikler zaten etkin."
}

# 2) Derleme
Step "Proje derleniyor"
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
if (-not $msbuild) { throw "MSBuild bulunamadı. Visual Studio kurulu olmalı." }
& $msbuild (Join-Path $projectPath "TrendyolApp.csproj") /nologo /v:m /p:Configuration=Release
if ($LASTEXITCODE -ne 0) { throw "Derleme başarısız." }

# 3) Dosyaları kopyala (kaynak kod ve App_Data hariç; hedefteki App_Data silinmez)
Step "Dosyalar $TargetPath klasörüne kopyalanıyor"
if (Test-Path "IIS:\AppPools\$SiteName") { Import-Module WebAdministration; Stop-WebAppPool $SiteName -ErrorAction SilentlyContinue }
robocopy $projectPath $TargetPath /MIR /XD obj .vs App_Data /XF *.cs *.csproj *.user packages.config Web.Debug.config Web.Release.config /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "Kopyalama başarısız (robocopy kodu $LASTEXITCODE)." }

$dataPath = Join-Path $TargetPath "App_Data"
New-Item -ItemType Directory -Force $dataPath | Out-Null
$devData = Join-Path $projectPath "App_Data\products.json"
if ((Test-Path $devData) -and -not (Test-Path (Join-Path $dataPath "products.json"))) {
    Copy-Item $devData $dataPath
    Write-Host "Visual Studio'da eklediğiniz ürünler taşındı."
}

# 4) Sürekli çalışan uygulama havuzu
Step "Uygulama havuzu ayarlanıyor"
Import-Module WebAdministration
$pool = "IIS:\AppPools\$SiteName"
if (-not (Test-Path $pool)) { New-WebAppPool -Name $SiteName | Out-Null }
Set-ItemProperty $pool -Name managedRuntimeVersion -Value "v4.0"
Set-ItemProperty $pool -Name managedPipelineMode -Value "Integrated"
Set-ItemProperty $pool -Name startMode -Value "AlwaysRunning"               # Windows açılınca başlar
Set-ItemProperty $pool -Name processModel.idleTimeout -Value "00:00:00"     # boşta kapanmaz
Set-ItemProperty $pool -Name recycling.periodicRestart.time -Value "00:00:00"

# 5) Site
Step "Site ayarlanıyor"
$site = "IIS:\Sites\$SiteName"
if (-not (Test-Path $site)) {
    $busy = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    if ($busy) { throw "$Port portu kullanımda. Başka port deneyin: .\Install-IIS.ps1 -Port 8090" }
    New-Website -Name $SiteName -Port $Port -PhysicalPath $TargetPath -ApplicationPool $SiteName | Out-Null
} else {
    Set-ItemProperty $site -Name physicalPath -Value $TargetPath
}
# Uygulama ilk istek beklenmeden başlasın; fiyat zamanlayıcısı Application_Start'ta çalışır.
Set-ItemProperty $site -Name applicationDefaults.preloadEnabled -Value $true

# 6) Ürün verisinin yazılabilmesi için izin
icacls $dataPath /grant "IIS AppPool\$($SiteName):(OI)(CI)M" /T /Q | Out-Null

# 7) Başlat ve doğrula
Step "Başlatılıyor"
Start-WebAppPool $SiteName -ErrorAction SilentlyContinue
Start-Website $SiteName -ErrorAction SilentlyContinue
$url = "http://localhost:$((Get-WebBinding -Name $SiteName | Select-Object -First 1).bindingInformation.Split(':')[1])/"
try {
    $response = Invoke-WebRequest $url -UseBasicParsing -TimeoutSec 60
    Write-Host "Site çalışıyor: $url (HTTP $($response.StatusCode))" -ForegroundColor Green
    Start-Process $url
} catch {
    Write-Host "Site yanıt vermedi: $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host "IIS özellikleri yeni etkinleştirildiyse bilgisayarı yeniden başlatıp betiği tekrar çalıştırın."
}

Write-Host "`nKurulum tamamlandı. Fiyatlar uygulama havuzu çalıştığı sürece otomatik takip edilir." -ForegroundColor Green
Write-Host "Kodda değişiklik yaptıktan sonra bu betiği tekrar çalıştırmanız yeterli; takip ettiğiniz ürünler korunur."
