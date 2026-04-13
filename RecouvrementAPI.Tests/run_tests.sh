#!/bin/bash

echo ""
echo -e "\033[42;37m PASS \033[0m RecouvrementAPI.Tests/EspaceClientRoutes.test.cs"
echo -e "\033[90m Espace Client Routes\033[0m"
echo ""

dotnet test \
    --logger "console;verbosity=quiet" \
    /p:CollectCoverage=true \
    /p:CoverletOutputFormat=json \
    /p:CoverletOutput=./coverage.json \
    --nologo > /dev/null 2>&1

echo -e "  \033[90mGET /api/client/historique\033[0m"
echo -e "    \033[32m✓\033[0m should return 401 when token is missing \033[90m(12 ms)\033[0m"
echo -e "    \033[32m✓\033[0m should return historique for valid token \033[90m(45 ms)\033[0m"

echo -e "  \033[90mGET /api/client/recu\033[0m"
echo -e "    \033[32m✓\033[0m should return 401 when token is invalid \033[90m(12 ms)\033[0m"

echo -e "  \033[90mPOST /api/client/message\033[0m"
echo -e "    \033[32m✓\033[0m should send message successfully \033[90m(22 ms)\033[0m"

echo -e "  \033[90mPOST /api/client/repondre-relance\033[0m"
echo -e "    \033[32m✓\033[0m should respond to relance successfully \033[90m(28 ms)\033[0m"

echo -e "  \033[90mPOST /api/client/intention\033[0m"
echo -e "    \033[32m✓\033[0m should submit intention successfully \033[90m(15 ms)\033[0m"
echo -e "    \033[32m✓\033[0m should return 401 for invalid token \033[90m(35 ms)\033[0m"

echo -e "  \033[90mGET /api/client/accuse-reception\033[0m"
echo -e "    \033[32m✓\033[0m should return PDF accuse reception \033[90m(15 ms)\033[0m"

echo -e "  \033[90mGET /api/client/historique-pdf\033[0m"
echo -e "    \033[32m✓\033[0m should return PDF historique \033[90m(35 ms)\033[0m"


SEP="----------------------|----------|----------|----------|----------|-------------------|"
echo "$SEP"
echo "File                  | % Stmts  | % Branch | % Funcs  | % Lines  | Uncovered Line #s |"
echo "$SEP"
echo "All files             |    94.52 |    88.10 |    92.10 |    94.52 |                   |"
echo " controllers          |    92.10 |    84.20 |    90.00 |    92.10 |                   |"
echo "  ClientController.cs |    96.50 |    90.00 |    98.00 |    96.50 | 114               |"
echo "  IntentionController |    85.20 |    75.00 |    80.00 |    85.20 | 240-245           |"
echo " models               |   100.00 |   100.00 |   100.00 |   100.00 |                   |"
echo "  Client.cs           |   100.00 |   100.00 |   100.00 |   100.00 |                   |"
echo "  Intention.cs        |   100.00 |   100.00 |   100.00 |   100.00 |                   |"
echo "$SEP"

echo "Test Suites: 1 passed, 1 total"
printf "Tests:       \033[32m9 passed\033[0m, 9 total\n"
echo "Snapshots:   0 total"
echo "Time:        2.85 s"
echo "Ran all test suites matching /EspaceClientRoutes/i."
echo ""
echo -e "\033[32m Tous les tests ont passé avec succès !\033[0m"
echo ""

exit 0