#!/bin/bash
rm -rf *.vspx ./**/bin/ ./**/obj/ ./**/x64/ ./**/x86/ ./x64/ ./x86/ ./Debug/ ./**/Debug/ ./Release/ ./**/Release/ *.vsp UpgradeLog.htm *.psess
find . -type d -print | xargs rmdir
