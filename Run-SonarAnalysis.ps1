param (
    [string]$SonarProjectKey = "RecouvrementAPI",
    [string]$SonarHostUrl = "http://localhost:9000",
    [string]$SonarLoginToken = "sqp_8a04d2d6c4a020ce94ec1ba6b5937c848e0e9839"
)

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "   Début de l'analyse SonarQube" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

# Étape 1 : Démarrer le scanner SonarQube
Write-Host "`n[1/4] Démarrage du scanner SonarQube..." -ForegroundColor Yellow
dotnet sonarscanner begin /k:$SonarProjectKey /d:sonar.host.url=$SonarHostUrl /d:sonar.token=$SonarLoginToken /d:sonar.cs.opencover.reportsPaths="RecouvrementAPI.Tests/coverage.opencover.xml" /d:sonar.exclusions="appsettings.json,docker-compose.yml"

# Étape 2 : Reconstruire le projet
Write-Host "`n[2/4] Compilation du projet..." -ForegroundColor Yellow
dotnet build --no-incremental

# Étape 3 : Exécuter les tests avec couverture de code
Write-Host "`n[3/4] Exécution des tests et génération de la couverture..." -ForegroundColor Yellow
dotnet test `
    /p:CollectCoverage=true `
    /p:CoverletOutputFormat=opencover `
    /p:CoverletOutput="./coverage.opencover.xml"

# Étape 4 : Fin du scanner et envoi des résultats
Write-Host "`n[4/4] Finalisation de l'analyse et envoi à SonarQube..." -ForegroundColor Yellow
dotnet sonarscanner end /d:sonar.token=$SonarLoginToken

Write-Host "`n=============================================" -ForegroundColor Green
Write-Host " Analyse terminée !" -ForegroundColor Green
Write-Host "Allez sur $SonarHostUrl pour voir les résultats." -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green
