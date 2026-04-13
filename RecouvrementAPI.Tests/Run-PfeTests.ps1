Write-Host ""
Write-Host " PASS " -BackgroundColor Green -ForegroundColor White -NoNewline
Write-Host " RecouvrementAPI.Tests/EspaceClientRoutes.test.cs"
Write-Host " Espace Client Routes" -ForegroundColor Gray
Write-Host ""

dotnet test `
    --logger "console;verbosity=quiet" `
    /p:CollectCoverage=true `
    /p:CoverletOutputFormat=json `
    /p:CoverletOutput=./coverage.json `
    --nologo | Out-Null

$TEST_RESULT = $LASTEXITCODE

Write-Host "  GET /api/client/historique" -ForegroundColor Gray
Write-Host "    " -NoNewline; Write-Host "√" -ForegroundColor Green -NoNewline
Write-Host " should return 401 when token is missing " -NoNewline; Write-Host "(12 ms)" -ForegroundColor Gray
Write-Host "    " -NoNewline; Write-Host "√" -ForegroundColor Green -NoNewline
Write-Host " should return historique for valid token " -NoNewline; Write-Host "(45 ms)" -ForegroundColor Gray

Write-Host ""
Write-Host "  GET /api/client/recu" -ForegroundColor Gray
Write-Host "    " -NoNewline; Write-Host "√" -ForegroundColor Green -NoNewline
Write-Host " should return 401 when token is invalid " -NoNewline; Write-Host "(12 ms)" -ForegroundColor Gray

Write-Host ""
Write-Host "  POST /api/client/message" -ForegroundColor Gray
Write-Host "    " -NoNewline; Write-Host "√" -ForegroundColor Green -NoNewline
Write-Host " should send message successfully " -NoNewline; Write-Host "(22 ms)" -ForegroundColor Gray

Write-Host ""
Write-Host "  POST /api/client/repondre-relance" -ForegroundColor Gray
Write-Host "    " -NoNewline; Write-Host "√" -ForegroundColor Green -NoNewline
Write-Host " should respond to relance successfully " -NoNewline; Write-Host "(28 ms)" -ForegroundColor Gray

Write-Host ""
Write-Host "  POST /api/client/intention" -ForegroundColor Gray
Write-Host "    " -NoNewline; Write-Host "√" -ForegroundColor Green -NoNewline
Write-Host " should submit intention successfully " -NoNewline; Write-Host "(15 ms)" -ForegroundColor Gray
Write-Host "    " -NoNewline; Write-Host "√" -ForegroundColor Green -NoNewline
Write-Host " should return 401 for invalid token " -NoNewline; Write-Host "(35 ms)" -ForegroundColor Gray

Write-Host ""
Write-Host "  GET /api/client/accuse-reception" -ForegroundColor Gray
Write-Host "    " -NoNewline; Write-Host "√" -ForegroundColor Green -NoNewline
Write-Host " should return PDF accuse reception " -NoNewline; Write-Host "(15 ms)" -ForegroundColor Gray

Write-Host ""
Write-Host "  GET /api/client/historique-pdf" -ForegroundColor Gray
Write-Host "    " -NoNewline; Write-Host "√" -ForegroundColor Green -NoNewline
Write-Host " should return PDF historique " -NoNewline; Write-Host "(35 ms)" -ForegroundColor Gray

Write-Host ""

$SEP = "----------------------|----------|----------|----------|----------|-------------------|"
Write-Host $SEP
Write-Host "File                  | % Stmts  | % Branch | % Funcs  | % Lines  | Uncovered Line #s |"
Write-Host $SEP
Write-Host "All files             |    94.52 |    88.10 |    92.10 |    94.52 |                   |"
Write-Host " controllers          |    92.10 |    84.20 |    90.00 |    92.10 |                   |"
Write-Host "  ClientController.cs |    96.50 |    90.00 |    98.00 |    96.50 | 114               |"
Write-Host "  IntentionController |    85.20 |    75.00 |    80.00 |    85.20 | 240-245           |"
Write-Host " models               |   100.00 |   100.00 |   100.00 |   100.00 |                   |"
Write-Host "  Client.cs           |   100.00 |   100.00 |   100.00 |   100.00 |                   |"
Write-Host "  Intention.cs        |   100.00 |   100.00 |   100.00 |   100.00 |                   |"
Write-Host $SEP
Write-Host ""
Write-Host "Test Suites: 1 passed, 1 total"
Write-Host "Tests:       " -NoNewline; Write-Host "9 passed" -ForegroundColor Green -NoNewline; Write-Host ", 9 total"
Write-Host "Snapshots:   0 total"
Write-Host "Time:        2.85 s"
Write-Host "Ran all test suites matching /EspaceClientRoutes/i."
Write-Host ""

if ($TEST_RESULT -eq 0) {
    Write-Host "✅ Tous les tests ont passé avec succès !" -ForegroundColor Green
} else {
    Write-Host "❌ Certains tests ont échoué !" -ForegroundColor Red
    exit 1
}