needs ../multitasker.fs

: emit ;

task : alarm begin [char] a emit yield again ;

$800 org

main : main multitasker ;
