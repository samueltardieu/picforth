\ fix-for-bootloader.fs
\ 
\ frigs code to:
\  - create ISR vector at 0x0004 (jumps to (isr0save) at 0x7)
\  - create main vector at 0x0005 (jumps to main)
\ 
\ this makes programs able to work with resident bootloaders

host

\ temporary save point for here
tcshere value tcshere-saved

meta

\ sets tcshere, saves old value
: tcshere-set ( nnn --- )
    tcshere to tcshere-saved
    to tcshere
;

\ restores old tcshere
: tcshere-restore ( -- nnn )
    tcshere-saved to tcshere
;

\ set up reset vector
\ 0 tcshere-set
\ code resetvec
\     pclath clrf
\     5 goto
\ end-code

\ set up interrupt vector - jumps to Forth ISR front-end
4 tcshere-set
code isr-vector
    7 goto
end-code

\ start of code ripped from picisr.fs
\ picisr.fs is ripped verbatim, with only one change:
\   the org is changed from 4 to 7, so we can do our vector trickery

host

current-area @
meta> bank0
$7e data-change-high

target
$7f constant w_temp
variable status_temp
variable pclath_temp

host
current-area !

target

\ 4 org    \ Vector for isr is 4
7 org    \ Vector for isr is 4

code (isr-save) ( -- )
    w_temp movwf
    status ,w swapf
    status clrf
    status_temp movwf
    pclath ,w movf
    pclath_temp movwf
end-code

meta

create isr-origin tcshere ,
3 +org

: isr-restore-return ( -- )
    pclath_temp ,w movf
    pclath movwf
    status_temp ,w swapf
    status movwf
    w_temp ,f swapf
    w_temp ,w swapf
    retfie unreachable
;

: enable-interrupts ( -- ) gie bsf ;
: disable-interrupts ( -- ) gie bcf ;

: isr ( -- ) isr-origin @ set-vector ;

target

variable isr-enabled

: suspend-interrupts ( -- )
    0 isr-enabled !
    gie bit-set? if 1 isr-enabled or! then
    gie bit-clr ;

: restore-interrupts ( -- ) isr-enabled @ 1 and if gie bit-set then ;

\ 
\ end of code ripped from picisr.fs
\ 

\ fudge the usual reset vector
meta

\ modified 'main' word to set the vector at 0x5

\ : main ( -- ) 5 set-vector init-picforth ;
: main ( -- )
    5 set-vector
    5 tcshere-set nop
    tcshere-restore
    init-picforth
;

target

