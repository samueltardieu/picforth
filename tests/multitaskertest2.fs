needs ../multitasker.fs

: emit ;

task : alarm begin [char] a emit yield again ;

$1000 org

main : main multitasker ;
