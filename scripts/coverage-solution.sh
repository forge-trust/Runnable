#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SOLUTION_PATH="${1:-$ROOT_DIR/ForgeTrust.Runnable.slnx}"
OUTPUT_DIR="${2:-$ROOT_DIR/TestResults/coverage-merged}"
INCLUDE_FILTER="${INCLUDE_FILTER:-[ForgeTrust.Runnable.*]*}"
EXCLUDE_FILTER="${EXCLUDE_FILTER:-[*.Tests]*,[*.IntegrationTests]*}"
EXCLUDE_FILTER="${EXCLUDE_FILTER//,/%2c}"

if [[ ! -f "$SOLUTION_PATH" ]]; then
  echo "Solution not found: $SOLUTION_PATH" >&2
  exit 1
fi

mkdir -p "$OUTPUT_DIR"
rm -f "$OUTPUT_DIR/coverage.json" "$OUTPUT_DIR/coverage.cobertura.xml" "$OUTPUT_DIR/summary.txt"

test_projects=()
while IFS= read -r project; do
  test_projects+=("$project")
done < <(
  dotnet sln "$SOLUTION_PATH" list \
    | tail -n +3 \
    | sed '/^[[:space:]]*$/d' \
    | grep -E '(Tests|IntegrationTests)\.csproj$'
)

if [[ "${#test_projects[@]}" -eq 0 ]]; then
  echo "No test projects found in $SOLUTION_PATH" >&2
  exit 1
fi

for i in "${!test_projects[@]}"; do
  project_rel="${test_projects[$i]}"
  project_path="$ROOT_DIR/$project_rel"
  echo "[$((i + 1))/${#test_projects[@]}] dotnet test $project_rel"

  args=(
    dotnet test "$project_path"
    -v minimal
    /p:CollectCoverage=true
    "/p:CoverletOutput=$OUTPUT_DIR/coverage"
    "/p:CoverletOutputFormat=json%2ccobertura"
    "/p:Include=$INCLUDE_FILTER"
    "/p:Exclude=$EXCLUDE_FILTER"
  )

  if [[ -f "$OUTPUT_DIR/coverage.json" ]]; then
    args+=("/p:MergeWith=$OUTPUT_DIR/coverage.json")
  fi

  "${args[@]}"
done

coverage_file="$OUTPUT_DIR/coverage.cobertura.xml"
if [[ ! -f "$coverage_file" ]]; then
  echo "Merged Cobertura file was not created: $coverage_file" >&2
  exit 1
fi

read -r lines_covered lines_valid branches_covered branches_valid < <(
  sed -n '2p' "$coverage_file" \
    | sed -E 's/.*lines-covered="([0-9]+)".*lines-valid="([0-9]+)".*branches-covered="([0-9]+)".*branches-valid="([0-9]+)".*/\1 \2 \3 \4/'
)

line_rate="$(awk -v c="$lines_covered" -v v="$lines_valid" 'BEGIN { if (v == 0) printf "0.00"; else printf "%.2f", (c * 100 / v) }')"
branch_rate="$(awk -v c="$branches_covered" -v v="$branches_valid" 'BEGIN { if (v == 0) printf "0.00"; else printf "%.2f", (c * 100 / v) }')"

cat > "$OUTPUT_DIR/summary.txt" <<EOF
Solution coverage summary
Line coverage: $line_rate% ($lines_covered/$lines_valid)
Branch coverage: $branch_rate% ($branches_covered/$branches_valid)
Cobertura: $coverage_file
EOF

cat "$OUTPUT_DIR/summary.txt"
