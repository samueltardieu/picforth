\
\ PicForth library file
\
\ This library file has been written by Samuel Tardieu <sam@rfc1149.net>.
\ It belongs to the public domain. Do whatever you want with it.
\
\ This file allows reading and writing the EEPROM of the PIC.
\

\ ee@ is similar to @ for the EEPROM

:: ee@ ( addr -- b )
   eeadr !
   eepgd bit-clr
   rd bit-set
   eedata @
;

\ eeprom-write expects the address in EEADR and the data in EEDATA

: eeprom-write ( -- )
    eepgd bit-clr
    wren bit-set
    suspend-interrupts
    55 eecon2 !
    aa eecon2 !
    wr bit-set
    restore-interrupts
    wren bit-clr
;
    
\ wait-for-eeprom waits for the end of a write operation

code wait-for-eeprom ( -- )
    eecon1 adjust-bank forth> drop
label: wait-for-eeprom0
    wr btfsc
    wait-for-eeprom0 goto
    restore-bank
    return
end-code

\ ee! is similar to ! for the EEPROM

: ee! ( b addr -- ) eeadr ! eedata ! eeprom-write wait-for-eeprom ;
