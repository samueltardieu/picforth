\ Simple multitasker example with two tasks running concurrently.

pic16f87x

include multitasker.fs

1 pin-b led1
2 pin-b led2

\ First task toggles led1 each time it is called

task : foo ( -- ) begin led1 toggle yield again ;

\ Second task toggles led2 every 5 times

task : bar ( -- )
    begin
	led2 toggle
	5 begin yield 1- dup while repeat drop \ Relinguish control 5 times
    again
;

\ Main program

main
: main ( -- )
    f9 trisb !
    multitasker
;

\ Configuration

fosc-hs set-fosc    \ High-speed oscillator
