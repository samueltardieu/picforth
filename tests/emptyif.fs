macro
: test t0if bit-set? ;
target
: t ;
: e ;
: o ;

: test1 test if t then o ;
: test2 test if t else e then o ;
: test3 test if else e then o ;
: test4 test if else then o ;
: test5 test if then o ;