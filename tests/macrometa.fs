include ../picisr.fs
: criticalsection ;
macro
: test1 suspend-interrupts criticalsection restore-interrupts ;
target
main : test2 test1 ;
