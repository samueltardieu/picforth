needs ../multitasker.fs

meta
9 to task-stack-size
target

: emit ;

task : alarm begin [char] a emit yield again ;

$800 org

main : main multitasker ;
