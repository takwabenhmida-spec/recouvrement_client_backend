param (
    [string]$SonarProjectKey = "RecouvrementAPI",
    [string]$SonarHostUrl    = "http://localhost:9000",
    [string]$SonarLoginToken = $env:SONAR_TOKEN
)

# Définit la variable d'environnement pour que les tests puissent se connecter à la DB locale
$env:DB_PASSWORD = "takwa2004"

Write-Host "=== SonarQube Analysis ===" -ForegroundColor Cyan

# Vérification du token
if ([string]::IsNullOrEmpty($SonarLoginToken)) {
    Write-Host "ERREUR : Le token SonarQube est manquant !" -ForegroundColor Red
    Write-Host "Définissez la variable d'environnement : " -ForegroundColor Yellow
    Write-Host '  $env:SONAR_TOKEN = "votre_token"' -ForegroundColor Yellow
    Write-Host "Ou passez-le en argument : " -ForegroundColor Yellow
    Write-Host '  .\Run-SonarAnalysis.ps1 -SonarLoginToken "votre_token"' -ForegroundColor Yellow
    exit 1
}

# ✅ FIX — Chemin absolu et fixe pour le rapport de couverture
$TestProject   = "$PSScriptRoot\RecouvrementAPI.Tests"
$CoveragePath  = "$TestProject\coverage.opencover.xml"

Write-Host "Chemin couverture : $CoveragePath" -ForegroundColor Gray

# 1. Start Sonar — avec chemin ABSOLU
dotnet sonarscanner begin `
    /k:$SonarProjectKey `
    /d:sonar.host.url=$SonarHostUrl `
    /d:sonar.token=$SonarLoginToken `
    /d:sonar.cs.opencover.reportsPaths="$CoveragePath"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Erreur : SonarQube begin a échoué." -ForegroundColor Red
    exit 1
}

# 2. Build
dotnet build --no-incremental
if ($LASTEXITCODE -ne 0) {
    Write-Host "Erreur : Build échoué." -ForegroundColor Red
    exit 1
}

# 3. Test + Coverage — sortie dans le dossier du projet de tests
dotnet test $TestProject `
    /p:CollectCoverage=true `
    /p:CoverletOutputFormat=opencover `
    /p:CoverletOutput="$CoveragePath"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Attention : certains tests ont échoué." -ForegroundColor Yellow
}

# ✅ Vérification que le fichier a bien été généré
if (Test-Path $CoveragePath) {
    Write-Host "Rapport de couverture généré : $CoveragePath" -ForegroundColor Green
} else {
    Write-Host "ERREUR : Le rapport de couverture n'a pas été généré !" -ForegroundColor Red
    Write-Host "Vérifiez que Coverlet est installé dans le projet de tests." -ForegroundColor Yellow
    exit 1
}

# 4. End Sonar
dotnet sonarscanner end /d:sonar.token=$SonarLoginToken

# 5. Cleanup pour VSCode
Write-Host "Nettoyage des fichiers temporaires pour VSCode..." -ForegroundColor Cyan
dotnet clean > $null

Write-Host "=== Analyse terminée ===" -ForegroundColor Cyan
