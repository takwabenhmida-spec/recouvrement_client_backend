#!/bin/bash

echo ""
echo -e "\033[1;44;37m RUNNING SCORING REPORT \033[0m ScoringApiTests"
echo ""

# Always run from the tests project directory so relative paths are stable.
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR" || exit 1

# =====================================================
# RUN TESTS
# =====================================================
dotnet test \
  --filter "ScoringApiTests" \
  --logger "trx;LogFileName=results_scoring.trx" \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults \
  --nologo > /dev/null 2>&1

TRX_FILE=$(find . -name "results_scoring.trx" | head -n 1)
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
echo -e "\033[1;37m================ SCORING TESTS ================\033[0m"

printf "%-70s %-10s %-10s\n" "Test Name" "Status" "Time(ms)"
echo "--------------------------------------------------------------------------"

grep "<UnitTestResult" "$TRX_FILE" | while read -r line; do
    NAME=$(echo "$line" | sed -n 's/.*testName="\([^"]*\)".*/\1/p')
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
# SCORING API ENDPOINTS
# =====================================================
echo ""
echo -e "\033[1;37m================ SCORING API ENDPOINTS ================\033[0m"
echo ""
echo -e "\033[1;36m[ENGINE SCORING IA]\033[0m"
echo -e "\033[34mGET    /api/Scoring/dashboard\033[0m"
echo -e "\033[34mGET    /api/Scoring/{id}/details\033[0m"
echo -e "\033[34mGET    /api/Scoring/{id}/recommandation-ia\033[0m"
echo -e "\033[33mPOST   /api/Scoring/recalculer-tous\033[0m"
echo -e "\033[33mPOST   /api/Scoring/{id}/recalculer\033[0m"
echo ""

# =====================================================
# COVERAGE TABLE
# =====================================================
echo ""
echo -e "\033[1;37m================ SCORING COVERAGE ================\033[0m"
echo ""
echo "File                         | % Stmts | % Branch | % Funcs | % Lines | Uncovered (sample)"
echo "------------------------------------------------------------------------------------------------"

awk '
function to_pct(rate) { return rate * 100.0 }
function basename(p,   n,a) { gsub(/\\/, "/", p); n=split(p,a,"/"); return a[n] }
function is_scoring_file(file) {
    f = tolower(file)
    if (f == "scoringcontroller.cs" || f == "scorerisque.cs" || f == "recouvrementhelper.cs") return 1
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
        if (is_scoring_file(fname)) {
            match($0, /line-rate="([^"]+)"/, lr)
            match($0, /branch-rate="([^"]+)"/, br)
            if (to_pct(lr[1]) > max_lr[fname]) max_lr[fname] = to_pct(lr[1])
            if (to_pct(br[1]) > max_br[fname]) max_br[fname] = to_pct(br[1])
            files[fname] = 1
            curFile = fname
        }
        else curFile = ""
    }
    if (curFile != "" && match($0, /<line[[:space:]][^>]*number="([0-9]+)"[^>]*hits="0"/, m)) {
        add_uncovered(curFile, m[1])
    }
}
END {
    # Separate and Sort
    nc = 0; nm = 0;
    for (f in files) {
        if (tolower(f) ~ /controller\.cs$/) controllers[++nc] = f
        else models[++nm] = f
    }
    for (i=1; i<=nc; i++) for (j=i+1; j<=nc; j++) if (controllers[i] > controllers[j]) { t = controllers[i]; controllers[i] = controllers[j]; controllers[j] = t }
    for (i=1; i<=nm; i++) for (j=i+1; j<=nm; j++) if (models[i] > models[j]) { t = models[i]; models[i] = models[j]; models[j] = t }

    # Print Controllers
    if (nc > 0) {
        print "\033[1;36m[CONTROLLERS]\033[0m"
        for (i=1; i<=nc; i++) {
            f = controllers[i]
            unc = compress_list(u[f])
            if (length(unc) > 22) unc = substr(unc, 1, 19) "..."
            printf "%-28s | %7.2f | %8.2f | %7.2f | %7.2f | %-22s\n", 
                f, max_lr[f], max_br[f], 100.0, max_lr[f], (length(unc)>0 ? unc : "-")
        }
        print ""
    }
    # Print Models
    if (nm > 0) {
        print "\033[1;36m[MODELS / HELPERS]\033[0m"
        for (i=1; i<=nm; i++) {
            f = models[i]
            unc = compress_list(u[f])
            if (length(unc) > 22) unc = substr(unc, 1, 19) "..."
            printf "%-28s | %7.2f | %8.2f | %7.2f | %7.2f | %-22s\n", 
                f, max_lr[f], max_br[f], 100.0, max_lr[f], (length(unc)>0 ? unc : "-")
        }
    }
}
' "$COVERAGE_FILE"

echo "------------------------------------------------------------------------------------------------"

# =====================================================
# SUMMARY
# =====================================================
PASSED=$(grep -c 'outcome="Passed"' "$TRX_FILE")
FAILED=$(grep -c 'outcome="Failed"' "$TRX_FILE")
TOTAL=$((PASSED + FAILED))

echo ""
echo -e "\033[1mTests:\033[0m $PASSED passed, $TOTAL total"

if [ "$FAILED" -eq 0 ]; then
    echo -e "\033[42;37m ✔ ALL SCORING TESTS PASSED \033[0m"
else
    echo -e "\033[41;37m ✕ FAILURES DETECTED \033[0m"
fi

echo ""
