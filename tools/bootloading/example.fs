
\ a tiny demo program to verify that bootloading succeeded
\ counts portB outputs (wire up your portB to LEDs)

\ config stuff
fosc-hs set-fosc
false set-wdte
false set-boden
false set-lvp

\ processor selection
pic16f87x

\ you need this file if you want to run the prog from a bootloader
needs fix-for-bootloader.fs

variable cntr
variable cntr1
variable cntr2
variable cntr3

: tiny-delay
    255 cntr3 v-for v-next ;
: small-delay
    255 cntr2 v-for tiny-delay v-next ;
: delay
    $80 cntr1 v-for small-delay v-next ;

main : main
    0 TRISB !
    0 cntr !
    begin
        cntr @ PORTB !
        1 cntr +!
        delay
    again
;

