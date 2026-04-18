#!/bin/bash

echo ""
echo -e "\033[1;44;37m RUNNING BACK OFFICE TESTS \033[0m RecouvrementAPI.Tests"
echo ""

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR" || exit 1

dotnet test RecouvrementAPI.Tests.csproj \
  -c Debug \
  --filter "FullyQualifiedName~BackOfficeApiTests" \
  --logger "console;verbosity=minimal" \
  --nologo

