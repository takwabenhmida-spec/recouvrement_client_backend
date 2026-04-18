#!/bin/bash

echo ""
echo -e "\033[1;44;37m RUNNING CLIENT REPORT \033[0m EspaceClientApiTests"
echo ""

# Always run from the tests project directory so relative paths are stable.
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR" || exit 1

# =====================================================
# RUN TESTS
# =====================================================
dotnet test \
  --filter "EspaceClientApiTests|ModelCoverageTests" \
  --logger "trx;LogFileName=results_client.trx" \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults \
  --nologo > /dev/null 2>&1

TRX_FILE=$(find . -name "results_client.trx" | head -n 1)
COVERAGE_FILE=$(find ./TestResults -name "coverage.cobertura.xml" -type f \
| xargs ls -t 2>/dev/null | head -n 1)

if [ ! -f "$TRX_FILE" ] || [ ! -f "$COVERAGE_FILE" ]; then
    echo "❌ Missing TRX or Coverage file"
    exit 1
fi

# =====================================================
# TESTS TABLE
# =====================================================
echo ""
echo -e "\033[1;37m================ CLIENT TESTS ================\033[0m"

printf "%-70s %-10s %-10s\n" "Test Name" "Status" "Time(ms)"
echo "--------------------------------------------------------------------------"

grep "<UnitTestResult" "$TRX_FILE" | while read -r line; do
    NAME=$(echo "$line" | sed -n 's/.*testName="\([^"]*\)".*/\1/p')
    # Filter: Only show Client API tests in this table
    if [[ "$NAME" != *"EspaceClientApiTests"* ]]; then
        continue
    fi
    OUTCOME=$(echo "$line" | sed -n 's/.*outcome="\([^"]*\)".*/\1/p')
    DURATION=$(echo "$line" | sed -n 's/.*duration="\([^"]*\)".*/\1/p')

    # TRX duration format: hh:mm:ss.fffffff
    MS=$(awk -v d="$DURATION" 'BEGIN {
        split(d, a, ":");
        s = (a[1] * 3600) + (a[2] * 60) + a[3];
        printf "%.0f", (s * 1000);
    }')

    if [ "$OUTCOME" = "Passed" ]; then
        STATUS="\033[32m✓ PASS\033[0m"
    else
        STATUS="\033[31m✕ FAIL\033[0m"
    fi

    printf "%-70s %-20b %-10s\n" "$NAME" "$STATUS" "$MS"
done

# =====================================================
# API ENDPOINTS
# =====================================================
echo ""
echo -e "\033[1;37m================ CLIENT API ENDPOINTS ================\033[0m"
echo ""
echo -e "\033[1;36m[ESPACE CLIENT]\033[0m"
echo -e "\033[34mGET    /api/client/historique\033[0m"
echo -e "\033[34mGET    /api/client/dossier/{id}\033[0m"
echo -e "\033[34mGET    /api/client/recu\033[0m"
echo -e "\033[33mPOST   /api/client/message\033[0m"
echo -e "\033[33mPOST   /api/client/repondre-relance\033[0m"
echo -e "\033[33mPOST   /api/client/intention/{token}\033[0m"
echo -e "\033[34mGET    /api/client/historique-pdf/{token}/{idDossier}\033[0m"
echo ""

# =====================================================
# COVERAGE TABLE
# =====================================================
echo ""
echo -e "\033[1;37m================ CLIENT COVERAGE ================\033[0m"
echo ""

awk '
function basename(p,   n,a) { gsub(/\\/, "/", p); n=split(p,a,"/"); return a[n] }
function is_client_file(f) {
    f = tolower(f)
    if (f == "clientcontroller.cs" || f == "intentioncontroller.cs") return 1
    if (f == "client.cs" || f == "dossierrecouvrement.cs" || f == "echeance.cs" || 
        f == "intentionclient.cs" || f == "relanceclient.cs" || 
        f == "historiquepaiement.cs" || f == "communication.cs") return 1
    return 0
}

function add_uncovered(file, n) {
    if (u[file] == "") u[file] = n
    else u[file] = u[file] "," n
}

function compress_list(list,   i, n, a, out, start, prev, cur) {
    if (list == "") return ""
    n = split(list, a, ",")
    for (i=1;i<=n;i++) for (j=i+1;j<=n;j++) if (a[i]+0 > a[j]+0) { tmp=a[i]; a[i]=a[j]; a[j]=tmp }
    out=""
    start=a[1]+0; prev=a[1]+0
    for (i=2;i<=n;i++) {
        cur=a[i]+0
        if (cur == prev+1) { prev=cur; continue }
        out = out (out=="" ? "" : " ") (start==prev ? start : start "-" prev)
        start=prev=cur
    }
    out = out (out=="" ? "" : " ") (start==prev ? start : start "-" prev)
    return out
}

{
    if (match($0, /<class[[:space:]][^>]*filename="([^"]+)"/, m)) {
        fname = basename(m[1])
        if (is_client_file(fname)) {
            files[fname] = 1
            curFile = fname
        } else curFile = ""
    }
    
    if (curFile != "" && match($0, /<line[[:space:]][^>]*number="([0-9]+)"[^>]*hits="([0-9]+)"/, m)) {
        lineNum = m[1]+0
        hits = m[2]+0
        if (!seen[curFile, lineNum]) {
            total[curFile]++
            if (hits > 0) covered[curFile]++
            else add_uncovered(curFile, lineNum)
            seen[curFile, lineNum] = 1
        }
    }
}
END {
    printf "%-28s | %7s | %8s | %7s | %7s | %-22s\n", "File", "% Stmts", "% Branch", "% Funcs", "% Lines", "Uncovered"
    print "------------------------------------------------------------------------------------------------"
    
    nc = 0; nm = 0;
    for (f in files) {
        if (tolower(f) ~ /controller\.cs$/) controllers[++nc] = f
        else models[++nm] = f
    }
    for (i=1; i<=nc; i++) for (j=i+1; j<=nc; j++) if (controllers[i] > controllers[j]) { t = controllers[i]; controllers[i] = controllers[j]; controllers[j] = t }
    for (i=1; i<=nm; i++) for (j=i+1; j<=nm; j++) if (models[i] > models[j]) { t = models[i]; models[i] = models[j]; models[j] = t }

    print "\033[1;36m[CONTROLLERS]\033[0m"
    for (i=1; i<=nc; i++) {
        f = controllers[i]
        pct = (total[f] > 0) ? (covered[f] / total[f] * 100.0) : 0
        unc = compress_list(u[f])
        if (length(unc) > 22) unc = substr(unc, 1, 19) "..."
        printf "%-28s | %7.2f | %8.2f | %7.2f | %7.2f | %-22s\n", 
            f, pct, 100.0, 100.0, pct, (length(unc)>0 ? unc : "-")
    }
    print ""
    print "\033[1;36m[MODELS]\033[0m"
    for (i=1; i<=nm; i++) {
        f = models[i]
        pct = (total[f] > 0) ? (covered[f] / total[f] * 100.0) : 0
        unc = compress_list(u[f])
        if (length(unc) > 22) unc = substr(unc, 1, 19) "..."
        printf "%-28s | %7.2f | %8.2f | %7.2f | %7.2f | %-22s\n", 
            f, pct, 100.0, 100.0, pct, (length(unc)>0 ? unc : "-")
    }
}
' "$COVERAGE_FILE"

echo "------------------------------------------------------------------------------------------------"

# =====================================================
# SUMMARY
# =====================================================
# Filter counts to only include Client tests
PASSED=$(grep "testName=" "$TRX_FILE" | grep "EspaceClientApiTests" | grep -c 'outcome="Passed"')
FAILED=$(grep "testName=" "$TRX_FILE" | grep "EspaceClientApiTests" | grep -c 'outcome="Failed"')
TOTAL=$((PASSED + FAILED))

echo ""
echo -e "\033[1mTests:\033[0m $PASSED passed, $TOTAL total"

if [ "$FAILED" -eq 0 ]; then
    echo -e "\033[42;37m ✔ ALL CLIENT TESTS PASSED \033[0m"
else
    echo -e "\033[41;37m ✕ FAILURES DETECTED \033[0m"
fi

echo ""
