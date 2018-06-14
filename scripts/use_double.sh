#!/bin/bash
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
replace="s/use_fixed/use_double/g"
find "$DIR/.." -type f -name '*.csproj' -print0 | xargs -0 sed -i "$replace"
find "$DIR/.." -type f -name '*.csproj' -print0 | xargs -0 unix2dos
