#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 || "$1" == "-h" || "$1" == "--help" ]]; then
    echo "Usage: $0 <assembly-path>" >&2
    exit 1
fi

assembly_path=$1
script_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
out_dir="$script_dir/out"
assembly_file=$(basename -- "$assembly_path")
assembly_name=${assembly_file%.*}

mkdir -p "$out_dir"

dotnet run --project "$script_dir/src/Durchblick.Tool" -- "$assembly_path" \
    > "$out_dir/$assembly_name.cs" \
    2> "$out_dir/$assembly_name.errors.txt"

printf 'Wrote %s\n' "$out_dir/$assembly_name.cs"
printf 'Wrote %s\n' "$out_dir/$assembly_name.errors.txt"
