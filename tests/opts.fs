macro
: test t0if bit-set? ;
target

: r ;

: r1 test if r then ;
disallow-optimizations
: r2 test if r then ;
allow-optimizations
: r3 test if r then ;
