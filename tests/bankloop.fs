\ This test checks that the PCLATH is correctly set inside a loop

: x ;
: y ;

$800 org
main : main x begin y again ;