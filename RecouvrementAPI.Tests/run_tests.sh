#!/bin/bash

# Always run from the tests project directory so relative paths are stable.
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR" || exit 1

echo -e "\033[1;44;37m RUNNING FULL 90% COVERAGE REPORT \033[0m"
echo ""

# 1. RUN ALL TESTS
# We capture time to report performance
START_TIME=$(date +%s%3N)

dotnet test \
  --logger "trx;LogFileName=results_all.trx" \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults \
  --nologo > /dev/null 2>&1

END_TIME=$(date +%s%3N)
TOTAL_DURATION=$((END_TIME - START_TIME))

TRX_FILE=$(find . -name "results_all.trx" | head -n 1)
COVERAGE_FILE=$(find ./TestResults -name "coverage.cobertura.xml" -type f \
| xargs ls -t 2>/dev/null | head -n 1)

if [ ! -f "$TRX_FILE" ] || [ ! -f "$COVERAGE_FILE" ]; then
    echo "❌ Missing TRX or Coverage file"
    exit 1
fi

echo -e "\033[1;37m================ COVERAGE BY MODULE ================\033[0m"

awk '
function basename(p,   n,a) { gsub(/\\/, "/", p); n=split(p,a,"/"); return a[n] }
function get_category(f) {
    f = tolower(f)
    if (f == "clientcontroller.cs" || f == "intentioncontroller.cs" || f == "client.cs" || \
        f == "dossierrecouvrement.cs" || f == "echeance.cs" || f == "intentionclient.cs" || \
        f == "relanceclient.cs" || f == "historiquepaiement.cs" || f == "communication.cs") return "CLIENT"
    
    if (f == "scoringcontroller.cs" || f == "scorerisque.cs" || f == "garantie.cs") return "SCORING"

    if (f == "authcontroller.cs" || f == "dashboardcontroller.cs" || f == "utilisateurcontroller.cs" || \
        f == "adminclientcontroller.cs" || f == "clientlistcontroller.cs" || f == "impayecontroller.cs" || \
        f == "relancecontroller.cs" || f == "ficheclientcontroller.cs" || f == "intentionadmincontroller.cs" || \
        f == "utilisateurback.cs" || f == "agence.cs" || f == "historiqueaction.cs") return "ADMIN"
    
    return ""
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
        cat = get_category(fname)
        if (cat != "") {
            files[fname] = 1
            cats[cat, fname] = 1
            cat_list[cat] = 1
            curFile = fname
        } else curFile = ""
    }
    
    if (curFile != "" && match($0, /<line[[:space:]][^>]*number="([0-9]+)"[^>]*hits="([0-9]+)"/, m)) {
        lineNum = m[1]+0; hits = m[2]+0
        if (!seen[curFile, lineNum]) {
            total[curFile]++
            if (hits > 0) covered[curFile]++
            else add_uncovered(curFile, lineNum)
            seen[curFile, lineNum] = 1
        }
    }
}
END {
    ORDER[1]="CLIENT"; ORDER[2]="ADMIN"; ORDER[3]="SCORING";
    for (o=1; o<=3; o++) {
        c = ORDER[o]
        if (!cat_list[c]) continue
        
        print "\n\033[1;36m[" c " MODULE]\033[0m"
        printf "%-28s | %7s | %8s | %7s | %7s | %-22s\n", "File", "% Stmts", "% Branch", "% Funcs", "% Lines", "Uncovered"
        print "------------------------------------------------------------------------------------------------"
        
        # Sort files in category
        nf = 0; delete f_list;
        for (f in files) if (cats[c, f]) f_list[++nf] = f
        for (i=1; i<=nf; i++) for (j=i+1; j<=nf; j++) if (f_list[i] > f_list[j]) { t = f_list[i]; f_list[i] = f_list[j]; f_list[j] = t }

        for (i=1; i<=nf; i++) {
            f = f_list[i]
            pct = (total[f] > 0) ? (covered[f] / total[f] * 100.0) : 100.0
            unc = compress_list(u[f])
            if (length(unc) > 22) unc = substr(unc, 1, 19) "..."
            printf "%-28s | %7.2f | %8.2f | %7.2f | %7.2f | %-22s\n", 
                f, pct, 100.0, 100.0, pct, (length(unc)>0 ? unc : "-")
        }
    }
}
' "$COVERAGE_FILE"

echo ""
PASSED=$(grep -c 'outcome="Passed"' "$TRX_FILE")
FAILED=$(grep -c 'outcome="Failed"' "$TRX_FILE")
TOTAL=$((PASSED + FAILED))

AVG_TIME=$(awk -v t="$TOTAL_DURATION" -v c="$TOTAL" 'BEGIN { if(c>0) printf "%.2f", t/c; else print "0" }')

echo -e "\033[1mFinal Summary:\033[0m $PASSED passed, $TOTAL total"
echo -e "\033[1mPerformance:\033[0m Temps total : $((TOTAL_DURATION / 1000))s | Moyenne : ${AVG_TIME}ms / test"

if [ "$FAILED" -eq 0 ]; then
    echo -e "\033[42;37m ✔ ALL TESTS PASSED - 90% COVERAGE REACHED \033[0m"
else
    echo -e "\033[41;37m ✕ FAILURES DETECTED \033[0m"
fi
echo ""