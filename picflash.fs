\
\ PicForth library file
\
\ This library file has been written by Samuel Tardieu <sam@rfc1149.net>.
\ It belongs to the public domain. Do whatever you want with it.
\
\ This file allows reading and writing the flash memory of the PIC.
\

\ flash-read reads the data from the address EEADRH:EEADR. The result can be
\ found in EEDATH:EEDATA

code flash-read ( -- )
    eecon1 adjust-bank forth> drop  \ Select correct bank.
    eepgd bsf                       \ Select program mem.
    rd bsf                          \ Start read operation
    nop                             \ Two cycles...
    nop                             \ ...delay
    restore-bank
    return
end-code

\ flash-write writes the data found in EEDATH:EEDATA to address EEADRH:EEADR

code flash-write ( -- )
    eecon1 adjust-bank forth> drop  \ Select correct bank.
    eepgd bsf                       \ Select program mem.
    wren bsf                        \ Write enable
    disable-interrupts
    55 movlw eecon2 movwf           \ Magic sequence 1,2
    aa movlw eecon2 movwf           \ Magic sequence 3,4
    wr bsf                          \ Start write (magic sequence 5)
    nop nop                         \ Two cycles delay
    enable-interrupts
    wren bcf                        \ Write disable
    restore-bank
    return
end-code

meta

: fcreate ( -- ) create tcshere , does> @ (literal) ;
: f, ( b -- ) 3400 or (cs,) ;
: fallot ( n -- ) 0 ?do 3fff (cs,) loop ;
: fhigh! get-const 8 rshift (literal) eeadrh const-! ;

target
