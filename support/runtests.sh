#! /bin/sh -e

MAKE=${MAKE-make}
rm -rf testresults
mkdir testresults
for i in tests/*.fs; do
    $MAKE GFORTH="${GFORTH-gforth}" ${i%%fs}disasm
    cp ${i%%fs}hex ${i%%fs}disasm testresults/
done
