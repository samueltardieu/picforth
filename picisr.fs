\
\ PicForth library file
\
\ This library file has been written by Samuel Tardieu <sam@rfc1149.net>.
\ It belongs to the public domain. Do whatever you want with it.
\
\ Code for handling interrupt for the PIC. This is quite convoluted as we
\ have to restrict the bank 0 from using address $7f, which must be reserved
\ in all the banks (see PIC datasheet for an explanation).

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

4 org    \ Vector for isr is 4

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
