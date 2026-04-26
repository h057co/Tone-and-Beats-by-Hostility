# Tone & Beats - Release Automation Script
# Este script compila la aplicacion y prepara los archivos para el instalador.

$ProjectFile = "src\AudioAnalyzer.csproj"
$PublishDir = "publish"
$Configuration = "Release"

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host "Iniciando proceso de Build para Tone & Beats" -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan

# 1. Limpiar carpeta publish
if (Test-Path $PublishDir) {
    Write-Host "[-] Limpiando carpeta publish..." -ForegroundColor Yellow
    Remove-Item -Path $PublishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $PublishDir | Out-Null

# 2. Obtener versión actual del .csproj
$csprojContent = Get-Content $ProjectFile
$versionMatch = [regex]::Match($csprojContent, "<Version>(.*)</Version>")
if ($versionMatch.Success) {
    $currentVersion = $versionMatch.Groups[1].Value
    Write-Host "[+] Versión detectada: $currentVersion" -ForegroundColor Green
} else {
    Write-Host "[!] No se pudo detectar la versión en el .csproj" -ForegroundColor Red
    exit 1
}

# 3. Compilar y Publicar
Write-Host "[*] Compilando proyecto en modo $Configuration..." -ForegroundColor Cyan
dotnet publish $ProjectFile -c $Configuration -o $PublishDir /p:PublishSingleFile=true /p:SelfContained=true --runtime win-x64

if ($LASTEXITCODE -ne 0) {
    Write-Host "[!] Error durante la compilación." -ForegroundColor Red
    exit 1
}

Write-Host "====================================================" -ForegroundColor Green
Write-Host "BUILD COMPLETADO CON ÉXITO" -ForegroundColor Green
Write-Host "Versión: $currentVersion" -ForegroundColor Green
Write-Host "Archivos listos en: $PublishDir" -ForegroundColor Green
Write-Host "====================================================" -ForegroundColor Green
Write-Host "Siguientes pasos:" -ForegroundColor Cyan
Write-Host "1. Abre installer\setup.iss en Inno Setup." -ForegroundColor White
Write-Host "2. Presiona F9 para compilar el instalador." -ForegroundColor White
Write-Host "   (El instalador leerá automáticamente la versión del EXE)" -ForegroundColor Gray
